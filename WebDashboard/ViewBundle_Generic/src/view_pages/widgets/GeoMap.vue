<template>
  <div>

    <div :id="theID" v-bind:style="{ height: theHeight }" style="min-height: 100px;" @contextmenu="onContextMenu">

    </div>

    <v-menu v-model="contextMenu.show" :position-x="contextMenu.clientX" :position-y="contextMenu.clientY" absolute offset-y>
      <v-list>
        <v-list-item @click="onConfigureMap" >
          <v-list-item-title>Configure Map...</v-list-item-title>
        </v-list-item>
        <v-list-item @click="onConfigureLayers" >
          <v-list-item-title>Configure Layers...</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <GeoMapConfigDlgMap ref="dlgMapConfig" :configuration="config" :backendAsync="backendAsync" />
    <GeoMapConfigDlgLayers ref="dlgLayersConfig" :configuration="config" :backendAsync="backendAsync" />

  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import { TimeRange } from '../../utils'

import * as L from 'leaflet'
import 'leaflet-groupedlayercontrol'
import type { Feature, GeoJsonObject, GeoJsonTypes, BBox } from 'geojson'
import { GeoMapConfig, NamedLayerType, GeoLayerType } from './GeoMapConfigTypes'
import * as fast from '../../fast_types'
import GeoMapConfigDlgMap from './GeoMapConfigDlgMap.vue'
import GeoMapConfigDlgLayers from './GeoMapConfigDlgLayers.vue'
import * as model from '../model'
import parseGeoraster from 'georaster';
import GeoRasterLayer, { GeoRasterLayerOptions } from 'georaster-layer-for-leaflet'

//import 'leaflet/dist/leaflet.css'
import '../../assets/leaflet.css'
import 'leaflet-groupedlayercontrol/src/leaflet.groupedlayercontrol.css'

interface ColorMapRange {
  start: number // inclusive
  end: number   // exclusive
  color: string
}

interface GeoJsonObj {
  type: GeoJsonTypes  // copied from GeoJsonObject
  bbox?: BBox         // copied from GeoJsonObject
  setVariableValues?: Record<string, string>
}

interface GeoJsonUrl {
  type: 'GeoJsonUrl'
  url: string
  setVariableValues?: Record<string, string>
}

interface GeoTiffUrl {
  type: 'GeoTiffUrl'
  url: string
  setVariableValues?: Record<string, string>
  opacity?: number
  colorMap?: ColorMapRange[]
}

// Extend types to handle both direct layers and layer groups
type GeoLayer = L.GeoJSON | L.LayerGroup

interface GeoContentFrame {
  setVariableValues?: Record<string, string>
}

// Types for frame animation
interface GeoJsonFrame extends GeoContentFrame {
  data: GeoJsonObject
}

interface GeoTiffFrame extends GeoContentFrame {
  georaster: any // Type from parseGeoraster
  opacity?: number
  colorMap?: (values: number[]) => string | null
}

interface ColorMapRange {
  start: number; // inclusive
  end: number;   // exclusive
  color: string;
}

function createColorMapper(colorRanges?: ColorMapRange[]): (values: number[]) => string | null {
  if (!colorRanges) {
    return undefined
  }
  const ranges = [...colorRanges]  
  return (values: number[]): string | null => {
    if (!values || values.length === 0) {
      return null
    }
    const value = values[0]
    for (const range of ranges) {
      if (value >= range.start && value < range.end) {
        return range.color
      }
    }
    return null
  }
}

interface AnimationController {
  isRunning: boolean
  currentIndex: number
  lastFrameTime: number
  isPaused: boolean
}

let dgUUID = 0

L.Icon.Default.prototype.options.iconRetinaUrl = 'images/marker-icon-2x.png';
L.Icon.Default.prototype.options.iconUrl = 'images/marker-icon.png';
L.Icon.Default.prototype.options.shadowUrl = 'images/marker-shadow.png';

@Component({
  components: {
    GeoMapConfigDlgMap,
    GeoMapConfigDlgLayers,
  },
})
export default class GeoMap extends Vue {

