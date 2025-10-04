<template>
  <div>
    <div @contextmenu="onContextMenu">
      <dy-graph
        ref="theGraph"
        :graph-data="historyData"
        :graph-options="options"
        :graph-reset-zoom="zoomResetTime"
        :graph-style="graphStyle"
      ></dy-graph>
    </div>

    <v-menu
      v-model="contextMenu.show"
      :target="[contextMenu.clientX, contextMenu.clientY]"
    >
      <v-list>
        <v-list-item
          v-if="canUpdateConfig"
          @click="onConfigurePlotItems"
        >
          <v-list-item-title>Configure Plot Items...</v-list-item-title>
        </v-list-item>
        <v-list-item
          v-if="canUpdateConfig"
          @click="onConfigurePlot"
        >
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

    <v-dialog
      v-model="editorItems.show"
      max-width="1100px"
      persistent
      @keydown="onEditorItemsKeydown"
    >
      <v-card>
        <v-card-title>Configure Plot Items</v-card-title>
        <v-card-text>
          <table style="width: 100%; border-collapse: collapse">
            <thead>
              <tr>
                <th style="width: 100%; text-align: left">Name</th>
                <th>Color</th>
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
            <tbody>
              <tr
                v-for="(item, idx) in editorItems.items"
                :key="idx"
              >
                <td>
                  <v-text-field v-model="item.Name"></v-text-field>
                </td>
                <td>
                  <v-menu offset-y>
                    <template #activator="{ props }">
                      <v-btn
                        v-bind="props"
                        class="ml-2 mr-2"
                        style="min-width: 32px; width: 32px; height: 32px"
                        :style="{ backgroundColor: item.Color }"
                      ></v-btn>
                    </template>
                    <div>
                      <div
                        v-for="(color, index) in editorItems.colorList"
                        :key="index"
                        @click="item.Color = color"
                      >
                        <div
                          style="padding: 6px; cursor: pointer"
                          :style="{ backgroundColor: color }"
                        >
                          {{ color }}
                        </div>
                      </div>
                    </div>
                  </v-menu>
                </td>
                <td>
                  <v-text-field
                    v-model="item.Color"
                    style="width: 9ch"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.Size"
                    style="margin-left: 1ex; width: 5ch"
                  ></v-text-field>
                </td>
                <td>
                  <v-select
                    v-model="item.SeriesType"
                    :items="['Scatter', 'Line']"
                    style="margin-left: 1ex; width: 10ch"
                  ></v-select>
                </td>
                <td>
                  <v-select
                    v-model="item.Axis"
                    :items="['Left', 'Right']"
                    style="margin-left: 1ex; width: 8ch"
                  ></v-select>
                </td>
                <td>
                  <v-checkbox
                    v-model="item.Checked"
                    style="margin-left: 1ex; margin-right: 1ex"
                  ></v-checkbox>
                </td>
                <td style="font-size: 16px; max-width: 18ch; word-wrap: break-word">
                  {{ editorItems_ObjectID2Name(item.Variable.Object) }}
                </td>
                <td>
                  <v-btn
                    icon="mdi-pencil"
                    size="small"
                    variant="text"
                    @click="editorItems_SelectObj(item)"
                  ></v-btn>
                </td>
                <td>
                  <v-combobox
                    v-model="item.Variable.Name"
                    :items="editorItems_ObjectID2Variables(item.Variable)"
                    style="width: 12ch"
                  ></v-combobox>
                </td>
                <td>
                  <v-btn
                    icon="mdi-delete"
                    size="small"
                    variant="text"
                    @click="editorItems_DeleteItem(idx)"
                  ></v-btn>
                </td>
                <td>
                  <v-btn
                    v-if="idx > 0"
                    icon="mdi-chevron-up"
                    size="small"
                    variant="text"
                    @click="editorItems_MoveUpItem(idx)"
                  ></v-btn>
                </td>
              </tr>
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
                <td>
                  <v-btn
                    icon="mdi-plus"
                    size="small"
                    variant="text"
                    @click="editorItems_AddItem"
                  ></v-btn>
                </td>
                <td>&nbsp;</td>
              </tr>
            </tbody>
          </table>
        </v-card-text>
        <v-card-actions>
          <v-btn
            variant="text"
            @click="replaceEditorItemsFromCsvFromClipboard"
            >Import</v-btn
          >
          <v-btn
            variant="text"
            @click="copyEditorItemsAsCsvToClipboard"
            >Export</v-btn
          >
          <v-spacer></v-spacer>
          <v-btn
            color="grey-darken-1"
            variant="text"
            @click="editorItems.show = false"
            >Cancel</v-btn
          >
          <v-btn
            color="primary-darken-1"
            :disabled="!isItemsOK"
            variant="text"
            @click="editorItems_Save"
            >Save</v-btn
          >
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog
      v-model="editorPlot.show"
      max-width="400px"
      persistent
      @keydown="onEditorPlotKeydown"
    >
      <v-card>
        <v-card-title>
          <span class="text-h5">Configure Plot</span>
        </v-card-title>
        <v-card-text>
          <v-table style="width: 100%">
            <tbody>
              <tr>
                <td>
                  <v-text-field
                    v-model="editorPlot.plot.LeftAxisName"
                    label="Left Axis Caption"
                  ></v-text-field>
                </td>
              </tr>
              <tr>
                <td>
                  <v-text-field
                    v-model="editorPlot.plot.LeftAxisScaleDivisor"
                    label="Left Axis Scale Divisor"
                  ></v-text-field>
                </td>
              </tr>
              <tr>
                <td>
                  <v-checkbox
                    v-model="editorPlot.plot.LeftAxisStartFromZero"
                    label="Left Y Axis: Start From Zero"
                  ></v-checkbox>
                </td>
              </tr>
              <tr>
                <td>
                  <text-field-nullable-number
                    v-model="editorPlot.plot.LeftAxisLimitY"
                    label="Left Y Axis: Limit Y Value"
                  ></text-field-nullable-number>
                </td>
              </tr>
              <tr>
                <td>
                  <v-text-field
                    v-model="editorPlot.plot.RightAxisName"
                    label="Right Axis Caption"
                  ></v-text-field>
                </td>
              </tr>
              <tr>
                <td>
                  <v-text-field
                    v-model="editorPlot.plot.RightAxisScaleDivisor"
                    label="Right Axis Scale Divisor"
                  ></v-text-field>
                </td>
              </tr>
              <tr>
                <td>
                  <v-checkbox
                    v-model="editorPlot.plot.RightAxisStartFromZero"
                    label="Right Y Axis: Start From Zero"
                  ></v-checkbox>
                </td>
              </tr>
              <tr>
                <td>
                  <text-field-nullable-number
                    v-model="editorPlot.plot.RightAxisLimitY"
                    label="Right Y Axis: Limit Y Value"
                  ></text-field-nullable-number>
                </td>
              </tr>
              <tr>
                <td>
                  <v-text-field
                    v-model="editorPlot.plot.MaxDataPoints"
                    label="Max DataPoints"
                  ></v-text-field>
                </td>
              </tr>
              <tr>
                <td>
                  <v-select
                    v-model="editorPlot.plot.FilterByQuality"
                    :items="qualityFilterValues"
                    label="QualityFilter"
                  ></v-select>
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn
            color="grey-darken-1"
            variant="text"
            @click="editorPlot.show = false"
            >Cancel</v-btn
          >
          <v-btn
            color="primary-darken-1"
            variant="text"
            @click="editorPlot_Save"
            >Save</v-btn
          >
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog
      v-model="downloadOptions.show"
      max-width="350px"
      persistent
      @keydown="onDownloadOptionsKeydown"
    >
      <v-card>
        <v-card-title>
          <span class="text-h5">Download Options</span>
        </v-card-title>
        <v-card-text>
          <v-container class="pa-3">
            <v-row>
              <v-col
                cols="12"
                class="pa-1"
              >
                <v-menu
                  v-model="downloadOptions.rangeStartShow"
                  :close-on-content-click="false"
                  min-width="auto"
                  offset-y
                  transition="scale-transition"
                >
                  <template #activator="{ props }">
                    <v-text-field
                      v-model="downloadOptions.rangeStart"
                      append-inner-icon="mdi-calendar"
                      density="compact"
                      label="Range Start"
                      v-bind="props"
                    ></v-text-field>
                  </template>
                  <v-date-picker
                    v-model="downloadOptions.rangeStart"
                    @update:model-value="downloadOptions.rangeStartShow = false"
                  ></v-date-picker>
                </v-menu>
              </v-col>
            </v-row>

            <v-row>
              <v-col
                cols="12"
                class="pa-1"
              >
                <v-menu
                  v-model="downloadOptions.rangeEndShow"
                  :close-on-content-click="false"
                  min-width="auto"
                  offset-y
                  transition="scale-transition"
                >
                  <template #activator="{ props }">
                    <v-text-field
                      v-model="downloadOptions.rangeEnd"
                      append-inner-icon="mdi-calendar"
                      density="compact"
                      label="Range End"
                      v-bind="props"
                    ></v-text-field>
                  </template>
                  <v-date-picker
                    v-model="downloadOptions.rangeEnd"
                    @update:model-value="downloadOptions.rangeEndShow = false"
                  ></v-date-picker>
                </v-menu>
              </v-col>
            </v-row>

            <v-row>
              <v-col
                cols="12"
                class="pa-1"
              >
                <v-select
                  v-model="downloadOptions.aggregation"
                  density="compact"
                  :items="aggregationValues"
                  label="Aggregation"
                ></v-select>
              </v-col>
            </v-row>

            <v-row v-if="downloadOptions.aggregation !== 'None'">
              <v-col
                cols="5"
                class="pa-1"
              >
                <v-text-field
                  v-model.number="downloadOptions.resolutionCount"
                  density="compact"
                  label="Resolution"
                  type="number"
                ></v-text-field>
              </v-col>
              <v-col
                cols="7"
                class="pa-1"
              >
                <v-select
                  v-model="downloadOptions.resolutionUnit"
                  density="compact"
                  :items="timeUnitValues"
                ></v-select>
              </v-col>
            </v-row>

            <v-row v-if="downloadOptions.fileType === 'Spreadsheet'">
              <v-col
                cols="12"
                class="pa-1"
              >
                <v-checkbox
                  v-model="downloadOptions.simbaFormat"
                  density="compact"
                  label="SIMBA Format"
                ></v-checkbox>
              </v-col>
            </v-row>

            <v-row v-if="downloadOptions.aggregation !== 'None' && !downloadOptions.simbaFormat">
              <v-col
                cols="12"
                class="pa-1"
              >
                <v-checkbox
                  v-model="downloadOptions.skipEmptyIntervals"
                  density="compact"
                  label="Skip Empty Intervals"
                ></v-checkbox>
              </v-col>
            </v-row>
          </v-container>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn
            color="grey-darken-1"
            variant="text"
            @click="downloadOptions.show = false"
            >Cancel</v-btn
          >
          <v-btn
            color="primary-darken-1"
            variant="text"
            @click="downloadOptions_OK"
            >OK</v-btn
          >
        </v-card-actions>
      </v-card>
    </v-dialog>

    <dlg-object-select
      v-model="selectObject.show"
      :allow-config-variables="true"
      :module-id="selectObject.selectedModuleID"
      :modules="selectObject.modules"
      :object-id="selectObject.selectedObjectID"
      @on-selected="selectObject_OK"
    ></dlg-object-select>
  </div>
