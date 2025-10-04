<template>
  <div>
    <div
      :id="theID"
      style="min-height: 100px"
      :style="{ height: theHeight }"
      @contextmenu="onContextMenu"
    ></div>

    <v-menu
      v-model="contextMenu.show"
      :target="[contextMenu.clientX, contextMenu.clientY]"
    >
      <v-list>
        <v-list-item
          v-if="canUpdateConfig"
          @click="onConfigureMap"
        >
          <v-list-item-title>Configure Map...</v-list-item-title>
        </v-list-item>
        <v-list-item
          v-if="canUpdateConfig"
          @click="onConfigureLayers"
        >
          <v-list-item-title>Configure Layers...</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <GeoMapConfigDlgMap
      ref="dlgMapConfig"
      :backend-async="backendAsync"
      :configuration="config"
    />
    <GeoMapConfigDlgLayers
      ref="dlgLayersConfig"
      :backend-async="backendAsync"
      :configuration="config"
    />
    <GeoMapHtmlDialog ref="htmlDialog" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'
import type { TimeRange } from '../../utils'
import type * as fast from '../../fast_types'
import * as model from '../model'

import * as L from 'leaflet'
import 'leaflet-groupedlayercontrol'
import type { Feature, GeoJsonObject, GeoJsonTypes, BBox } from 'geojson'
import { type GeoMapConfig, type NamedLayerType, type GeoLayerType, type StaticLayer } from './GeoMapConfigTypes'
import GeoMapConfigDlgMap from './GeoMapConfigDlgMap.vue'
import GeoMapConfigDlgLayers from './GeoMapConfigDlgLayers.vue'
import GeoMapHtmlDialog from './GeoMapHtmlDialog.vue'
// Global variables for georaster packages - loaded via script tags
declare const parseGeoraster: (data: ArrayBuffer | Blob | string) => Promise<any>

interface GeoRasterLayerOptions {
  georaster: any
  opacity?: number
  zIndex?: number
  pixelValuesToColorFn?: (values: number[]) => string | null
  resolution?: number
}

interface GeoRasterLayerConstructor {
  new (options: GeoRasterLayerOptions): L.GridLayer
}

declare const GeoRasterLayer: GeoRasterLayerConstructor

//import parseGeoraster from 'georaster'
//import GeoRasterLayer from 'georaster-layer-for-leaflet'

//import 'leaflet/dist/leaflet.css'
import '../../assets/leaflet.css'
import 'leaflet-groupedlayercontrol/src/leaflet.groupedlayercontrol.css'

interface ColorMapRange {
  start: number // inclusive
  end: number // exclusive
  color: string
}

interface GeoJsonObj {
  type: GeoJsonTypes // copied from GeoJsonObject
  bbox?: BBox // copied from GeoJsonObject
  setVariableValues?: Record<string, string>
  setWidgetTitleVarValues?: Record<string, string>
}

interface GeoJsonUrl {
  type: 'GeoJsonUrl'
  url: string
  setVariableValues?: Record<string, string>
  setWidgetTitleVarValues?: Record<string, string>
}

interface GeoTiffUrl {
  type: 'GeoTiffUrl'
  url: string
  setVariableValues?: Record<string, string>
  setWidgetTitleVarValues?: Record<string, string>
  opacity?: number
  colorMap?: ColorMapRange[]
}

// Extend types to handle both direct layers and layer groups
type GeoLayer = L.GeoJSON | L.LayerGroup

interface GeoContentFrame {
  setVariableValues?: Record<string, string>
  setWidgetTitleVarValues?: Record<string, string>
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

function createColorMapper(colorRanges?: ColorMapRange[]): ((values: number[]) => string | null) | undefined {
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

// @ts-ignore
L.Icon.Default.prototype.options.iconRetinaUrl = 'assets/images/marker-icon-2x.png'
// @ts-ignore
L.Icon.Default.prototype.options.iconUrl = 'assets/images/marker-icon.png'
// @ts-ignore
L.Icon.Default.prototype.options.shadowUrl = 'assets/images/marker-shadow.png'

// Props
interface Props {
  id?: string
  width?: string
  height?: string
  config?: GeoMapConfig
  backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  eventName?: string
  eventPayload?: object
  timeRange?: TimeRange
  resize?: number
  dateWindow?: number[] | null
  configVariables?: model.ConfigVariableValues
  setConfigVariableValues: (variableValues: Record<string, string>) => void
  setWidgetTitleVarValues: (variableValues: Record<string, string>) => void
}

const props = withDefaults(defineProps<Props>(), {
  id: '',
  width: '',
  height: '',
  config: () => ({}) as GeoMapConfig,
  eventName: '',
  eventPayload: () => ({}),
  timeRange: () => ({}) as TimeRange,
  resize: 0,
  dateWindow: null,
  configVariables: () => ({}) as model.ConfigVariableValues,
})

const uid = ref(Date.now().toString() + Math.random().toString(36).substr(2, 9))
const map = ref<L.Map | null>(null)
const baseMaps = ref<Record<string, L.Layer>>({})
const staticLayers = ref<{ [key: string]: GeoLayer }>({})
const mainLayers = ref<{ [key: string]: GeoLayer }>({})
const optionalLayers = ref<{ [key: string]: GeoLayer }>({})
const stringWithVarResolvedMap = ref<Map<string, string>>(new Map())
const resolvedCenter = ref('')
const activeRequests = ref<Map<string, AbortController>>(new Map())
const layersWithSetVariableValues = ref<Map<string, Record<string, string>>>(new Map())
const layersWithSetWidgetTitleVarValues = ref<Map<string, Record<string, string>>>(new Map())

// Animation properties
const animationControllers = ref<Map<string, AnimationController>>(new Map())
const geoContentFrames = ref<Map<string, GeoContentFrame[]>>(new Map())
const globalPaused = ref(false)

const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})
const canUpdateConfig = ref(false)
const playPauseControl = ref<L.Control | null>(null)