  @Prop({ default() { return '' } }) id: string
  @Prop({ default() { return '' } }) width: string
  @Prop({ default() { return '' } }) height: string
  @Prop({ default() { return {} } }) config: GeoMapConfig
  @Prop() backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  @Prop({ default() { return '' } }) eventName: string
  @Prop({ default() { return {} } }) eventPayload: object
  @Prop({ default() { return {} } }) timeRange: TimeRange
  @Prop({ default() { return 0 } }) resize: number
  @Prop({ default() { return null } }) dateWindow: number[]
  @Prop({ default() { return {} } }) configVariables: model.ConfigVariableValues
  @Prop() setConfigVariableValues: (variableValues: Record<string, string>) => void

  uid = dgUUID.toString()
  map: L.Map = null

  showConfigDialog = false

  baseMaps: {[key: string]: L.Layer} = { }
  mainLayers: {[key: string]: GeoLayer} = { }
  optionalLayers: {[key: string]: GeoLayer} = { }
  
  stringWithVarResolvedMap: Map<string, string> = new Map()
  resolvedCenter: string = ''
  activeRequests: Map<string, AbortController> = new Map()
  layersWithSetVariableValues: Map<string, Record<string, string>> = new Map()
  
  // Animation properties
  animationControllers: Map<string, AnimationController> = new Map() // layerName -> animation controller
  geoContentFrames: Map<string, GeoContentFrame[]> = new Map() // layerName -> frames

  contextMenu = {
    show: false,
    clientX: 0,
    clientY: 0,
  }
  canUpdateConfig = false

  beforeCreate() {
    dgUUID += 1
  }

  get theID(): string {
    return 'GeoMap_' + this.uid
  }

  mounted(): void {
    this.canUpdateConfig = window.parent['dashboardApp'].canUpdateViewConfig()
    this.initMap()
  }

  beforeDestroy() {
    this.activeRequests.forEach((controller) => {
      controller.abort()
    })    
    this.activeRequests.clear()

    this.stopAllAnimations()
    this.clearMap()
  }

  clearMap(): void {
    // Stop all animations
    this.stopAllAnimations()
    
    if (this.map) {
      this.map.remove()
      this.map = null
    }
    this.layersWithSetVariableValues.clear()
    this.geoContentFrames.clear()
  }
  
  stopAllAnimations(): void {
    // Mark all animations as stopped
    this.animationControllers.forEach((controller) => {
      controller.isRunning = false
    })
    this.animationControllers.clear()
  }

  @Watch('configVariables.VarValues', { deep: true })
  watch_configVariablesVarValues(newVal: object, oldVal: object): void {

    const resolvedCenter = model.VariableReplacer.replaceVariables(this.config.MapConfig.Center, this.configVariables.VarValues)
    if (this.resolvedCenter !== resolvedCenter) {
      this.map.panTo(this.getResolvedCenter())
    }
    
    const resolveMap = this.stringWithVarResolvedMap

    const mainLayersWithVariables = this.config.MainLayers.filter((mainLayer) => {
      const obj = mainLayer.Variable.Object
      const objResolved = model.VariableReplacer.replaceVariables(obj, this.configVariables.VarValues)
      return !resolveMap.has(obj) || resolveMap.get(obj) !== objResolved
    })

    const optionalLayersWithVariables = this.config.OptionalLayers.filter((optionalLayer) => {
      const obj = optionalLayer.Variable.Object
      const objResolved = model.VariableReplacer.replaceVariables(obj, this.configVariables.VarValues)
      return !resolveMap.has(obj) || resolveMap.get(obj) !== objResolved
    })

    if (mainLayersWithVariables.length > 0 || optionalLayersWithVariables.length > 0) {
      this.loadLayers(mainLayersWithVariables.concat(optionalLayersWithVariables))
    }
  }

