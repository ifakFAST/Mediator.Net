<template>

  <div>

    <div @contextmenu="onContextMenu">
      <dy-graph ref="theGraph"
                :graph-data="historyData"
                :graph-options="options"
                :graph-style="graphStyle"
                :graph-reset-zoom="zoomResetTime"></dy-graph>
    </div>

    <v-menu v-model="contextMenu.show" :position-x="contextMenu.clientX" :position-y="contextMenu.clientY" absolute offset-y>
      <v-list>
        <v-list-item @click="onConfigurePlotItems" >
          <v-list-item-title>Configure Plot Items...</v-list-item-title>
        </v-list-item>
        <v-list-item @click="onConfigurePlot" >
          <v-list-item-title>Configure Plot...</v-list-item-title>
        </v-list-item>
        <v-list-item @click="downloadCSV">
          <v-list-item-title>Download CSV File</v-list-item-title>
        </v-list-item>
        <v-list-item @click="downloadSpreadsheet">
            <v-list-item-title>Download Spreadsheet File</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <v-dialog v-model="editorItems.show" persistent max-width="1100px" @keydown="(e) => { if (e.keyCode === 27 && !selectObject.show) { editorItems.show = false; }}">
      <v-card>
          <v-card-title>
            <span class="headline">Configure Plot Items</span>
          </v-card-title>
          <v-card-text>
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th class="text-center">Color</th>
                  <th>&nbsp;</th>
                  <th>Size</th>
                  <th>Type</th>
                  <th>Axis</th>
                  <th>Check</th>
                  <th>Object Name</th>
                  <th>&nbsp;</th>
                  <th>Variable</th>
                  <th>&nbsp;</th>
                  <th>&nbsp;</th>
                </tr>
              </thead>
              <template v-for="(item, idx) in editorItems.items">
                <tr v-bind:key="idx">
                  <td><v-text-field class="tabcontent" v-model="item.Name"></v-text-field></td>
                  <td>
                    <v-menu offset-y>
                      <template v-slot:activator="{ on }">
                        <v-btn v-on="on" v-bind:style="{ backgroundColor: item.Color }" class="ml-2 mr-2" style="min-width:32px;width:32px;height:32px"></v-btn>
                      </template>
                      <div>
                        <div v-for="(color, index) in editorItems.colorList" :key="index" @click="item.Color = color">
                          <div style="padding:6px; cursor: pointer;" v-bind:style="{ backgroundColor: color }">{{color}}</div>
                        </div>
                      </div>
                    </v-menu>
                  </td>
                  <td><v-text-field class="tabcontent" v-model="item.Color"      style="width:9ch;"></v-text-field></td>
                  <td><v-text-field class="tabcontent" v-model="item.Size"       style="margin-left: 1ex; width:5ch;"></v-text-field></td>
                  <td><v-select     class="tabcontent" v-model="item.SeriesType" style="margin-left: 1ex; width:10ch;" :items="['Scatter', 'Line']"></v-select></td>
                  <td><v-select     class="tabcontent" v-model="item.Axis"       style="margin-left: 1ex; width:8ch;" :items="['Left', 'Right']"></v-select></td>
                  <td><v-checkbox   class="tabcontent" v-model="item.Checked"    style="margin-left: 1ex; margin-right: 1ex;"></v-checkbox></td>
                  <td style="font-size:16px; max-width:17ch; word-wrap:break-word;">{{editorItems_ObjectID2Name(item.Variable.Object)}}</td>
                  <td><v-btn class="ml-2 mr-4" style="min-width:36px;width:36px;" @click="editorItems_SelectObj(item)"><v-icon>edit</v-icon></v-btn></td>
                  <td><v-select     class="tabcontent" :items="editorItems_ObjectID2Variables(item.Variable.Object)" style="width:12ch;" v-model="item.Variable.Name"></v-select></td>
                  <td><v-btn class="ml-2 mr-2" style="min-width:36px;width:36px;" @click="editorItems_DeleteItem(idx)"><v-icon>delete</v-icon></v-btn></td>
                  <td><v-btn class="ml-2 mr-2" style="min-width:36px;width:36px;" v-if="idx > 0" @click="editorItems_MoveUpItem(idx)"><v-icon>keyboard_arrow_up</v-icon></v-btn></td>
                </tr>
              </template>
              <tr>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
                <td>&nbsp;</td>
                <td><v-btn class="ml-2 mr-2" style="min-width:36px;width:36px;" @click="editorItems_AddItem"><v-icon>add</v-icon></v-btn></td>
                <td>&nbsp;</td>
              </tr>
            </table>
          </v-card-text>
          <v-card-actions>
            <v-spacer></v-spacer>
            <v-btn color="grey darken-1"  text @click.native="editorItems.show = false">Cancel</v-btn>
            <v-btn color="primary darken-1" text :disabled="!isItemsOK" @click.native="editorItems_Save">Save</v-btn>
          </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="editorPlot.show" persistent max-width="400px" @keydown="(e) => { if (e.keyCode === 27) { editorPlot.show = false; }}">
      <v-card>
        <v-card-title>
          <span class="headline">Configure Plot</span>
        </v-card-title>
        <v-card-text>
          <table style="width:100%;">
            <tr>
              <td><v-text-field v-model="editorPlot.plot.LeftAxisName"  label="Left Axis Caption" ></v-text-field></td>
            </tr>
            <tr>
              <td><v-checkbox v-model="editorPlot.plot.LeftAxisStartFromZero" label="Left Y Axis: Start From Zero"></v-checkbox></td>
            </tr>
            <tr>
              <td><text-field-nullable-number v-model="editorPlot.plot.LeftAxisLimitY" label="Left Y Axis: Limit Y Value" ></text-field-nullable-number></td>
            </tr>
            <tr>
              <td><v-text-field v-model="editorPlot.plot.RightAxisName" label="Right Axis Caption" ></v-text-field></td>
            </tr>
            <tr>
              <td><v-checkbox v-model="editorPlot.plot.RightAxisStartFromZero" label="Right Y Axis: Start From Zero"></v-checkbox></td>
            </tr>
            <tr>
              <td><text-field-nullable-number v-model="editorPlot.plot.RightAxisLimitY" label="Right Y Axis: Limit Y Value" ></text-field-nullable-number></td>
            </tr>
            <tr>
              <td><v-text-field v-model="editorPlot.plot.MaxDataPoints" label="Max DataPoints"    ></v-text-field></td>
            </tr>
            <tr>
              <td><v-select v-model="editorPlot.plot.FilterByQuality" label="QualityFilter" :items="qualityFilterValues"></v-select></td>
            </tr>
          </table>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn color="grey darken-1"  text @click.native="editorPlot.show = false">Cancel</v-btn>
          <v-btn color="primary darken-1" text @click.native="editorPlot_Save">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <dlg-object-select v-model="selectObject.show"
        :object-id="selectObject.selectedObjectID"
        :module-id="selectObject.selectedModuleID"
        :modules="selectObject.modules"
        @onselected="selectObject_OK"></dlg-object-select>

  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import DyGraph from '../../components/DyGraph.vue'