</template>

<script setup lang="ts">
// @ts-nocheck
import { ref, computed, watch, onMounted, onBeforeUnmount, nextTick, getCurrentInstance } from 'vue'
import DyGraph from '../../components/DyGraph.vue'
import DlgObjectSelect from '../../components/DlgObjectSelect.vue'
import type { TimeRange, TimeUnit } from '../../utils'
import { TimeUnitValues, timeWindowFromTimeRange, getLocalDateIsoStringFromTimestamp } from '../../utils'
import TextFieldNullableNumber from '../../components/TextFieldNullableNumber.vue'
import type { ModuleInfo, ObjectMap, Obj, Variable, SelectObject, ObjInfo } from './common'
import * as model from '../model'

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
  LeftAxisScaleDivisor: number
  RightAxisName: string
  RightAxisStartFromZero: boolean
  RightAxisLimitY?: number | null
  RightAxisScaleDivisor: number
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

type Aggregation = 'None' | 'Average' | 'Min' | 'Max' | 'First' | 'Last' | 'Count'
const AggregationValues: Aggregation[] = ['None', 'Average', 'Min', 'Max', 'First', 'Last', 'Count']

type FileType = 'CSV' | 'Spreadsheet'

interface DownloadOptions {
  show: boolean
  rangeStartShow: boolean
  rangeEndShow: boolean
  rangeStart: string
  rangeEnd: string
  fileType: FileType
  aggregation: Aggregation
  resolutionCount: number
  resolutionUnit: TimeUnit
  skipEmptyIntervals: boolean
  simbaFormat: boolean
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

// Props
interface Props {
  id?: string
  width?: string
  height?: string
  config: Config
  backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  eventName?: string
  eventPayload?: object
  timeRange?: TimeRange
  resize?: number
  dateWindow?: number[]
  configVariables?: model.ConfigVariableValues
}

const props = withDefaults(defineProps<Props>(), {
  id: '',
  width: '',
  height: '',
  eventName: '',
  eventPayload: () => ({}),
  timeRange: () => ({}) as TimeRange,
  resize: 0,
  dateWindow: () => [],
  configVariables: () => ({}) as model.ConfigVariableValues,
})

// Emits
const emit = defineEmits<{
  'date-window-changed': [dateWindow: number[] | null]
}>()

// Reactive data
const qualityFilterValues = ref(QualityFilterValues)
const canUpdateConfig = ref(false)
const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})
const historyData = ref<any[][]>([])
const zoomResetTime = ref(0)
const objectMap = ref<ObjectMap>({})
const editorItems = ref<EditorItems>({
  show: false,
  items: [],
  colorList: ['#1BA1E2', '#A05000', '#339933', '#A2C139', '#D80073', '#F09609', '#E671B8', '#A200FF', '#E51400', '#00ABA9', '#000000', '#CCCCCC'],
})
const editorPlot = ref<EditorPlot>({
  show: false,
  plot: {
    MaxDataPoints: 100,
    FilterByQuality: 'ExcludeBad',
    LeftAxisName: '',
    LeftAxisStartFromZero: true,
    LeftAxisLimitY: null,
    LeftAxisScaleDivisor: 1,
    RightAxisName: '',
    RightAxisStartFromZero: true,
    RightAxisLimitY: null,
    RightAxisScaleDivisor: 1,
  },
})
const selectObject = ref<SelectObject>({
  show: false,
  modules: [],
  selectedModuleID: '',
  selectedObjectID: '',
})
const aggregationValues = ref(AggregationValues)
const timeUnitValues = ref(TimeUnitValues)
const downloadOptions = ref<DownloadOptions>({
  show: false,
  rangeStartShow: false,
  rangeEndShow: false,
  rangeStart: '',
  rangeEnd: '',
  fileType: 'CSV',
  aggregation: 'None',
  resolutionCount: 15,
  resolutionUnit: 'Minutes',
  skipEmptyIntervals: false,
  simbaFormat: false,
})
const currentVariable = ref<Variable>({ Object: '', Name: '' })
const dataRevision = ref(0)
const stringWithVarResolvedMap = ref<Map<string, string>>(new Map())