  getResolvedCenter(): [number, number] {
    this.resolvedCenter = model.VariableReplacer.replaceVariables(this.config.MapConfig.Center, this.configVariables.VarValues)
    const parts = this.resolvedCenter.split(',')
    return [parseFloat(parts[0]), parseFloat(parts[1])]
  }

  async initMap(): Promise<void> {
    
    this.clearMap()

    const config: GeoMapConfig = this.config
   
    const mapOptions: L.MapOptions = {
      center: this.getResolvedCenter(),
      zoom: config.MapConfig.ZoomDefault,
      zoomControl: true,
      zoomSnap: 0.1,
      zoomDelta: 0.5
    }

    this.map = L.map(this.theID, mapOptions)
    this.map.on('layeradd', (e) => this.onLayerAdd(e))
    this.map.on('layerremove', (e) => this.onLayerRemove(e))

    this.baseMaps = { }
    let isFirstBaseLayer = true
    for (const tileLayer of config.TileLayers) {
      const layer = L.tileLayer(tileLayer.Url, {
        attribution: tileLayer.Attribution,
        minZoom: tileLayer.MinZoom,
        maxZoom: tileLayer.MaxZoom
      })
      this.baseMaps[tileLayer.Name] = layer
      if (isFirstBaseLayer) {
        layer.addTo(this.map)
        isFirstBaseLayer = false
      }
    }   

    this.mainLayers = { }
    let isFirstMainLayer = true
    for (const mainLayer of config.MainLayers) {
      const layer = this.createLayer(mainLayer)
      this.mainLayers[mainLayer.Name] = layer
      if (isFirstMainLayer) {
        layer.addTo(this.map)
        isFirstMainLayer = false
      }
    }

    this.optionalLayers = { }
    for (const optionalLayer of config.OptionalLayers) {
      const layer = this.createLayer(optionalLayer)
      this.optionalLayers[optionalLayer.Name] = layer
      if (optionalLayer.IsSelected) {
        layer.addTo(this.map)
      }
    }

    const hasSeveralBaseMaps = Object.keys(this.baseMaps).length > 1
    const hasSeveralMainLayers = Object.keys(this.mainLayers).length > 1
    const hasOptionalLayers = Object.keys(this.optionalLayers).length > 0

    // Only add the control if there's something to select
    if (hasSeveralBaseMaps || hasSeveralMainLayers || hasOptionalLayers) {

      const MainLabel = config.MapConfig.MainGroupLabel
      const OptionalLabel = config.MapConfig.OptionalGroupLabel

      const groupedOverlays = {}
      groupedOverlays[MainLabel] = this.mainLayers
      groupedOverlays[OptionalLabel] = this.optionalLayers

      const hasSeveralMainLayers = Object.keys(this.mainLayers).length > 1
      if (!hasSeveralMainLayers) {
        delete groupedOverlays[MainLabel]
      }

      const options: L.GroupedLayersOptions = {
        exclusiveGroups: hasSeveralMainLayers ? [MainLabel] : [],
        groupCheckboxes: false
      }

      const control = L.control.groupedLayers(
        this.baseMaps, 
        groupedOverlays, 
        options)

      control.addTo(this.map)
      
      
    }
    else {

      if (Object.keys(this.baseMaps).length > 0) {
        const singleBaseMap = Object.values(this.baseMaps)[0]
        singleBaseMap.addTo(this.map)
      }

      if (Object.keys(this.mainLayers).length > 0) {
        const singleMainLayer = Object.values(this.mainLayers)[0]
        singleMainLayer.addTo(this.map)
      }

    }
    await this.loadLayers()

    if (this.config.LegendConfig && this.config.LegendConfig.File && this.config.LegendConfig.File.length > 0) {

      const url = '/WebAssets/' + this.config.LegendConfig.File
      const width = this.config.LegendConfig.Width
      const height = this.config.LegendConfig.Height

      // fetch the legend image:
      const response = await fetch(url)
      if (response.ok) {
        const blob = await response.blob()
        const url = URL.createObjectURL(blob)
        const img = document.createElement('img')
        img.src = url
        img.width = width
        img.height = height
        img.style.position = 'absolute'
        img.style.bottom = '10px'
        img.style.left = '10px'
        img.style.zIndex = '6'
        this.map.getContainer().appendChild(img)
      }
    }
  }