import DlgObjectSelect from '../../components/DlgObjectSelect.vue'
import { TimeRange, timeWindowFromTimeRange } from '../../utils'
import TextFieldNullableNumber from '../../components/TextFieldNullableNumber.vue'
import { ModuleInfo, ObjectMap, Obj, Variable, SelectObject, ObjInfo } from './common'

/////////////////////////////////////////////////////////////////////////

interface Config {
  PlotConfig: PlotConfig
  Items: ItemConfig[]
  DataExport: DataExport
}

interface PlotConfig {
  MaxDataPoints: number
  FilterByQuality: QualityFilter
  LeftAxisName: string
  LeftAxisStartFromZero: boolean
  LeftAxisLimitY?: number | null
  RightAxisName: string
  RightAxisStartFromZero: boolean
  RightAxisLimitY?: number | null
}

type QualityFilter = 'ExcludeNone' | 'ExcludeBad' | 'ExcludeNonGood'

const QualityFilterValues: QualityFilter[] = ['ExcludeNone', 'ExcludeBad', 'ExcludeNonGood']

interface ItemConfig {
  Name: string
  Color: string
  Size: number
  SeriesType: SeriesType
  Axis: Axis
  Checked: boolean
  Variable: Variable
}

type SeriesType = 'Line' | 'Scatter'

type Axis = 'Left' | 'Right'

interface DataExport {
  CSV: CsvDataExport
  Spreadsheet: SpreadsheetDataExport
}