// Template refs
const dlgMapConfig = ref<InstanceType<typeof GeoMapConfigDlgMap> | null>(null)
const dlgLayersConfig = ref<InstanceType<typeof GeoMapConfigDlgLayers> | null>(null)
const htmlDialog = ref<InstanceType<typeof GeoMapHtmlDialog> | null>(null)

// Computed
const theID = computed(() => {
  return 'GeoMap_' + uid.value
})

const theHeight = computed(() => {
  if (props.height.trim() === '') {
    return 'auto'
  }
  return props.height
})

// Methods
const initPlayPauseControl = (): void => {
  // Remove any existing control
  if (playPauseControl.value) {
    playPauseControl.value.remove()
    playPauseControl.value = null
  }

  // Create a custom control for play/pause
  // @ts-ignore
  const PlayPauseControl = L.Control.extend({
    options: {
      position: 'bottomright',
    },

    onAdd: (_map: L.Map) => {
      // @ts-ignore
      const container = L.DomUtil.create('div', 'leaflet-bar leaflet-control leaflet-control-custom')
      // @ts-ignore
      const button = L.DomUtil.create('a', '', container)

      button.href = '#'
      button.title = globalPaused.value ? 'Play animation' : 'Pause animation'
      button.innerHTML = globalPaused.value ? '▶' : '⏸'
      button.style.fontSize = '16px'
      button.style.textAlign = 'center'
      button.style.lineHeight = '26px'
      button.style.fontWeight = 'bold'
      button.style.width = '30px'
      button.style.height = '30px'
      button.style.display = 'flex'
      button.style.alignItems = 'center'
      button.style.justifyContent = 'center'

      // @ts-ignore
      L.DomEvent.on(button, 'click', L.DomEvent.stop)
      // @ts-ignore
      L.DomEvent.on(button, 'click', () => {
        toggleGlobalPlayPause()
        button.innerHTML = globalPaused.value ? '▶' : '⏸'
        button.title = globalPaused.value ? 'Play animation' : 'Pause animation'
      })

      // Prevent propagation of mousedown/up events to map
      // @ts-ignore
      L.DomEvent.disableClickPropagation(container)

      return container
    },
  })

  // Only add the control if we have any animatable layers
  const hasAnimatableLayers = hasAnimatableLayersFunc()
  if (hasAnimatableLayers && map.value) {
    const currentMap = map.value as L.Map
    const control = new PlayPauseControl()
    control.addTo(currentMap)
    playPauseControl.value = control
  }
}

const hasAnimatableLayersFunc = (): boolean => {
  // Check if we have any layers with multiple frames
  for (const frames of Array.from(geoContentFrames.value.values())) {
    if (frames.length > 1) {
      return true
    }
  }
  return false
}

const toggleGlobalPlayPause = (): void => {
  globalPaused.value = !globalPaused.value
  // Apply to all active animations
  animationControllers.value.forEach((controller) => {
    controller.isPaused = globalPaused.value
  })
}

const clearMap = (): void => {
  // Stop all animations
  stopAllAnimations()

  // Remove play/pause control
  if (playPauseControl.value) {
    playPauseControl.value.remove()
    playPauseControl.value = null
  }

  if (map.value) {
    map.value.remove()
    map.value = null
  }
  staticLayers.value = {}
  layersWithSetVariableValues.value.clear()
  layersWithSetWidgetTitleVarValues.value.clear()
  geoContentFrames.value.clear()
}

const stopAllAnimations = (): void => {
  // Mark all animations as stopped
  animationControllers.value.forEach((controller) => {
    controller.isRunning = false
  })
  animationControllers.value.clear()
}