  createLayer(layer: NamedLayerType): GeoLayer {
    if (layer.Type === 'GeoJson' || layer.Type === undefined) {
      return L.geoJSON(null, { 
        style: this.getFeatureStyle,
        onEachFeature: this.onEachFeature,
        pointToLayer: this.pointToLayer
      })
    }
    else if (layer.Type === 'GeoTiff') {
      // Create a LayerGroup to hold the GeoTiff layer
      return L.layerGroup()
    }
    else {
      throw new Error('Unknown layer type: ' + layer.Type)
    }
  }

  onLayerAdd(e: L.LayerEvent): void {
    // Find the layer name
    let layerName: string | null = null
    for (const [name, layer] of Object.entries(this.mainLayers)) {
      if (layer === e.layer) {
        layerName = name
        break
      }
    }
    if (!layerName) {
      for (const [name, layer] of Object.entries(this.optionalLayers)) {
        if (layer === e.layer) {
          layerName = name
          break
        }
      }
    }

    if (layerName) {
      // Set any variable values from the layer
      if (this.layersWithSetVariableValues.has(layerName)) {
        const variableValues = this.layersWithSetVariableValues.get(layerName)
        this.setConfigVariableValues(variableValues)
      }
      
      // Start animation if this layer has multiple frames
      const hasGeoContentFrames = this.geoContentFrames.has(layerName) && this.geoContentFrames.get(layerName).length > 1      
      if (hasGeoContentFrames) {
        this.startLayerAnimation(layerName)
      }
    }
  }
  
  onLayerRemove(e: L.LayerEvent): void {
    // Find the layer name
    let layerName: string | null = null
    for (const [name, layer] of Object.entries(this.mainLayers)) {
      if (layer === e.layer) {
        layerName = name
        break
      }
    }
    if (!layerName) {
      for (const [name, layer] of Object.entries(this.optionalLayers)) {
        if (layer === e.layer) {
          layerName = name
          break
        }
      }
    }

    if (layerName) {
      // Stop animation for this layer
      this.stopLayerAnimation(layerName)
    }
  }

  async loadLayers(layers?: NamedLayerType[]): Promise<void> {

    if (!layers) {
      layers = this.config.MainLayers.concat(this.config.OptionalLayers)
    }

    for (const layer of layers) {
      await this.loadLayerContent(layer)
      const obj = layer.Variable.Object
      this.stringWithVarResolvedMap.set(obj, model.VariableReplacer.replaceVariables(obj, this.configVariables.VarValues))
    }
  }

  getFeatureStyle(feature?: Feature): L.PathOptions  {
    if (feature && feature.properties && feature.properties.style) {
      return feature.properties.style
    }
    return {
    }
  }

  pointToLayer(feature: Feature, latlng: L.LatLng): L.Layer {
    if (feature?.properties?.markerIcon) {
      const url = feature.properties.markerIcon.url
      const width = feature.properties.markerIcon.width
      const height = feature.properties.markerIcon.height
      const anchorX = feature.properties.markerIcon.anchorX
      const anchorY = feature.properties.markerIcon.anchorY
      const theIcon = L.icon({
        iconUrl: url,
        iconSize: [width, height],
        iconAnchor: [anchorX, anchorY],
      })
      return L.marker(latlng, {
        icon: theIcon
      })
    }
    return L.marker(latlng)
  }