interface CsvDataExport {
  TimestampFormat: string
  ColumnSeparator: string
}

interface SpreadsheetDataExport {
  TimestampFormat: string
}

/////////////////////////////////////////////////////////////////////////

interface EditorPlot {
  show: boolean
  plot: PlotConfig
}

interface EditorItems {
  show: boolean
  items: ItemConfig[]
  colorList: string[]
}

@Component({
  components: {
    DyGraph,
    DlgObjectSelect,
    TextFieldNullableNumber,
  },
})
export default class HistoryPlot extends Vue {

  @Prop({ default() { return '' } }) id: string
  @Prop({ default() { return '' } }) width: string
  @Prop({ default() { return '' } }) height: string
  @Prop({ default() { return {} } }) config: Config
  @Prop() backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  @Prop({ default() { return '' } }) eventName: string
  @Prop({ default() { return {} } }) eventPayload: object
  @Prop({ default() { return {} } }) timeRange: TimeRange
  @Prop({ default() { return 0 } }) resize: number
  @Prop({ default() { return null } }) dateWindow: number[]

  qualityFilterValues = QualityFilterValues

  contextMenu = {
    show: false,
    clientX: 0,
    clientY: 0,
  }

  historyData: any[][] = []

  zoomResetTime = 0

  objectMap: ObjectMap = {}

  editorItems: EditorItems = {
    show: false,
    items: [],
    colorList: [
      '#1BA1E2',
      '#A05000',
      '#339933',
      '#A2C139',
      '#D80073',
      '#F09609',
      '#E671B8',
      '#A200FF',
      '#E51400',
      '#00ABA9',
      '#000000',
      '#CCCCCC',
    ],
  }

  editorPlot: EditorPlot = {
    show: false,
    plot: {
      MaxDataPoints: 100,
      FilterByQuality: 'ExcludeBad',
      LeftAxisName: '',
      LeftAxisStartFromZero: true,
      LeftAxisLimitY: null,
      RightAxisName: '',
      RightAxisStartFromZero: true,
      RightAxisLimitY: null,
    },
  }

  selectObject: SelectObject = {
    show: false,
    modules: [],
    selectedModuleID: '',
    selectedObjectID: '',
    variable: { Object: '', Name: '' },
  }

  get variables(): Variable[] {
    return this.items.map((it) => it.Variable)
  }

  get items(): ItemConfig[] {
    return this.config.Items ?? []
  }

  get plotConfig(): PlotConfig {
    return this.config.PlotConfig ?? {
      MaxDataPoints: 1000,
      FilterByQuality: 'ExcludeBad',
      LeftAxisName: '',
      LeftAxisLimitY: null,
      RightAxisName: '',
      LeftAxisStartFromZero: true,
      RightAxisStartFromZero: true,
      RightAxisLimitY: null,
    }
  }

  get graphStyle(): object {
    const width = this.width === '' ? '100%' : this.width
    const height = this.height === '' ? '300px' : this.height
    return {
      width,
      height,
    }
  }

