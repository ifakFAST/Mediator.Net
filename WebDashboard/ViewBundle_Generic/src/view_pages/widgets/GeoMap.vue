<template>
  <div>

    <div :id="theID" v-bind:style="{ height: theHeight }" style="min-height: 100px;" @contextmenu="onContextMenu">

    </div>

    <v-menu v-model="contextMenu.show" :position-x="contextMenu.clientX" :position-y="contextMenu.clientY" absolute offset-y>
      <v-list>
        <v-list-item @click="onConfigure" >
          <v-list-item-title>Configure...</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <GeoMapConfigDlg ref="dlgConfig" :configuration="config" :backendAsync="backendAsync" />

  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import { TimeRange } from '../../utils'

import L from '../../assets/leaflet.js'
import '../../assets/leaflet.groupedlayercontrol.js'
import type { Feature, FeatureCollection } from 'geojson'
import { GeoMapConfig } from './GeoMapConfigTypes'
import * as fast from '../../fast_types'
import GeoMapConfigDlg from './GeoMapConfigDlg.vue'

let dgUUID = 0

@Component({
  components: {
    GeoMapConfigDlg,
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

  uid = dgUUID.toString()
  map: L.Map = null

  showConfigDialog = false

  baseMaps: {[key: string]: L.Layer} = { }
  mainLayers: {[key: string]: L.GeoJSON} = { }
  optionalLayers: {[key: string]: L.GeoJSON} = { }
  
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
    this.clearMap()
  }

  clearMap(): void {
    if (this.map) {
      this.map.remove()
      this.map = null
    }
  }

  async initMap(): Promise<void> {

    this.clearMap()

    const config: GeoMapConfig = this.config
   
    const mapOptions: L.MapOptions = {
      center: [52.38671, 9.75749],
      zoom: config.MapConfig.ZoomDefault,
      zoomControl: true,
      zoomSnap: 0.5,
      zoomDelta: 0.5
    }

    this.map = L.map(this.theID, mapOptions)

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
      const layer = L.geoJSON(null, { 
        style: this.getFeatureStyle,
        onEachFeature: this.onEachFeature 
      })
      this.mainLayers[mainLayer.Name] = layer
      if (isFirstMainLayer) {
        layer.addTo(this.map)
        isFirstMainLayer = false
      }
    }

    this.optionalLayers = { }
    for (const optionalLayer of config.OptionalLayers) {
      const layer = L.geoJSON(null, { 
        style: this.getFeatureStyle,
        onEachFeature: this.onEachFeature 
      })
      this.optionalLayers[optionalLayer.Name] = layer
      if (optionalLayer.IsSelected) {
        layer.addTo(this.map)
      }
    }

    const MainLabel = config.MapConfig.MainGroupLabel
    const OptionalLabel = config.MapConfig.OptionalGroupLabel

    const groupedOverlays = {}
    groupedOverlays[MainLabel] = this.mainLayers
    groupedOverlays[OptionalLabel] = this.optionalLayers

    const hasSeveralMainLayers = Object.keys(this.mainLayers).length > 1
    if (!hasSeveralMainLayers) {
      delete groupedOverlays[MainLabel]
    }

    const options: L.ControlOptions = {
      exclusiveGroups: hasSeveralMainLayers ? [MainLabel] : [],
      groupCheckboxes: false
    }

    const control = L.control.groupedLayers(
      this.baseMaps, 
      groupedOverlays, 
      options)

    control.addTo(this.map)
    
    for (const mainLayer of config.MainLayers) {
      await this.loadGeoJson(this.mainLayers[mainLayer.Name], mainLayer.Variable)
    }

    for (const optionalLayer of config.OptionalLayers) {
      await this.loadGeoJson(this.optionalLayers[optionalLayer.Name], optionalLayer.Variable)
    }
  }

  getFeatureStyle(feature?: Feature): L.PathOptions  {
    if (feature && feature.properties && feature.properties.style) {
      return feature.properties.style
    }
    return {
    }
  }

  onEachFeature(feature: Feature, layer: L.Layer): void {
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
        autoClose: false,
        closeOnClick: false,
        //className: 'geo-feature-popup'
      }

      layer.bindPopup(popup, options)
    }

    if (feature?.properties?.setVariableValues) {
      const setVariableValues: Record<string, string> = feature.properties.setVariableValues
      layer.on('click', (e: L.LeafletMouseEvent) => {        
        const para = {
          variableValues: setVariableValues
        }
        window.parent['dashboardApp'].sendViewRequest('SetConfigVariableValues', para, (strResponse) => {})
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

  async loadGeoJson(layer: L.GeoJSON, variable: fast.VariableRef): Promise<void> {
    try {
      const data: FeatureCollection = await this.backendAsync('GetGeoJson', { variable: variable })
      layer.clearLayers()
      layer.addData(data)
    } 
    catch (err) {
      console.error(err)
    }
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

  async onConfigure(): Promise<void> {
    const dlg = this.$refs.dlgConfig as GeoMapConfigDlg
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
        const valueStr = this.eventPayload['Value'] as string
        const value: FeatureCollection = JSON.parse(valueStr)
      
        for (const mainLayer of this.config.MainLayers) {
          if (mainLayer.Variable.Object === obj && mainLayer.Variable.Name === name) {
            this.mainLayers[mainLayer.Name].clearLayers()
            this.mainLayers[mainLayer.Name].addData(value)
          }
        }

        for (const optionalLayer of this.config.OptionalLayers) {
          if (optionalLayer.Variable.Object === obj && optionalLayer.Variable.Name === name) {
            this.optionalLayers[optionalLayer.Name].clearLayers()
            this.optionalLayers[optionalLayer.Name].addData(value)
          }
        }
    }
  }
}

</script>

<style src="../../assets/leaflet.css"></style>
<style src="../../assets/leaflet.groupedlayercontrol.css"></style>

<style>

</style>