  onEachFeature(feature: Feature, layer: L.Layer): void {

    if (feature?.properties?.label) {
      const label = feature.properties.label
      const text: string = label.text      
      const direction = label.direction || 'right'
      const offsetX = label.offsetX || 0
      const offsetY = label.offsetY || 0
      const options: L.TooltipOptions = {
        permanent: true,
        direction: direction,
        offset: [offsetX, offsetY],
        className: 'geomap-label'
      }
      layer.bindTooltip(text, options)
      if (label.style && typeof label.style === 'object' && label.style !== null) {
        layer.on('tooltipopen', (event) => {
          const tooltipElement = event.tooltip.getElement()
          if (tooltipElement) {
            for (const [key, value] of Object.entries(label.style)) {
              tooltipElement.style[key] = value
            }
          }
        })
      }
    }

    if (feature?.properties?.tooltip) {
      let tooltip = feature.properties.tooltip
      if (typeof tooltip === 'string') {
        // replace newlines with <br>:
        tooltip = tooltip.replace(/(?:\r\n|\r|\n)/g, '<br>')
      }
      else if (typeof tooltip === 'object') {
        tooltip = Object.entries(tooltip)
                        .map(([key, value]) => `<strong>${key}:</strong> ${value}`)
                        .join('<br>')
      }
      else {
        return
      }

      const options: L.TooltipOptions = {
        permanent: false,
        direction: 'center',
        sticky: false,
        //className: 'geo-feature-tooltip'
      }

      layer.bindTooltip(tooltip, options)
    }

    if (feature?.properties?.popup) {
      let popup = feature.properties.popup
      if (typeof popup === 'string') {
        // replace newlines with <br>:
        popup = popup.replace(/(?:\r\n|\r|\n)/g, '<br>')
      }
      else if (typeof popup === 'object') {
        popup = Object.entries(popup)
                        .map(([key, value]) => `<strong>${key}:</strong> ${value}`)
                        .join('<br>')
      }
      else {
        return
      }

      const options: L.PopupOptions = {
        autoClose: true,
        closeOnClick: true,
        //className: 'geo-feature-popup'
      }

      layer.bindPopup(popup, options)
    }

    if (feature?.properties?.setVariableValues) {
      const setVariableValues: Record<string, string> = feature.properties.setVariableValues
      layer.on('click', (e: L.LeafletMouseEvent) => {   
        this.setConfigVariableValues(setVariableValues)
      })
    }

    if (feature?.properties?.style?.fillOpacity) {     

      const stdOpacity = feature.properties.style.opacity || 1.0
      const stdFillOpacity = feature.properties.style.fillOpacity
      const delta = this.config.MapConfig.MouseOverOpacityDelta || 0.3
      const mouseOverFillOpacity = Math.min(1.0, stdFillOpacity + delta)
      const mouseOverOpacity = Math.min(1.0, stdOpacity + delta)

      layer.on({
        mouseover: (e) => {
          const layer = e.target
          layer.setStyle({
            fillOpacity: mouseOverFillOpacity,
            opacity: mouseOverOpacity
          })
        },
        mouseout: (e) => {
          const layer: L.Path = e.target;
          layer.setStyle({
            fillOpacity: stdFillOpacity,
            opacity: stdOpacity
          })
        }
      })
    }
  }

  @Watch('timeRange')
  watch_timeRange(newVal: object, old: object): void {
    // Stop all animations before reloading layers
    this.stopAllAnimations()
    this.loadLayers()
  }