const getResolvedCenter = (): [number, number] => {
  resolvedCenter.value = model.VariableReplacer.replaceVariables(props.config.MapConfig.Center, props.configVariables?.VarValues)
  const parts = resolvedCenter.value.split(',')
  return [parseFloat(parts[0]), parseFloat(parts[1])]
}

const fetchUrl = async (url: string, opts?: RequestInit): Promise<Response> => {
  // const theUrl = `http://localhost:9999${url}`
  const backend: string = (window.parent as any)['dashboardApp']?.getBackendUrl() || ''
  const theUrl = backend + url
  return await fetch(theUrl, opts)
}

const initMap = async (): Promise<void> => {
  clearMap()

  const config: GeoMapConfig = props.config

  globalPaused.value = !config.MapConfig.AutoPlayLoop

  const mapOptions: L.MapOptions = {
    center: getResolvedCenter(),
    zoom: config.MapConfig.ZoomDefault,
    zoomControl: true,
    zoomSnap: 0.1,
    zoomDelta: 0.5,
  }

  const currentMap = L.map(theID.value, mapOptions)
  map.value = currentMap
  currentMap.on('layeradd', onLayerAdd)
  currentMap.on('layerremove', onLayerRemove)

  baseMaps.value = {}
  let isFirstBaseLayer = true
  for (const tileLayer of config.TileLayers) {
    const layer = L.tileLayer(tileLayer.Url, {
      attribution: tileLayer.Attribution,
      minZoom: tileLayer.MinZoom,
      maxZoom: tileLayer.MaxZoom,
    })
    baseMaps.value[tileLayer.Name] = layer
    if (isFirstBaseLayer) {
      layer.addTo(currentMap)
      isFirstBaseLayer = false
    }
  }

  // Load static layers (between tile layers and main/optional layers)
  staticLayers.value = {}
  for (const staticLayer of config.StaticLayers) {
    const layer = await loadStaticLayer(staticLayer)
    if (layer) {
      staticLayers.value[staticLayer.Name] = layer
      if (staticLayer.IsSelected) {
        layer.addTo(currentMap)
      }
    }
  }

  mainLayers.value = {}
  let isFirstMainLayer = true
  for (const mainLayer of config.MainLayers) {
    const layer = createLayer(mainLayer)
    mainLayers.value[mainLayer.Name] = layer
    if (isFirstMainLayer) {
      layer.addTo(currentMap)
      isFirstMainLayer = false
    }
  }

  optionalLayers.value = {}
  for (const optionalLayer of config.OptionalLayers) {
    const layer = createLayer(optionalLayer)
    optionalLayers.value[optionalLayer.Name] = layer
    if (optionalLayer.IsSelected) {
      layer.addTo(currentMap)
    }
  }

  const hasSeveralBaseMaps = Object.keys(baseMaps.value).length > 1
  const hasSeveralMainLayers = Object.keys(mainLayers.value).length > 1
  const hasOptionalLayers = Object.keys(optionalLayers.value).length > 0
  const hasStaticLayers = Object.keys(staticLayers.value).length > 0

  // Only add the control if there's something to select
  if (hasSeveralBaseMaps || hasSeveralMainLayers || hasOptionalLayers || hasStaticLayers) {
    const MainLabel = config.MapConfig.MainGroupLabel
    const OptionalLabel = config.MapConfig.OptionalGroupLabel

    const groupedOverlays: Record<string, Record<string, GeoLayer>> = {
      Static: staticLayers.value,
      [MainLabel]: mainLayers.value,
      [OptionalLabel]: optionalLayers.value,
    }

    const hasSeveralMainLayersCheck = Object.keys(mainLayers.value).length > 1
    if (!hasSeveralMainLayersCheck) {
      delete groupedOverlays[MainLabel]
    }

    if (!hasStaticLayers) {
      delete groupedOverlays['Static']
    }

    const options: L.GroupedLayersOptions = {
      exclusiveGroups: hasSeveralMainLayersCheck ? [MainLabel] : [],
      groupCheckboxes: false,
    }

    const control = L.control.groupedLayers(baseMaps.value, groupedOverlays, options)

    control.addTo(currentMap)
  } else {
    if (Object.keys(baseMaps.value).length > 0) {
      const singleBaseMap = Object.values(baseMaps.value)[0]
      singleBaseMap.addTo(currentMap)
    }

    if (Object.keys(mainLayers.value).length > 0) {
      const singleMainLayer = Object.values(mainLayers.value)[0]
      singleMainLayer.addTo(currentMap)
    }
  }

  await loadLayers()

  if (props.config.LegendConfig && props.config.LegendConfig.File && props.config.LegendConfig.File.length > 0) {
    const url = '/WebAssets/' + props.config.LegendConfig.File
    const width = props.config.LegendConfig.Width
    const height = props.config.LegendConfig.Height

    // fetch the legend image:
    const response = await fetchUrl(url)
    if (response.ok) {
      const blob = await response.blob()
      const imageUrl = URL.createObjectURL(blob)
      const img = document.createElement('img')
      img.src = imageUrl
      img.width = width
      img.height = height
      img.style.position = 'absolute'
      img.style.bottom = '10px'
      img.style.left = '10px'
      img.style.zIndex = '6'
      map.value!.getContainer().appendChild(img)
    }
  }

  // Add play/pause control
  initPlayPauseControl()
}