  get options(): object {

    const plotConfig = this.plotConfig
    const items = this.items

    const makeLabel = (it: ItemConfig) => it.Name + ((it.Axis === 'Right') ? ' [R]' : '')

    const seriesOptions = {}
    for (const it of items) {
      const scatter = it.SeriesType === 'Scatter'
      seriesOptions[makeLabel(it)] = {
        axis: it.Axis === 'Left' ? 'y' : 'y2',
        drawPoints: scatter,
        strokeWidth: scatter ? 0.0 : it.Size,
        pointSize: it.Size,
        color: it.Color,
      }
    }

    const axes = {
      y: {
        independentTicks: true,
        drawGrid: true,
        includeZero: plotConfig.LeftAxisStartFromZero,
        gridLinePattern: null,
      },
      y2: {
        independentTicks: true,
        drawGrid: true,
        includeZero: plotConfig.RightAxisStartFromZero,
        gridLinePattern: [2, 2],
      },
    }

    const context = this

    const legendFormatter = (data) => {
      const grID = context.dyGraphID
      if (grID === undefined) { return '' }
      const HasData = data.x != null
      const theSeries = data.series.map((series, i) => {
        const id = grID + '_varCheck_' + i
        const checked = series.isVisible ? 'checked' : ''
        const label = series.labelHTML
        return `<div style="background-color: white; display: inline;">` +
          `<input type=checkbox id="${id}" ${checked} onClick="window.changeVarVisibility_${grID}('${id}', ${i})">&nbsp;&nbsp;` +
          `<span style='font-weight: bold;'>${series.dashHTML} ${label}</span>` +
          (HasData && series.isVisible ? (': ' + series.yHTML) : '') +
          '&nbsp;</div>'
      })
      return (HasData ? data.xHTML : '') + '<br>' + theSeries.join('<br>')
    }

    const zoomCallback = (minDate, maxDate, yRanges: number[][]) => {

      const theGraph = context.dyGraph

      const xExtremes: number[] = theGraph.xAxisExtremes()
      // console.info('zoomCallback xAxisExtremes: ' + JSON.stringify(xExtremes) + ' minDate: ' + minDate + ' maxDate: ' + maxDate)
      let dateWindow: number[]
      if (xExtremes[0] !== minDate || xExtremes[1] !== maxDate) {
        dateWindow = [minDate, maxDate]
      }
      else {
        dateWindow = null
      }

      // console.info('date-window-changed: ' + JSON.stringify(dateWindow))
      context.$emit('date-window-changed', dateWindow)

      // console.info('zoomCallback: ' + JSON.stringify(yRanges))
      context.enforceYAxisLimitsWithCurrentRanges(yRanges)
    }

    return {
      labels: ['Date'].concat(items.map(makeLabel)),
      legend: 'always',
      series: seriesOptions,
      axes,
      drawAxesAtZero: true,
      includeZero: true,
      connectSeparatedPoints: true,
      ylabel: plotConfig.LeftAxisName,
      y2label: plotConfig.RightAxisName,
      visibility: items.map((it) => it.Checked),
      legendFormatter,
      zoomCallback,
    }
  }

  get dyGraph(): any | null {
    const gr: any = this.$refs.theGraph
    if (gr) {
      return gr._data.graph
    }
    return null
  }

  get dyGraphID(): string | undefined {
    const gr: any = this.$refs.theGraph
    if (gr) {
      return gr._data.id
    }
    return undefined
  }

  created() {
    const { left, right } = timeWindowFromTimeRange(this.timeRange)
    const data = []
    this.sliceDataToDateWindow(data, left, right)
    this.historyData = data
  }

  mounted(): void {

    const id = this.dyGraphID
    const context = this

    window['changeVarVisibility_' + id] = (elementID: string, index: number) => {
      const checkBox: any = document.getElementById(elementID)
      context.dyGraph.setVisibility(index, checkBox.checked)
    }

    this.onLoadDataForNextTab(false)
  }

  @Watch('dateWindow')
  watch_dateWindow(newVal: number[], old: number[]): void {
    this.applyDateWindow()
  }

  applyDateWindow(): void {

    const window = this.dateWindow
    const theGraph = this.dyGraph

    const opts = {
      dateWindow: window,
    }
    // console.info('watch_dateWindow -> ' + JSON.stringify(window))
    theGraph.updateOptions(opts)
  }

  enforceYAxisLimits(): void {

    const context = this

    this.$nextTick(() => {

      const theGraph = context.dyGraph

      if (theGraph.isZoomed('y')) {
        // console.info(theGraph.toString() + ' reset y')
        theGraph.axes_.forEach((axis) => {
          if (axis.valueRange) { delete axis.valueRange }
        })
        theGraph.drawGraph_()
      }

      const ranges = theGraph.yAxisRanges()
      // console.info(theGraph.toString() + ' yAxisRanges: ' + JSON.stringify(ranges))
      context.enforceYAxisLimitsWithCurrentRanges(ranges)
    })
  }