  async loadLayerContent(layerObj: NamedLayerType): Promise<void> {

    const layerName = layerObj.Name
    const layer: GeoLayer = this.mainLayers[layerName] || this.optionalLayers[layerName]
    const variable: fast.VariableRef = layerObj.Variable
    const layerType: GeoLayerType = layerObj.Type
    const frameCount = layerObj.FrameCount || 1
    
    this.stopLayerAnimation(layerName)

    if (this.activeRequests.has(layerName)) {
      this.activeRequests.get(layerName).abort()
      this.activeRequests.delete(layerName)
    }

    const abortController = new AbortController()
    this.activeRequests.set(layerName, abortController)

    try {

      this.geoContentFrames.delete(layerName)
      
      const dataArray: GeoJsonObj[] | GeoJsonUrl[] | GeoTiffUrl[]  = await this.backendAsync('GetGeoData', { 
        variable: variable, 
        timeRange: this.timeRange,
        frameCount: frameCount,
        configVars: this.configVariables.VarValues
      })
      
      if (abortController.signal.aborted) {
        console.info(`Request for layer ${layerName} was aborted after backendAsync`)
        return
      }
      
      if (!Array.isArray(dataArray) || dataArray.length === 0) {
        throw new Error(`Received empty or invalid data for layer ${layerName}`)
      }
      
      // Process based on data type of first frame
      const firstItem = dataArray[0]
      
      if (firstItem.type === 'GeoTiffUrl') { // Handle GeoTiff URL data

        if (layerType === 'GeoJson') {
          throw new Error('Expected GeoJson data, but got GeoTiff data')
        }
        
        const frames: GeoTiffFrame[] = []
        
        for (const geoTiffUrl of dataArray as GeoTiffUrl[]) {

          if (abortController.signal.aborted) {
            return
          }
          
          try {
            const fetchStartTime = performance.now()
            const response = await fetch(geoTiffUrl.url, { 
              signal: abortController.signal 
            })
            const fetchEndTime = performance.now()
            
            if (abortController.signal.aborted) {
              return
            }
            
            if (!response.ok) {
              throw new Error(`Failed to load GeoTiff data: ${response.statusText}`)
            }
            
            const arrayBufferStartTime = performance.now()
            const arrayBuffer = await response.arrayBuffer()
            const arrayBufferEndTime = performance.now()
            
            if (abortController.signal.aborted) {
              return
            }
            
            const parseStartTime = performance.now()
            const georaster = await parseGeoraster(arrayBuffer)
            const parseEndTime = performance.now()
            
            if (abortController.signal.aborted) {
              return
            }
            
            frames.push({
              georaster,
              opacity: geoTiffUrl.opacity || 0.9,
              colorMap: createColorMapper(geoTiffUrl.colorMap),
              setVariableValues: geoTiffUrl.setVariableValues
            })
            
            console.info(`Loaded GeoTiff frame: fetched in ${fetchEndTime - fetchStartTime}ms, arrayBuffer in ${arrayBufferEndTime - arrayBufferStartTime}ms, parsed in ${parseEndTime - parseStartTime}ms`)
          }
          catch (error) {
            console.error(`Failed to load GeoTiff frame: ${error.message}`)
          }
        }
        
        if (frames.length === 0) {
          throw new Error(`No valid GeoTiff frames loaded for layer ${layerName}`)
        }

        this.geoContentFrames.set(layerName, frames)
        this.setGeoTiffLayerContent(layer, frames[0])
        this.setFrameVariables(frames[0], layerName, layer)

        if (frames.length > 1 && this.map.hasLayer(layer)) {
          this.startLayerAnimation(layerName)
        }

      }
      else if (firstItem.type === 'GeoJsonUrl') { // Handle GeoJSON URL data

        if (layerType === 'GeoTiff') {
          throw new Error('Expected GeoTiff data, but got GeoJson data')
        }
        
        const frames: GeoJsonFrame[] = []
        
        for (const geoJsonUrl of dataArray as GeoJsonUrl[]) {

          if (abortController.signal.aborted) {
            return
          }
          
          try {
            const fetchStartTime = performance.now()
            const response = await fetch(geoJsonUrl.url, { 
              signal: abortController.signal 
            })
            const fetchEndTime = performance.now()
            
            if (abortController.signal.aborted) {
              return
            }
            
            if (!response.ok) {
              throw new Error(`Failed to load GeoJSON data: ${response.statusText}`)
            }
            
            const jsonStartTime = performance.now()
            const geoJsonData = await response.json()
            const jsonEndTime = performance.now()
            
            if (abortController.signal.aborted) {
              return
            }
            
            frames.push({
              data: geoJsonData as GeoJsonObject,
              setVariableValues: geoJsonUrl.setVariableValues
            })
            
            console.info(`Loaded GeoJSON frame: fetched in ${fetchEndTime - fetchStartTime}ms, parsed in ${jsonEndTime - jsonStartTime}ms`)
          }
          catch (error) {
            console.error(`Failed to load GeoJSON frame: ${error.message}`)
          }
        }
        
        if (frames.length === 0) {
          throw new Error(`No valid GeoJSON frames loaded for layer ${layerName}`)
        }
        
        this.geoContentFrames.set(layerName, frames)
        this.setGeoJsonLayerContent(layer as L.GeoJSON, frames[0])
        this.setFrameVariables(frames[0], layerName, layer)
        
        // Start animation if there are multiple frames
        if (frames.length > 1 && this.map.hasLayer(layer)) {
          this.startLayerAnimation(layerName)
        }
        
      }
      else { // Handle direct GeoJSON data

        if (layerType === 'GeoTiff') {
          throw new Error('Expected GeoTiff data, but got GeoJson data')
        }
        
        const frames: GeoJsonFrame[] = dataArray.map(data => ({
          data: data as GeoJsonObject,
          setVariableValues: (data as any).setVariableValues
        }))
        
        this.geoContentFrames.set(layerName, frames)
        this.setGeoJsonLayerContent(layer as L.GeoJSON, frames[0])
        this.setFrameVariables(frames[0], layerName, layer)
        
        // Start animation if there are multiple frames
        if (frames.length > 1 && this.map.hasLayer(layer)) {
          this.startLayerAnimation(layerName)
        }
        
      }
    } 
    catch (err) {
      // Don't show errors for aborted requests
      if (err.name === 'AbortError') {
        console.info(`Request for layer ${layerName} was aborted via exception`)
      }
      else {
        console.error(err.message)
        layer.clearLayers()
      }
    }
    finally {
      // Clean up if this is still the active request
      if (this.activeRequests.get(layerName) === abortController) {
        this.activeRequests.delete(layerName)
      }
    }
  }