const createLayer = (layer: NamedLayerType): GeoLayer => {
  if (layer.Type === 'GeoJson' || layer.Type === undefined) {
    return L.geoJSON(null, {
      style: getFeatureStyle,
      onEachFeature: onEachFeature,
      pointToLayer: pointToLayer,
    })
  } else if (layer.Type === 'GeoTiff') {
    // Create a LayerGroup to hold the GeoTiff layer
    return L.layerGroup()
  } else {
    throw new Error('Unknown layer type: ' + layer.Type)
  }
}

const onLayerAdd = (e: L.LayerEvent): void => {
  // Find the layer name
  let layerName: string | null = null
  for (const [name, layer] of Object.entries(mainLayers.value)) {
    if (layer === e.layer) {
      layerName = name
      break
    }
  }
  if (!layerName) {
    for (const [name, layer] of Object.entries(optionalLayers.value)) {
      if (layer === e.layer) {
        layerName = name
        break
      }
    }
  }

  if (layerName) {
    // Set any variable values from the layer
    if (layersWithSetVariableValues.value.has(layerName)) {
      const variableValues = layersWithSetVariableValues.value.get(layerName)!
      props.setConfigVariableValues?.(variableValues)
    }

    if (layersWithSetWidgetTitleVarValues.value.has(layerName)) {
      const variableValues = layersWithSetWidgetTitleVarValues.value.get(layerName)!
      props.setWidgetTitleVarValues?.(variableValues)
    }

    // Start animation if this layer has multiple frames
    const hasGeoContentFrames = geoContentFrames.value.has(layerName) && geoContentFrames.value.get(layerName)!.length > 1
    if (hasGeoContentFrames) {
      startLayerAnimation(layerName)
    }
  }
}

const onLayerRemove = (e: L.LayerEvent): void => {
  // Find the layer name
  let layerName: string | null = null
  for (const [name, layer] of Object.entries(mainLayers.value)) {
    if (layer === e.layer) {
      layerName = name
      break
    }
  }
  if (!layerName) {
    for (const [name, layer] of Object.entries(optionalLayers.value)) {
      if (layer === e.layer) {
        layerName = name
        break
      }
    }
  }

  if (layerName) {
    // Stop animation for this layer
    stopLayerAnimation(layerName)
  }
}

const loadLayers = async (layers?: NamedLayerType[]): Promise<void> => {
  if (!layers) {
    layers = props.config.MainLayers.concat(props.config.OptionalLayers)
  }

  for (const layer of layers) {
    await loadLayerContent(layer)
    const obj = layer.Variable.Object
    const VariableReplacer = (window.parent as any)['dashboardApp']?.variableReplacer || {
      replaceVariables: (str: string, vars: Record<string, string>) => str,
    }
    stringWithVarResolvedMap.value.set(obj, VariableReplacer.replaceVariables(obj, props.configVariables?.VarValues))
  }
}

const getFeatureStyle = (feature?: Feature): L.PathOptions => {
  if (feature?.properties?.style) {
    return feature.properties.style as L.PathOptions
  }
  return {} as L.PathOptions
}

const pointToLayer = (feature: Feature, latlng: L.LatLng): L.Layer => {
  if (feature?.properties?.markerIcon) {
    const url = feature.properties.markerIcon.url
    const backend: string = (window.parent as any)['dashboardApp']?.getBackendUrl() || ''
    const fullUrl = backend + url
    const width = feature.properties.markerIcon.width
    const height = feature.properties.markerIcon.height
    const anchorX = feature.properties.markerIcon.anchorX
    const anchorY = feature.properties.markerIcon.anchorY
    const theIcon = L.icon({
      iconUrl: fullUrl,
      iconSize: [width, height],
      iconAnchor: [anchorX, anchorY],
    })
    return L.marker(latlng, {
      icon: theIcon,
    })
  }
  return L.marker(latlng)
}