// Template refs
const theGraph = ref<InstanceType<typeof DyGraph> | null>(null)

// Computed properties
const variables = computed(() => {
  return items.value.map((it) => it.Variable)
})

const items = computed(() => {
  return props.config.Items ?? []
})

const plotConfig = computed(() => {
  return (
    props.config.PlotConfig ?? {
      MaxDataPoints: 1000,
      FilterByQuality: 'ExcludeBad',
      LeftAxisName: '',
      LeftAxisLimitY: null,
      LeftAxisScaleDivisor: 1,
      RightAxisName: '',
      LeftAxisStartFromZero: true,
      RightAxisStartFromZero: true,
      RightAxisLimitY: null,
      RightAxisScaleDivisor: 1,
    }
  )
})

const graphStyle = computed(() => {
  const width = props.width === '' ? '100%' : props.width
  const height = props.height === '' ? '300px' : props.height
  return {
    width,
    height,
  }
})

const options = computed(() => {
  const plotConfigValue = plotConfig.value
  const itemsValue = items.value

  const makeLabel = (it: ItemConfig) => {
    const name = model.VariableReplacer.replaceVariables(it.Name, props.configVariables?.VarValues || {})
    return name + (it.Axis === 'Right' ? ' [R]' : '')
  }

  const seriesOptions: any = {}
  for (const it of itemsValue) {
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
      includeZero: plotConfigValue.LeftAxisStartFromZero,
      gridLinePattern: null,
    },
    y2: {
      independentTicks: true,
      drawGrid: true,
      includeZero: plotConfigValue.RightAxisStartFromZero,
      gridLinePattern: [2, 2],
    },
  }

  const instance = getCurrentInstance()

  const legendFormatter = (data: any) => {
    const grID = dyGraphID.value
    if (grID === undefined) {
      return ''
    }
    const HasData = data.x != null
    const theSeries = data.series.map((series: any, i: number) => {
      const id = grID + '_varCheck_' + i
      const checked = series.isVisible ? 'checked' : ''
      const label = series.labelHTML
      return (
        `<div style="background-color: white; display: inline;">` +
        `<input type=checkbox id="${id}" ${checked} onClick="window.changeVarVisibility_${grID}('${id}', ${i})">&nbsp;&nbsp;` +
        `<span style='font-weight: bold;'>${series.dashHTML} ${label}</span>` +
        (HasData && series.isVisible ? ': ' + series.yHTML : '') +
        '&nbsp;</div>'
      )
    })
    return (HasData ? data.xHTML : '') + '<br>' + theSeries.join('<br>')
  }

  const zoomCallback = (minDate: number, maxDate: number, yRanges: number[][]) => {
    const theGraphValue = dyGraph.value

    const xExtremes: number[] = theGraphValue.xAxisExtremes()
    let dateWindow: number[] | null
    if (xExtremes[0] !== minDate || xExtremes[1] !== maxDate) {
      dateWindow = [minDate, maxDate]
    } else {
      dateWindow = null
    }

    emit('date-window-changed', dateWindow)
    enforceYAxisLimitsWithCurrentRanges(yRanges)
  }

  return {
    labels: ['Date'].concat(itemsValue.map(makeLabel)),
    legend: 'always',
    series: seriesOptions,
    axes,
    drawAxesAtZero: true,
    includeZero: true,
    connectSeparatedPoints: true,
    ylabel: resolvedLeftAxisName.value,
    y2label: resolvedRightAxisName.value,
    visibility: itemsValue.map((it) => it.Checked),
    legendFormatter,
    zoomCallback,
  }
})