  enforceYAxisLimitsWithCurrentRanges(yRanges: number[][]): void {

    const plotConfig = this.config.PlotConfig
    const y1UpperBound: number | null = plotConfig.LeftAxisLimitY ?? null
    const y2UpperBound: number | null = plotConfig.RightAxisLimitY ?? null

    // console.info('y1UpperBound: ' + JSON.stringify(y1UpperBound))
    // console.info('y2UpperBound: ' + JSON.stringify(y2UpperBound))

    if (y1UpperBound !== null || y2UpperBound !== null) {

      const theGraph = this.dyGraph

      const y1 = yRanges[0]
      const y2 = yRanges[1] ?? [0, 0.001]
      const y1Lower = y1[0]
      const y2Lower = y2[0]
      const y1Upper = y1[1]
      const y2Upper = y2[1]

      const y1NeedsUpdate = y1UpperBound !== null && y1Upper > y1UpperBound
      const y2NeedsUpdate = y2UpperBound !== null && y2Upper > y2UpperBound

      if (y1NeedsUpdate || y2NeedsUpdate) {

        const newY1Upper = y1UpperBound === null ? y1Upper : Math.min(y1Upper, y1UpperBound)
        const newY2Upper = y2UpperBound === null ? y2Upper : Math.min(y2Upper, y2UpperBound)

        const axesValueRange = {
          y: {
            valueRange: [y1Lower, newY1Upper],
          },
          y2: {
            valueRange: [y2Lower, newY2Upper],
          },
        }

        theGraph.updateOptions({
          axes: axesValueRange,
        })
      }
    }
  }

  @Watch('resize')
  watch_resize(newVal: object, old: object): void {
    const theGraph = this.dyGraph
    if (theGraph) {
      const fn = () => {
        theGraph.resize()
      }
      setTimeout(fn, 500)
    }
  }

  @Watch('timeRange')
  watch_timeRange(newVal: object, old: object): void {
    this.onLoadDataForNextTab(true)
  }

  @Watch('eventPayload')
  watch_event(newVal: object, old: object): void {

    if (this.eventName === 'DataAppend') {

      const payload: {
        Data: string,
        WindowLeft: number,
        WindowRight: number } = this.eventPayload as any

      const newData: any[][] = JSON.parse(payload.Data)
      if (newData.length === 0) { return }

      this.convertTimestamps(newData)

      const data = this.historyData

      const hasData = (entry) => {
        for (let k = 1; k < entry.length; ++k) {
          if (entry[k] !== null) { return true }
        }
        return false
      }

      const t = newData[0][0]
      const len = data.length
      let countRemove = 0
      for (let i = len - 1; i >= 0; --i) {
        const entry = data[i]
        if (entry[0] < t && hasData(entry)) {
          break
        }
        countRemove += 1
      }
      if (countRemove > 0) {
        data.splice(-countRemove, countRemove)
      }

      newData.forEach((element) => {
        data.push(element)
      })

      this.sliceDataToDateWindow(data, payload.WindowLeft, payload.WindowRight)
    }
  }