const onEachFeature = (feature: Feature, layer: L.Layer): void => {
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
      className: 'geomap-label',
    }
    layer.bindTooltip(text, options)
    if (label.style && typeof label.style === 'object' && label.style !== null) {
      layer.on('tooltipopen', (event: L.LeafletEvent) => {
        const tooltip = (event as L.LeafletEvent & { tooltip?: L.Tooltip }).tooltip
        const tooltipElement = tooltip?.getElement()
        if (tooltipElement) {
          for (const [key, value] of Object.entries(label.style)) {
            tooltipElement.style.setProperty(key, String(value))
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
    } else if (typeof tooltip === 'object') {
      tooltip = Object.entries(tooltip)
        .map(([key, value]) => `<strong>${key}:</strong> ${value}`)
        .join('<br>')
    } else {
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

  // Handle htmlDialog property - takes precedence over popup
  if (feature?.properties?.htmlDialog) {
    const htmlDialogData = feature.properties.htmlDialog
    layer.on('click', () => {
      if (htmlDialog.value) {
        htmlDialog.value.open(htmlDialogData)
      }
    })
  } else if (feature?.properties?.popup) {
    let popup = feature.properties.popup
    if (typeof popup === 'string') {
      // replace newlines with <br>:
      popup = popup.replace(/(?:\r\n|\r|\n)/g, '<br>')
    } else if (typeof popup === 'object') {
      popup = Object.entries(popup)
        .map(([key, value]) => `<strong>${key}:</strong> ${value}`)
        .join('<br>')
    } else {
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
    layer.on('click', () => {
      props.setConfigVariableValues?.(setVariableValues)
    })
  }

  if (feature?.properties?.style?.fillOpacity) {
    const stdOpacity = feature.properties.style.opacity || 1.0
    const stdFillOpacity = feature.properties.style.fillOpacity
    const delta = props.config.MapConfig.MouseOverOpacityDelta || 0.3
    const mouseOverFillOpacity = Math.min(1.0, stdFillOpacity + delta)
    const mouseOverOpacity = Math.min(1.0, stdOpacity + delta)

    layer.on({
      mouseover: (event: L.LeafletEvent) => {
        const target = event.target as L.Path
        target.setStyle({
          fillOpacity: mouseOverFillOpacity,
          opacity: mouseOverOpacity,
        })
      },
      mouseout: (event: L.LeafletEvent) => {
        const target = event.target as L.Path
        target.setStyle({
          fillOpacity: stdFillOpacity,
          opacity: stdOpacity,
        })
      },
    })
  }
}

const loadGeoRasterModules = async (): Promise<void> => {
  // Check if already loaded
  if (typeof parseGeoraster !== 'undefined' && typeof GeoRasterLayer !== 'undefined') {
    return
  }

  // Dynamic loading utility
  const loadScript = (src: string): Promise<void> => {
    return new Promise((resolve, reject) => {
      if (document.querySelector(`script[src="${src}"]`)) {
        resolve()
        return
      }

      const script = document.createElement('script')
      script.src = src
      script.onload = () => resolve()
      script.onerror = () => reject(new Error(`Failed to load ${src}`))
      document.head.appendChild(script)
    })
  }

  // Load both libraries
  await Promise.all([
    loadScript('/ViewBundle_Generic/js/georaster.browser.bundle.js'),
    loadScript('/ViewBundle_Generic/js/georaster-layer-for-leaflet.min.js'),
  ])

  // Wait for globals to be available
  while (typeof parseGeoraster === 'undefined' || typeof GeoRasterLayer === 'undefined') {
    await new Promise((resolve) => setTimeout(resolve, 10))
  }
}

const loadStaticLayer = async (staticLayer: StaticLayer): Promise<GeoLayer | null> => {
  const url = '/WebAssets/' + staticLayer.FileName
  const extension = staticLayer.FileName.toLowerCase()

  try {
    if (extension.endsWith('.geojson') || extension.endsWith('.json')) {
      // Load as GeoJSON
      const response = await fetchUrl(url)
      if (!response.ok) {
        throw new Error(`Failed to load static GeoJSON: ${response.statusText}`)
      }
      const geoJsonData = await response.json()
      return L.geoJSON(geoJsonData, {
        style: getFeatureStyle,
        onEachFeature: onEachFeature,
        pointToLayer: pointToLayer,
      })
    } else if (extension.endsWith('.tif') || extension.endsWith('.tiff')) {
      // Load as GeoTiff
      await loadGeoRasterModules()
      const response = await fetchUrl(url)
      if (!response.ok) {
        throw new Error(`Failed to load static GeoTiff: ${response.statusText}`)
      }
      const arrayBuffer = await response.arrayBuffer()
      const georaster = await parseGeoraster(arrayBuffer)

      const geoRasterLayer = new GeoRasterLayer({
        georaster,
        opacity: 0.9,
        zIndex: 3, // Between tiles (1-2) and main/optional layers (4+)
        resolution: props.config.MapConfig.GeoTiffResolution,
      })

      return L.layerGroup([geoRasterLayer])
    } else {
      console.warn(`Unsupported static layer file type: ${staticLayer.FileName}`)
      return null
    }
  } catch (error: any) {
    console.error(`Failed to load static layer ${staticLayer.Name}: ${error.message}`)
    return null
  }
}

const loadLayerContent = async (layerObj: NamedLayerType): Promise<void> => {
  const layerName = layerObj.Name
  const layer: GeoLayer = mainLayers.value[layerName] || optionalLayers.value[layerName]
  const variable: fast.VariableRef = layerObj.Variable
  const layerType: GeoLayerType = layerObj.Type
  const frameCount = layerObj.FrameCount || 1

  if (!layer) {
    console.error(`Layer ${layerName} not found in map layers`)
    return
  }

  stopLayerAnimation(layerName)

  if (activeRequests.value.has(layerName)) {
    activeRequests.value.get(layerName)!.abort()
    activeRequests.value.delete(layerName)
  }

  const abortController = new AbortController()
  activeRequests.value.set(layerName, abortController)

  try {
    geoContentFrames.value.delete(layerName)

    const dataArray: GeoJsonObj[] | GeoJsonUrl[] | GeoTiffUrl[] = await props.backendAsync!('GetGeoData', {
      variable: variable,
      timeRange: props.timeRange,
      frameCount: frameCount,
      configVars: props.configVariables?.VarValues,
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

    if (firstItem.type === 'GeoTiffUrl') {
      // Handle GeoTiff URL data
      if (layerType === 'GeoJson') {
        throw new Error('Expected GeoJson data, but got GeoTiff data')
      }

      // Load georaster modules only when needed for GeoTiff
      await loadGeoRasterModules()

      const frames: GeoTiffFrame[] = []

      for (const geoTiffUrl of dataArray as GeoTiffUrl[]) {
        if (abortController.signal.aborted) {
          return
        }

        try {
          const fetchStartTime = performance.now()
          const response = await fetchUrl(geoTiffUrl.url, {
            signal: abortController.signal,
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
            setVariableValues: geoTiffUrl.setVariableValues,
            setWidgetTitleVarValues: geoTiffUrl.setWidgetTitleVarValues,
          })

          console.info(
            `Loaded GeoTiff frame: fetched in ${fetchEndTime - fetchStartTime}ms, arrayBuffer in ${arrayBufferEndTime - arrayBufferStartTime}ms, parsed in ${parseEndTime - parseStartTime}ms`,
          )
        } catch (error: any) {
          console.error(`Failed to load GeoTiff frame: ${error.message}`)
        }
      }

      if (frames.length === 0) {
        throw new Error(`No valid GeoTiff frames loaded for layer ${layerName}`)
      }

      geoContentFrames.value.set(layerName, frames)
      setGeoTiffLayerContent(layer as L.LayerGroup, frames[0])
      setFrameVariables(frames[0], layerName, layer)

      if (frames.length > 1 && map.value!.hasLayer(layer)) {
        startLayerAnimation(layerName)
      }
    } else if (firstItem.type === 'GeoJsonUrl') {
      // Handle GeoJSON URL data
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
          const response = await fetchUrl(geoJsonUrl.url, {
            signal: abortController.signal,
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
            setVariableValues: geoJsonUrl.setVariableValues,
            setWidgetTitleVarValues: geoJsonUrl.setWidgetTitleVarValues,
          })

          console.info(`Loaded GeoJSON frame: fetched in ${fetchEndTime - fetchStartTime}ms, parsed in ${jsonEndTime - jsonStartTime}ms`)
        } catch (error: any) {
          console.error(`Failed to load GeoJSON frame: ${error.message}`)
        }
      }

      if (frames.length === 0) {
        throw new Error(`No valid GeoJSON frames loaded for layer ${layerName}`)
      }

      geoContentFrames.value.set(layerName, frames)
      setGeoJsonLayerContent(layer as L.GeoJSON, frames[0])
      setFrameVariables(frames[0], layerName, layer)

      // Start animation if there are multiple frames
      if (frames.length > 1 && map.value!.hasLayer(layer)) {
        startLayerAnimation(layerName)
      }
    } else {
      // Handle direct GeoJSON data
      if (layerType === 'GeoTiff') {
        throw new Error('Expected GeoTiff data, but got GeoJson data')
      }

      const frames: GeoJsonFrame[] = dataArray.map((data) => ({
        data: data as GeoJsonObject,
        setVariableValues: (data as GeoJsonObj).setVariableValues,
        setWidgetTitleVarValues: (data as GeoJsonObj).setWidgetTitleVarValues,
      }))

      geoContentFrames.value.set(layerName, frames)
      setGeoJsonLayerContent(layer as L.GeoJSON, frames[0])
      setFrameVariables(frames[0], layerName, layer)

      // Start animation if there are multiple frames
      if (frames.length > 1 && map.value!.hasLayer(layer)) {
        startLayerAnimation(layerName)
      }
    }
  } catch (err: any) {
    // Don't show errors for aborted requests
    if (err.name === 'AbortError') {
      console.info(`Request for layer ${layerName} was aborted via exception`)
    } else {
      console.error(err.message)
      layer.clearLayers()
    }
  } finally {
    // Clean up if this is still the active request
    if (activeRequests.value.get(layerName) === abortController) {
      activeRequests.value.delete(layerName)
    }
  }
}

const setFrameVariables = (frame: GeoContentFrame, layerName: string, layer: GeoLayer): void => {
  if (frame.setVariableValues) {
    layersWithSetVariableValues.value.set(layerName, frame.setVariableValues)
    if (map.value!.hasLayer(layer)) {
      props.setConfigVariableValues?.(frame.setVariableValues)
    }
  }

  if (frame.setWidgetTitleVarValues) {
    layersWithSetWidgetTitleVarValues.value.set(layerName, frame.setWidgetTitleVarValues)
    if (map.value!.hasLayer(layer)) {
      props.setWidgetTitleVarValues?.(frame.setWidgetTitleVarValues)
    }
  }
}

const startLayerAnimation = (layerName: string): void => {
  // Clear any existing animation
  stopLayerAnimation(layerName)

  const layer = mainLayers.value[layerName] || optionalLayers.value[layerName]
  if (!layer) return

  const isGeoJson = layer instanceof L.GeoJSON
  const frames = geoContentFrames.value.get(layerName)

  if (!frames || frames.length <= 1) return

  // Create animation controller
  const controller: AnimationController = {
    isRunning: true,
    currentIndex: 0,
    lastFrameTime: 0,
    isPaused: globalPaused.value,
  }
  animationControllers.value.set(layerName, controller)

  animateLayer(layerName, layer, frames, isGeoJson, controller)

  if (!playPauseControl.value && hasAnimatableLayersFunc()) {
    initPlayPauseControl()
  }
}

const animateLayer = (layerName: string, layer: GeoLayer, frames: GeoContentFrame[], isGeoJson: boolean, controller: AnimationController): void => {
  const frameInterval = props.config.MapConfig.FrameDelay

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
          setGeoJsonLayerContent(layer as L.GeoJSON, frame as GeoJsonFrame)
        } else {
          setGeoTiffLayerContent(layer as L.LayerGroup, frame as GeoTiffFrame)
        }

        setFrameVariables(frame, layerName, layer)
      } catch (error) {
        console.error(`Error updating frame ${controller.currentIndex} for layer ${layerName}:`, error)
      }

      // Move to next frame
      controller.currentIndex = (controller.currentIndex + 1) % frames.length

      // Handle end of loop pause if needed
      if (controller.currentIndex === 0 && props.config.MapConfig.EndOfLoopPause > 0) {
        controller.isPaused = true
        setTimeout(() => {
          if (controller.isRunning) {
            // Only resume if we're not globally paused
            controller.isPaused = globalPaused.value
            controller.lastFrameTime = 0 // Reset timing
          }
        }, props.config.MapConfig.EndOfLoopPause)
      }
    }
  }

  requestAnimationFrame(animate)
}

const stopLayerAnimation = (layerName: string): void => {
  if (animationControllers.value.has(layerName)) {
    animationControllers.value.get(layerName)!.isRunning = false
    animationControllers.value.delete(layerName)
  }
}

const setGeoTiffLayerContent = (layerGroup: L.LayerGroup, frame: GeoTiffFrame): void => {
  const options: GeoRasterLayerOptions = {
    georaster: frame.georaster,
    opacity: frame.opacity ?? 0.9,
    zIndex: 5, // Main/optional layers above static layers (z-index 3)
    pixelValuesToColorFn: frame.colorMap,
    resolution: props.config.MapConfig.GeoTiffResolution,
  }

  // GeoRasterLayer should be loaded at this point
  if (!GeoRasterLayer) {
    throw new Error('GeoRasterLayer not loaded')
  }

  const nextLayer: L.GridLayer = new GeoRasterLayer(options)

  // Get current layers before adding the new one
  const currentLayers = layerGroup.getLayers() as L.Layer[]

  // Add the new layer to the group
  layerGroup.addLayer(nextLayer)

  if (currentLayers.length > 0) {
    if (currentLayers.length > 1) {
      // Clean up all old layers except the most recent one
      for (let i = 0; i < currentLayers.length - 1; i++) {
        layerGroup.removeLayer(currentLayers[i])
      }
    }

    const currentLayer = currentLayers[currentLayers.length - 1] as L.GridLayer

    // GeoRasterLayer emits 'load' after the internal canvas is drawn
    nextLayer.once('load', () => {
      // Fade out the old layer while keeping the new one at full opacity
      crossFade(layerGroup, currentLayer, 0, 200, () => {
        layerGroup.removeLayer(currentLayer)
      })
    })
  }
}

const crossFade = (layerGroup: L.LayerGroup, layer: L.GridLayer, targetOpacity: number, duration: number, done?: () => void): void => {
  const options = layer.options as L.GridLayerOptions

  const start = performance.now()
  const initial = options?.opacity ?? 1.0
  const delta = targetOpacity - initial

  const tick = (now: DOMHighResTimeStamp) => {
    const p = Math.min(1, (now - start) / duration)
    layer.setOpacity(initial + delta * p)
    if (p < 1) {
      requestAnimationFrame(tick)
    } else if (done) {
      done()
    }
  }

  requestAnimationFrame(tick)
}

const setGeoJsonLayerContent = (layer: L.GeoJSON, frame: GeoJsonFrame): void => {
  layer.clearLayers()
  layer.addData(frame.data)
}

const onContextMenu = (e: MouseEvent): void => {
  if (canUpdateConfig.value) {
    e.preventDefault()
    e.stopPropagation()
    contextMenu.value.show = false
    contextMenu.value.clientX = e.clientX
    contextMenu.value.clientY = e.clientY
    nextTick(() => {
      contextMenu.value.show = true
    })
  }
}

const onConfigureMap = async (): Promise<void> => {
  const dlg = dlgMapConfig.value
  if (dlg) {
    const ok = await dlg.showDialog()
    if (ok) {
      await initMap()
    }
  }
}

const onConfigureLayers = async (): Promise<void> => {
  const dlg = dlgLayersConfig.value
  if (dlg) {
    const ok = await dlg.showDialog()
    if (ok) {
      await initMap()
    }
  }
}

// Watchers
watch(
  () => props.configVariables?.VarValues,
  (newVal, oldVal) => {
    const VariableReplacer = (window.parent as any)['dashboardApp']?.variableReplacer || {
      replaceVariables: (str: string, vars: Record<string, string>) => str,
    }
    const resolvedCenterNew = VariableReplacer.replaceVariables(props.config.MapConfig.Center, props.configVariables?.VarValues)
    if (resolvedCenter.value !== resolvedCenterNew) {
      map.value?.panTo(getResolvedCenter())
    }

    const resolveMap = stringWithVarResolvedMap.value

    const mainLayersWithVariables = props.config.MainLayers.filter((mainLayer) => {
      const obj = mainLayer.Variable.Object
      const objResolved = VariableReplacer.replaceVariables(obj, props.configVariables?.VarValues)
      return !resolveMap.has(obj) || resolveMap.get(obj) !== objResolved
    })

    const optionalLayersWithVariables = props.config.OptionalLayers.filter((optionalLayer) => {
      const obj = optionalLayer.Variable.Object
      const objResolved = VariableReplacer.replaceVariables(obj, props.configVariables?.VarValues)
      return !resolveMap.has(obj) || resolveMap.get(obj) !== objResolved
    })

    if (mainLayersWithVariables.length > 0 || optionalLayersWithVariables.length > 0) {
      loadLayers(mainLayersWithVariables.concat(optionalLayersWithVariables))
    }
  },
  { deep: true },
)

watch(
  () => props.timeRange,
  () => {
    // Stop all animations before reloading layers
    stopAllAnimations()
    loadLayers()
  },
)

watch(
  () => props.eventPayload,
  () => {
    if (props.eventName === 'OnVarChanged') {
      const obj = (props.eventPayload as any)['Object'] as string
      const name = (props.eventPayload as any)['Name'] as string
      for (const mainLayer of props.config.MainLayers) {
        if (mainLayer.Variable.Object === obj && mainLayer.Variable.Name === name) {
          loadLayerContent(mainLayer)
        }
      }
      for (const optionalLayer of props.config.OptionalLayers) {
        if (optionalLayer.Variable.Object === obj && optionalLayer.Variable.Name === name) {
          loadLayerContent(optionalLayer)
        }
      }
    }
  },
)

// Lifecycle
onMounted(() => {
  canUpdateConfig.value = (window.parent as any)['dashboardApp']?.canUpdateViewConfig() ?? false
  initMap()
})

onBeforeUnmount(() => {
  activeRequests.value.forEach((controller) => {
    controller.abort()
  })
  activeRequests.value.clear()

  stopAllAnimations()
  clearMap()
})
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
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.4);
}

.leaflet-control-custom a {
  background-color: #fff;
  color: #444;
  cursor: pointer;
}

.leaflet-control-custom a:hover {
  background-color: #f4f4f4;
}
</style>