const resolvedLeftAxisName = computed(() => {
  return model.VariableReplacer.replaceVariables(plotConfig.value.LeftAxisName, props.configVariables?.VarValues || {})
})

const resolvedRightAxisName = computed(() => {
  return model.VariableReplacer.replaceVariables(plotConfig.value.RightAxisName, props.configVariables?.VarValues || {})
})

const dyGraph = computed(() => {
  const gr: any = theGraph.value
  if (gr) {
    return gr._data.graph.value
  }
  return null
})

const dyGraphID = computed(() => {
  const gr: any = theGraph.value
  if (gr) {
    return gr._data.id.value
  }
  return undefined
})

const isItemsOK = computed(() => {
  return editorItems.value.items.every((it) => {
    return it.Name !== '' && it.Variable.Object !== '' && it.Variable.Name !== ''
  })
})

// Methods
const onContextMenu = (e: MouseEvent): void => {
  e.preventDefault()
  e.stopPropagation()
  contextMenu.value.show = false
  contextMenu.value.clientX = e.clientX
  contextMenu.value.clientY = e.clientY
  nextTick(() => {
    contextMenu.value.show = true
  })
}

const onEditorItemsKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape' && !selectObject.value.show) {
    editorItems.value.show = false
  }
}

const onEditorPlotKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    editorPlot.value.show = false
  }
}

const onDownloadOptionsKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    downloadOptions.value.show = false
  }
}

const onConfigurePlotItems = async (): Promise<void> => {
  const response: {
    ObjectMap: ObjectMap
    Modules: ModuleInfo[]
  } = await props.backendAsync('GetItemsData', {})

  objectMap.value = response.ObjectMap
  selectObject.value.modules = response.Modules

  const str = JSON.stringify(items.value)
  editorItems.value.items = JSON.parse(str)
  editorItems.value.show = true
}

const onLoadData = async (resetZoom: boolean): Promise<void> => {
  const para = {
    timeRange: props.timeRange,
    configVars: props.configVariables?.VarValues || {},
  }

  const response: {
    WindowLeft: number
    WindowRight: number
    Data: any[][]
    DataRevision: number
  } = await props.backendAsync('LoadData', para)

  const resolveMap = stringWithVarResolvedMap.value
  for (const it of items.value) {
    const obj = it.Variable.Object
    resolveMap.set(obj, model.VariableReplacer.replaceVariables(obj, props.configVariables?.VarValues || {}))
    const name = it.Name
    resolveMap.set(name, model.VariableReplacer.replaceVariables(name, props.configVariables?.VarValues || {}))
  }

  dataRevision.value = response.DataRevision
  const data: any[][] = response.Data

  convertTimestamps(data)
  sliceDataToDateWindow(data, response.WindowLeft, response.WindowRight)
  historyData.value = data

  enforceYAxisLimits()

  if (resetZoom) {
    zoomResetTime.value = new Date().getTime()
  } else {
    applyDateWindow()
  }
}