  setFrameVariables(frame: GeoContentFrame, layerName: string, layer: GeoLayer): void {
    if (frame.setVariableValues) {
      this.layersWithSetVariableValues.set(layerName, frame.setVariableValues)
      if (this.map.hasLayer(layer)) {
        this.setConfigVariableValues(frame.setVariableValues)
      }
    }
  }
  
  startLayerAnimation(layerName: string): void {
    // Clear any existing animation
    this.stopLayerAnimation(layerName)
    
    const layer = this.mainLayers[layerName] || this.optionalLayers[layerName]
    if (!layer) return
    
    const isGeoJson = layer instanceof L.GeoJSON
    const frames = this.geoContentFrames.get(layerName)
    
    if (!frames || frames.length <= 1) return
    
    // Create animation controller
    const controller: AnimationController = {
      isRunning: true,
      currentIndex: 0,
      lastFrameTime: 0,
      isPaused: false
    }
    this.animationControllers.set(layerName, controller)
    
    // Start animation loop
    this.animateLayer(layerName, layer, frames, isGeoJson, controller)
  }
  
  private animateLayer(
    layerName: string, 
    layer: GeoLayer, 
    frames: GeoContentFrame[], 
    isGeoJson: boolean,
    controller: AnimationController
  ): void {
    
    const frameInterval = this.config.MapConfig.FrameDelay
    
    const animate = (timestamp: number) => {

      if (!controller.isRunning) return
      
      // Request next frame first to ensure smooth animation
      requestAnimationFrame(animate)
      
      if (controller.isPaused) return
      
      // Calculate time since last frame
      const elapsed = controller.lastFrameTime ? timestamp - controller.lastFrameTime : frameInterval
      
      // Check if it's time to show next frame
      if (elapsed >= frameInterval) {
        // Update last frame time, accounting for any "debt" time
        controller.lastFrameTime = timestamp - (elapsed % frameInterval)
        
        const frame = frames[controller.currentIndex]
        
        try {
          // Update the layer with new frame data
          if (isGeoJson) {
            this.setGeoJsonLayerContent(layer as L.GeoJSON, frame as GeoJsonFrame)
          } 
          else {
            this.setGeoTiffLayerContent(layer, frame as GeoTiffFrame)
          }
          
          this.setFrameVariables(frame, layerName, layer)
        } 
        catch (error) {
          console.error(`Error updating frame ${controller.currentIndex} for layer ${layerName}:`, error)
        }
        
        // Move to next frame
        controller.currentIndex = (controller.currentIndex + 1) % frames.length
        
        // Handle end of loop pause if needed
        if (controller.currentIndex === 0 && this.config.MapConfig.EndOfLoopPause > 0) {
          controller.isPaused = true
          setTimeout(() => {
            if (controller.isRunning) {
              controller.isPaused = false
              controller.lastFrameTime = 0 // Reset timing
            }
          }, this.config.MapConfig.EndOfLoopPause)
        }
      }
    }
    
    requestAnimationFrame(animate)
  }
  