  onContextMenu(e: any): void {
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

  async onConfigurePlotItems(): Promise<void> {

    const response: {
        ObjectMap: ObjectMap,
        Modules: ModuleInfo[],
      } = await this.backendAsync('GetItemsData', { })

    this.objectMap = response.ObjectMap
    this.selectObject.modules = response.Modules

    const str = JSON.stringify(this.items)
    this.editorItems.items = JSON.parse(str)
    this.editorItems.show = true
  }

  get isItemsOK(): boolean {
    return this.editorItems.items.every((it) => {
      return it.Name !== '' &&
        it.Variable.Object !== '' &&
        it.Variable.Name !== ''
    })
  }

  async onLoadDataForNextTab(resetZoom: boolean): Promise<void> {

    const para = {
      timeRange: this.timeRange,
    }

    const response: {
        WindowLeft: number,
        WindowRight: number,
        Data: any[][],
      } = await this.backendAsync('LoadData', para)

    const data: any[][] = response.Data

    this.convertTimestamps(data)
    this.sliceDataToDateWindow(data, response.WindowLeft, response.WindowRight)
    this.historyData = data

    this.enforceYAxisLimits()

    if (resetZoom) {
      // console.info('onLoadDataForNextTab: resetZoom...')
      this.zoomResetTime = new Date().getTime()
    }
    else {
      this.applyDateWindow()
    }
  }

  convertTimestamps(data: any[][]): void {
    const len = data.length
    for (let i = 0; i < len; ++i) {
      const entry = data[i]
      entry[0] = new Date(entry[0])
    }
  }

  sliceDataToDateWindow(data: any[][], windowLeft: number, windowRight: number): void {

    const seriesCount = this.variables.length

    const leftData  = [new Date(windowLeft)]
    const rightData = [new Date(windowRight)]
    for (let i = 0; i < seriesCount; ++i) {
      leftData.push(null)
      rightData.push(null)
    }

    if (data.length === 0) {
      data.push(leftData)
      data.push(rightData)
    }
    else {

      while (data.length > 0 && data[0][0].getTime() < windowLeft) {
        data.shift()
      }

      if (windowLeft < data[0][0].getTime()) {
        data.unshift(leftData)
      }

      if (data[data.length - 1][0].getTime() < windowRight) {
        data.push(rightData)
      }
    }
  }

  editorItems_AddItem(): void {
    const item: ItemConfig = {
      Name: '',
      Color: this.editorItems.colorList[this.editorItems.items.length % this.editorItems.colorList.length],
      Size: 3,
      SeriesType: 'Scatter',
      Axis: 'Left',
      Checked: true,
      Variable: {
        Object: '',
        Name: '',
      },
    }
    this.editorItems.items.push(item)
  }

  editorItems_DeleteItem(idx: number): void {
    this.editorItems.items.splice(idx, 1)
  }

  editorItems_MoveUpItem(idx: number): void {
    const array = this.editorItems.items
    if (idx > 0) {
      const item = array[idx]
      array.splice(idx, 1)
      array.splice(idx - 1, 0, item)
    }
  }

  async editorItems_Save(): Promise<void> {

    this.editorItems.show = false

    const para = {
      items: this.editorItems.items,
    }

    const response: {
        ReloadData: boolean,
      } = await this.backendAsync('SaveItems', para)

    if (response.ReloadData) {
      this.onLoadDataForNextTab(true)
    }
  }

  editorItems_ObjectID2Name(id: string): string {
    const obj: ObjInfo = this.objectMap[id]
    if (obj === undefined) { return id }
    return obj.Name
  }

  editorItems_ObjectID2Variables(id: string): string[] {
    const obj: ObjInfo = this.objectMap[id]
    if (obj === undefined) { return [] }
    return obj.Variables
  }

  onConfigurePlot(): void {
    const str = JSON.stringify(this.plotConfig)
    // console.info(str)
    this.editorPlot.plot = JSON.parse(str)
    this.editorPlot.show = true
  }

  async editorPlot_Save(): Promise<void> {

    this.editorPlot.show = false

    const para = {
      plot: this.editorPlot.plot,
    }

    const response: {
        ReloadData: boolean,
      } = await this.backendAsync('SavePlot', para)

    if (response.ReloadData) {
      this.onLoadDataForNextTab(true)
    }
  }

  editorItems_SelectObj(item: ItemConfig): void {
    const currObj: string = item.Variable.Object
    let objForModuleID: string = currObj
    if (objForModuleID === '') {
      const nonEmptyItems = this.editorItems.items.filter((it) => it.Variable.Object !== '')
      if (nonEmptyItems.length > 0) {
        objForModuleID = nonEmptyItems[0].Variable.Object
      }
    }

    const i = objForModuleID.indexOf(':')
    if (i <= 0) {
      this.selectObject.selectedModuleID = this.selectObject.modules[0].ID
    }
    else {
      this.selectObject.selectedModuleID = objForModuleID.substring(0, i)
    }
    this.selectObject.selectedObjectID = currObj
    this.selectObject.variable = item.Variable
    this.selectObject.show = true
  }

  selectObject_OK(obj: Obj): void {
    this.objectMap[obj.ID] = {
      Name: obj.Name,
      Variables: obj.Variables,
    }
    this.selectObject.variable.Object = obj.ID
    if (obj.Variables.length === 1) {
      this.selectObject.variable.Name = obj.Variables[0]
    }
  }

  downloadSpreadsheet(): void {
    this.downloadFile('Spreadsheet', '.xlsx')
  }

  downloadCSV(): void {
    this.downloadFile('CSV', '.csv')
  }

  async downloadFile(type: string, extension: string): Promise<void> {

    const visibilty = this.dyGraph.visibility()

    const para = {
      timeRange: this.timeRange,
      variables: this.variables.filter((x, i) => visibilty[i]),
      variableNames: this.items.filter((x, i) => visibilty[i]).map((it) => it.Name),
      fileType: type,
    }
    const blobResponse = await this.backendAsync('DownloadFile', para, 'blob')
    this.downloadBlob(blobResponse, 'HistoryData' + extension)
  }

  downloadBlob(blob, filename): void {
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename || 'download'
    a.click()
    setTimeout(() => {
      URL.revokeObjectURL(url)
    }, 500)
  }

}

</script>