const convertTimestamps = (data: any[][]): void => {
  const len = data.length
  for (let i = 0; i < len; ++i) {
    const entry = data[i]
    entry[0] = new Date(entry[0])
  }
}

const sliceDataToDateWindow = (data: any[][], windowLeft: number, windowRight: number): void => {
  const seriesCount = variables.value.length

  const leftData: any[] = [new Date(windowLeft)]
  const rightData: any[] = [new Date(windowRight)]
  for (let i = 0; i < seriesCount; ++i) {
    leftData.push(null)
    rightData.push(null)
  }

  if (data.length === 0) {
    data.push(leftData)
    data.push(rightData)
  } else {
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

const applyDateWindow = (): void => {
  const window = props.dateWindow
  const theGraphValue = dyGraph.value

  const opts = {
    dateWindow: window,
  }
  theGraphValue.updateOptions(opts)
}

const enforceYAxisLimits = (): void => {
  nextTick(() => {
    const theGraphValue = dyGraph.value
    if (!theGraphValue) {
      return
    }
    if (theGraphValue.isZoomed('y')) {
      theGraphValue.axes_.forEach((axis: any) => {
        if (axis.valueRange) {
          delete axis.valueRange
        }
      })
      theGraphValue.drawGraph_()
    }

    const ranges = theGraphValue.yAxisRanges()
    enforceYAxisLimitsWithCurrentRanges(ranges)
  })
}

const enforceYAxisLimitsWithCurrentRanges = (yRanges: number[][]): void => {
  const plotConfigValue = props.config.PlotConfig
  const y1UpperBound: number | null = plotConfigValue.LeftAxisLimitY ?? null
  const y2UpperBound: number | null = plotConfigValue.RightAxisLimitY ?? null

  if (y1UpperBound !== null || y2UpperBound !== null) {
    const theGraphValue = dyGraph.value

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

      theGraphValue.updateOptions({
        axes: axesValueRange,
      })
    }
  }
}

const editorItems_AddItem = (): void => {
  const item: ItemConfig = {
    Name: '',
    Color: editorItems.value.colorList[editorItems.value.items.length % editorItems.value.colorList.length],
    Size: 3,
    SeriesType: 'Scatter',
    Axis: 'Left',
    Checked: true,
    Variable: {
      Object: '',
      Name: '',
    },
  }
  editorItems.value.items.push(item)
}

const editorItems_DeleteItem = (idx: number): void => {
  editorItems.value.items.splice(idx, 1)
}

const editorItems_MoveUpItem = (idx: number): void => {
  const array = editorItems.value.items
  if (idx > 0) {
    const item = array[idx]
    array.splice(idx, 1)
    array.splice(idx - 1, 0, item)
  }
}

const editorItems_Save = async (): Promise<void> => {
  editorItems.value.show = false

  const para = {
    items: editorItems.value.items,
  }

  try {
    const response: {
      ReloadData: boolean
    } = await props.backendAsync('SaveItems', para)

    if (response.ReloadData) {
      onLoadData(true)
    }
  } catch (err: any) {
    alert(err.message)
  }
}

const copyEditorItemsAsCsvToClipboard = (): void => {
  const csvData = editorItems.value.items
    .map((item: ItemConfig) => {
      return `${item.Name},${item.Color},${item.Size},${item.SeriesType},${item.Axis},${item.Checked},${item.Variable.Object},${item.Variable.Name}`
    })
    .join('\r\n')

  const header = 'Name,Color,Size,SeriesType,Axis,Checked,Object,Variable'
  const csvDataWithHeader = header + '\r\n' + csvData

  navigator.clipboard
    .writeText(csvDataWithHeader)
    .then(() => {
      alert('CSV data copied to clipboard')
    })
    .catch((error) => {
      alert('Failed to copy CSV data to clipboard: ' + error)
    })
}

const replaceEditorItemsFromCsvFromClipboard = (): void => {
  navigator.clipboard
    .readText()
    .then((csvData) => {
      const lines = csvData.split('\n')
      const header = lines.shift()?.split(',') // get header row and split into columns

      const newItems = lines
        .filter((line) => line.trim() !== '')
        .map((line, idx) => {
          const parts = line.split(',')
          const it: ItemConfig = {
            Name: '',
            Color: editorItems.value.colorList[idx % editorItems.value.colorList.length],
            Size: 3,
            SeriesType: 'Scatter',
            Axis: 'Left',
            Checked: true,
            Variable: { Object: '', Name: 'Value' },
          }

          // Assign values to properties based on header column names
          header?.forEach((column, index) => {
            switch (column) {
              case 'Name':
                it.Name = parts[index]
                break
              case 'Color':
                it.Color = parts[index]
                break
              case 'Size':
                it.Size = parseInt(parts[index])
                break
              case 'SeriesType':
                it.SeriesType = parts[index] as SeriesType
                break
              case 'Axis':
                it.Axis = parts[index] as Axis
                break
              case 'Checked':
                it.Checked = parts[index] === 'true'
                break
              case 'Object':
                if (parts[index]) {
                  it.Variable = { ...it.Variable, Object: parts[index] }
                }
                break
              case 'Variable':
                if (parts[index]) {
                  it.Variable = { ...it.Variable, Name: parts[index] }
                }
                break
            }
          })

          if (it.Name === '') {
            // If no name is provided, use it.Variable.Object but only the part after the first semicolon:
            const i = it.Variable.Object.indexOf(':')
            it.Name = i > 0 ? it.Variable.Object.substring(i + 1) : it.Variable.Object
          }

          return it
        })

      editorItems.value.items = newItems
    })
    .catch((error) => {
      alert('Failed to paste CSV data from clipboard: ' + error)
    })
}

const editorItems_ObjectID2Name = (id: string): string => {
  const obj: ObjInfo = objectMap.value[id]
  if (obj === undefined) {
    return id
  }
  return obj.Name
}

const editorItems_ObjectID2Variables = (id: Variable): string[] => {
  const obj: ObjInfo = objectMap.value[id.Object]
  if (obj === undefined) {
    if (id.Name === '') {
      return []
    }
    return [id.Name]
  }
  return obj.Variables
}

const onConfigurePlot = (): void => {
  const str = JSON.stringify(plotConfig.value)
  editorPlot.value.plot = JSON.parse(str)
  editorPlot.value.show = true
}

const editorPlot_Save = async (): Promise<void> => {
  editorPlot.value.show = false

  const para = {
    plot: editorPlot.value.plot,
  }

  try {
    const response: {
      ReloadData: boolean
    } = await props.backendAsync('SavePlot', para)

    if (response.ReloadData) {
      onLoadData(true)
    }
  } catch (err: any) {
    alert(err.message)
  }
}

const editorItems_SelectObj = (item: ItemConfig): void => {
  const currObj: string = item.Variable.Object
  let objForModuleID: string = currObj
  if (objForModuleID === '') {
    const nonEmptyItems = editorItems.value.items.filter((it) => it.Variable.Object !== '')
    if (nonEmptyItems.length > 0) {
      objForModuleID = nonEmptyItems[0].Variable.Object
    }
  }

  const i = objForModuleID.indexOf(':')
  if (i <= 0) {
    selectObject.value.selectedModuleID = selectObject.value.modules[0].ID
  } else {
    selectObject.value.selectedModuleID = objForModuleID.substring(0, i)
  }
  selectObject.value.selectedObjectID = currObj
  selectObject.value.show = true
  currentVariable.value = item.Variable
}

const selectObject_OK = (obj: Obj): void => {
  objectMap.value[obj.ID] = {
    Name: obj.Name,
    Variables: obj.Variables,
  }
  currentVariable.value.Object = obj.ID
  if (obj.Variables.length === 1) {
    currentVariable.value.Name = obj.Variables[0]
  }
}

const downloadSpreadsheet = (): void => {
  downloadOptions.value.fileType = 'Spreadsheet'
  showDownloadDlg()
}

const downloadCSV = (): void => {
  downloadOptions.value.fileType = 'CSV'
  downloadOptions.value.simbaFormat = false
  showDownloadDlg()
}

const showDownloadDlg = (): void => {
  if (props.timeRange?.type === 'Range') {
    downloadOptions.value.rangeStart = props.timeRange.rangeStart
    downloadOptions.value.rangeEnd = props.timeRange.rangeEnd
  } else {
    const Day = 24 * 60 * 60 * 1000
    const { left, right } = timeWindowFromTimeRange(props.timeRange || ({} as TimeRange))
    downloadOptions.value.rangeStart = getLocalDateIsoStringFromTimestamp(left)
    downloadOptions.value.rangeEnd = getLocalDateIsoStringFromTimestamp(right + Day)
  }
  downloadOptions.value.show = true
}

const downloadOptions_OK = (): void => {
  downloadOptions.value.show = false
  downloadFile()
}

const downloadFile = async (): Promise<void> => {
  const type = downloadOptions.value.fileType
  const extension = type === 'CSV' ? '.csv' : '.xlsx'
  const visibility = dyGraph.value.visibility()

  const agg = {
    Agg: downloadOptions.value.aggregation,
    ResolutionCount: downloadOptions.value.resolutionCount,
    ResolutionUnit: downloadOptions.value.resolutionUnit,
    SkipEmptyIntervals: downloadOptions.value.skipEmptyIntervals || downloadOptions.value.simbaFormat,
  }

  const hasAgg = agg.Agg !== 'None'

  const timeRange: TimeRange = {
    type: 'Range',
    lastCount: 0,
    lastUnit: 'Minutes',
    rangeStart: downloadOptions.value.rangeStart,
    rangeEnd: downloadOptions.value.rangeEnd,
  }

  const para = {
    timeRange: timeRange,
    variables: variables.value.filter((x, i) => visibility[i]),
    variableNames: items.value.filter((x, i) => visibility[i]).map((it) => it.Name),
    fileType: type,
    simbaFormat: downloadOptions.value.simbaFormat,
    aggregation: hasAgg ? agg : null,
  }
  try {
    const blobResponse = await props.backendAsync('DownloadFile', para, 'blob')
    downloadBlob(blobResponse, 'HistoryData' + extension)
  } catch (err: any) {
    alert(err.message)
  }
}

const downloadBlob = (blob: Blob, filename: string): void => {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename || 'download'
  a.click()
  setTimeout(() => {
    URL.revokeObjectURL(url)
  }, 500)
}

// Watchers
watch(
  () => props.configVariables?.VarValues,
  () => {
    const resolveMap = stringWithVarResolvedMap.value

    const anyChanges = items.value.some((it) => {
      const obj = it.Variable.Object
      const objResolved = model.VariableReplacer.replaceVariables(obj, props.configVariables?.VarValues || {})
      const objChanged = !resolveMap.has(obj) || resolveMap.get(obj) !== objResolved

      const name = it.Name
      const nameResolved = model.VariableReplacer.replaceVariables(name, props.configVariables?.VarValues || {})
      const nameChanged = !resolveMap.has(name) || resolveMap.get(name) !== nameResolved

      return objChanged || nameChanged
    })

    if (anyChanges) {
      onLoadData(true)
    }
  },
  { deep: true },
)

watch(
  () => props.dateWindow,
  () => {
    applyDateWindow()
  },
)

watch(
  () => props.resize,
  () => {
    const theGraphValue = dyGraph.value
    if (theGraphValue) {
      const fn = () => {
        theGraphValue.resize()
      }
      setTimeout(fn, 500)
    }
  },
)

watch(
  () => props.timeRange,
  () => {
    onLoadData(true)
  },
)

watch(
  () => props.eventPayload,
  () => {
    if (props.eventName === 'DataAppend') {
      const payload: {
        Data: string
        WindowLeft: number
        WindowRight: number
        DataRevision: number
      } = props.eventPayload as any

      if (payload.DataRevision !== dataRevision.value) {
        return
      }
      const newData: any[][] = JSON.parse(payload.Data)
      if (newData.length === 0) {
        return
      }

      convertTimestamps(newData)

      const data = historyData.value

      if (data.length > 0 && data[0].length !== newData[0].length) {
        return
      }

      const hasData = (entry: any[]) => {
        for (let k = 1; k < entry.length; ++k) {
          if (entry[k] !== null) {
            return true
          }
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

      sliceDataToDateWindow(data, payload.WindowLeft, payload.WindowRight)
    }
  },
)

// Initialize data early (equivalent to Vue 2 created() hook)
const { left, right } = timeWindowFromTimeRange(props.timeRange || ({} as TimeRange))
const initialData: any[] = []
sliceDataToDateWindow(initialData, left, right)
historyData.value = initialData

// Lifecycle
onMounted(() => {
  const id = dyGraphID.value
  if (id) {
    ;(window as any)['changeVarVisibility_' + id] = (elementID: string, index: number) => {
      const checkBox: any = document.getElementById(elementID)
      const graphInstance = dyGraph.value
      if (graphInstance) {
        graphInstance.setVisibility(index, checkBox.checked)
      }
    }
  }

  onLoadData(false)

  canUpdateConfig.value = (window.parent as any)['dashboardApp'].canUpdateViewConfig()
})

// Cleanup on unmount
onBeforeUnmount(() => {
  const id = dyGraphID.value
  if (id) {
    delete (window as any)['changeVarVisibility_' + id]
  }
})
</script>