  stopLayerAnimation(layerName: string): void {
    if (this.animationControllers.has(layerName)) {
      this.animationControllers.get(layerName).isRunning = false
      this.animationControllers.delete(layerName)
    }
  }

  setGeoTiffLayerContent(layer: L.LayerGroup, frame: GeoTiffFrame): void {
    const options: GeoRasterLayerOptions = {
      georaster: frame.georaster,
      opacity: frame.opacity || 0.9,
      zIndex: 4,
      pixelValuesToColorFn: frame.colorMap,
      resolution: this.config.MapConfig.GeoTiffResolution
    }
    const rasterLayer = new GeoRasterLayer(options)

    const oldLayers = layer.getLayers()

    //layer.clearLayers()
    layer.addLayer(rasterLayer)

    for (const oldLayer of oldLayers) {
      layer.removeLayer(oldLayer)      
    }
  }

  setGeoJsonLayerContent(layer: L.GeoJSON, frame: GeoJsonFrame): void {
    layer.clearLayers()
    layer.addData(frame.data)
  }

  get theHeight(): string {
    if (this.height.trim() === '') { return 'auto' }
    return this.height
  }

  onContextMenu(e: any): void {
    if (this.canUpdateConfig) {
      e.preventDefault()
      e.stopPropagation()
      this.contextMenu.show = false
      this.contextMenu.clientX = e.clientX
      this.contextMenu.clientY = e.clientY
      const context = this
      this.$nextTick(() => {
        context.contextMenu.show = true
      })
    }
  }

  async onConfigureMap(): Promise<void> {
    const dlg = this.$refs.dlgMapConfig as GeoMapConfigDlgMap
    const ok = await dlg.showDialog()
    if (ok) {
      await this.initMap()
    }
  }

  async onConfigureLayers(): Promise<void> {
    const dlg = this.$refs.dlgLayersConfig as GeoMapConfigDlgLayers
    const ok = await dlg.showDialog()
    if (ok) {
      await this.initMap()
    }
  }

  @Watch('eventPayload')
  watch_event(newVal: object, old: object): void {
    if (this.eventName === 'OnVarChanged') {
        const obj = this.eventPayload['Object'] as string
        const name = this.eventPayload['Name'] as string
        for (const mainLayer of this.config.MainLayers) {
          if (mainLayer.Variable.Object === obj && mainLayer.Variable.Name === name) {
            this.loadLayerContent(mainLayer)
          }
        }
        for (const optionalLayer of this.config.OptionalLayers) {
          if (optionalLayer.Variable.Object === obj && optionalLayer.Variable.Name === name) {
            this.loadLayerContent(optionalLayer)
          }
        }
    }
  }
}

</script>

<style>
.geomap-label {
	position: absolute;
	padding: 0px;
	padding-right: 3px;
	padding-left: 3px;
	background-color: #ffffff;
	border: 1px solid #fff;
	border-radius: 3px;
	font-weight: bold;
	color: #222;
	line-height: 16px;
	white-space: nowrap;
	-webkit-user-select: none;
	-moz-user-select: none;
	-ms-user-select: none;
	user-select: none;
	pointer-events: none;
	box-shadow: 0 1px 3px rgba(0,0,0,0.4);
}
</style>
