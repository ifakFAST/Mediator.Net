<template>
  <div>
    <v-dialog
      v-model="showDialog"
      max-width="950px"
      persistent
    >
      <v-card>
        <v-card-title>Configure</v-card-title>
        <v-card-text>
          <v-tabs v-model="configTab">
            <v-tab value="series">Data Series</v-tab>
            <v-tab value="plot">Plot Settings</v-tab>
          </v-tabs>

          <v-window v-model="configTab">
            <v-window-item value="series">
              <table style="width: 100%; border-collapse: collapse; margin-top: 16px">
                <thead>
                  <tr>
                    <th style="text-align: left">Name</th>
                    <th>Color</th>
                    <th>&nbsp;</th>
                    <th>Object Name</th>
                    <th>&nbsp;</th>
                    <th>Variable</th>
                    <th>Aggregation</th>
                    <th>&nbsp;</th>
                    <th>&nbsp;</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="(item, idx) in seriesItems"
                    :key="idx"
                  >
                    <td>
                      <v-text-field 
                        v-model="item.Name"
                        style="min-width: 20ch"></v-text-field>
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
                            v-for="(color, index) in colorList"
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
                        class="mr-2"
                      ></v-text-field>
                    </td>
                    <td style="font-size: 14px; overflow-wrap: anywhere">
                      {{ getObjectName(item.Variable.Object) }}
                    </td>
                    <td>
                      <v-btn
                        icon="mdi-pencil"
                        size="small"
                        variant="text"
                        @click="selectObjectForSeries(item)"
                      ></v-btn>
                    </td>
                    <td>
                      <v-combobox
                        v-model="item.Variable.Name"
                        :items="getVariablesForObject(item.Variable.Object)"
                        :disabled="getVariablesForObject(item.Variable.Object).length === 0"
                        style="width: 12ch"
                      ></v-combobox>
                    </td>
                    <td>
                      <v-select
                        v-model="item.Aggregation"
                        :items="['Average', 'Sum', 'Min', 'Max', 'Count', 'First', 'Last']"
                        style="width: 12ch; margin-left: 1ex"
                      ></v-select>
                    </td>
                    <td>
                      <v-btn
                        icon="mdi-delete"
                        size="small"
                        variant="text"
                        @click="deleteSeries(idx)"
                      ></v-btn>
                    </td>
                    <td>
                      <v-btn
                        v-if="idx > 0"
                        icon="mdi-chevron-up"
                        size="small"
                        variant="text"
                        @click="moveSeriesUp(idx)"
                      ></v-btn>
                    </td>
                  </tr>
                  <tr>
                    <td colspan="7">&nbsp;</td>
                    <td>
                      <v-btn
                        icon="mdi-plus"
                        size="small"
                        variant="text"
                        @click="addSeries"
                      ></v-btn>
                    </td>
                    <td>&nbsp;</td>
                  </tr>
                </tbody>
              </table>
            </v-window-item>

            <v-window-item value="plot">
              <v-container>
                <v-row>
                  <v-col cols="4">
                    <v-text-field
                      v-model="configData.ChartConfig.StartTime"
                      label="Start Time"
                      type="datetime-local"
                      hint="Format: YYYY-MM-DDTHH:mm:ss"
                    ></v-text-field>
                  </v-col>
                  <v-col cols="4">
                    <v-text-field
                      v-model="configData.ChartConfig.EndTime"
                      label="End Time (optional)"
                      type="datetime-local"
                      hint="Leave empty to use current time"
                      clearable
                    ></v-text-field>
                  </v-col>
                </v-row>

                <v-row>
                  <v-col cols="4">
                    <v-select
                      v-model="configData.ChartConfig.TimeGranularity"
                      label="Time Granularity"
                      :items="timeGranularityOptions"
                    ></v-select>
                  </v-col>
                  <v-col cols="4" v-if="configData.ChartConfig.TimeGranularity === 'Weekly'">
                    <v-select
                      v-model="configData.ChartConfig.WeekStart"
                      label="Week Start Day"
                      :items="weekStartOptions"
                    ></v-select>
                  </v-col>
                </v-row>

              </v-container>
            </v-window-item>
          </v-window>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn
            color="grey-darken-1"
            variant="text"
            @click="cancel"
          >
            Cancel
          </v-btn>
          <v-btn
            color="primary-darken-1"
            variant="text"
            :disabled="isSaveDisabled"
            @click="save"
          >
            Save
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <dlg-object-select
      v-model="selectObject.show"
      :allow-config-variables="true"
      :module-id="selectObject.selectedModuleID"
      :modules="selectObject.modules"
      :object-id="selectObject.selectedObjectID"
      @onselected="onObjectSelected"
    ></dlg-object-select>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import DlgObjectSelect from '../../components/DlgObjectSelect.vue'
import type { ModuleInfo, Obj, ObjectMap } from './common'
import type {
  TimeAggregatedBarChartConfig,
  TimeAggregatedBarChartDataSeries,
  BarAggregationOption,
  WeekStartOption,
  TimeGranularityOption,
} from './TimeAggregatedBarChartTypes'

const createDefaultConfig = (): TimeAggregatedBarChartConfig => ({
  ChartConfig: {
    StartTime: '',
    EndTime: null,
    TimeGranularity: 'Monthly',
    WeekStart: 'Monday',
  },
  DataSeries: [],
})

const showDialog = ref(false)
const configTab = ref('series')
const colorList = ref([
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
])

const timeGranularityOptions: TimeGranularityOption[] = ['Yearly', 'Monthly', 'Quarterly', 'Weekly', 'Daily']

const weekStartOptions: WeekStartOption[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

const pad = (num: number): string => num.toString().padStart(2, '0')

const toDateTimeLocal = (value: string | null | undefined): string => {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(
    date.getMinutes(),
  )}:${pad(date.getSeconds())}`
}

const toIsoUtc = (value: string | null | undefined): string | null => {
  if (value == null) return null
  const trimmed = value.trim()
  if (trimmed === '') return null
  const date = new Date(trimmed)
  if (Number.isNaN(date.getTime())) {
    return trimmed
  }
  return date.toISOString()
}

const objectMap = ref<ObjectMap>({})
const configData = ref<TimeAggregatedBarChartConfig>(createDefaultConfig())
const seriesItems = ref<TimeAggregatedBarChartDataSeries[]>([])
const selectObject = ref({
  show: false,
  modules: [] as ModuleInfo[],
  selectedModuleID: '',
  selectedObjectID: '',
})

const isSaveDisabled = computed(() => {
  const startTime = configData.value.ChartConfig.StartTime?.trim()
  return seriesItems.value.length === 0 || !startTime
})

let resolveDialog: ((value: TimeAggregatedBarChartConfig | null) => void) | null = null
let currentEditingSeries: TimeAggregatedBarChartDataSeries | null = null

const cloneSeries = (items: TimeAggregatedBarChartDataSeries[]): TimeAggregatedBarChartDataSeries[] =>
  items.map((item) => ({
    Name: item.Name,
    Color: item.Color,
    Aggregation: item.Aggregation as BarAggregationOption,
    Variable: {
      Object: item.Variable.Object,
      Name: item.Variable.Name,
    },
  }))

const open = (
  config: TimeAggregatedBarChartConfig,
  objectMapData: ObjectMap,
  modules: ModuleInfo[],
): Promise<TimeAggregatedBarChartConfig | null> => {
  return new Promise((resolve) => {
    const normalizedConfig = JSON.parse(JSON.stringify(config ?? createDefaultConfig())) as TimeAggregatedBarChartConfig

    if (!normalizedConfig.ChartConfig) {
      normalizedConfig.ChartConfig = createDefaultConfig().ChartConfig
    } else {
      normalizedConfig.ChartConfig = {
        StartTime: normalizedConfig.ChartConfig.StartTime ?? '',
        EndTime: normalizedConfig.ChartConfig.EndTime ?? null,
        TimeGranularity: (normalizedConfig.ChartConfig.TimeGranularity || 'Monthly') as TimeGranularityOption,
        WeekStart: (normalizedConfig.ChartConfig.WeekStart || 'Monday') as WeekStartOption,
      }
    }

    normalizedConfig.DataSeries = normalizedConfig.DataSeries ?? []

    configData.value = {
      ChartConfig: {
        ...normalizedConfig.ChartConfig,
        StartTime: toDateTimeLocal(normalizedConfig.ChartConfig.StartTime),
        EndTime: normalizedConfig.ChartConfig.EndTime ? toDateTimeLocal(normalizedConfig.ChartConfig.EndTime) : null,
      },
      DataSeries: normalizedConfig.DataSeries,
    }

    seriesItems.value = cloneSeries(configData.value.DataSeries)
    objectMap.value = objectMapData
    selectObject.value.modules = modules
    selectObject.value.selectedModuleID = ''
    selectObject.value.selectedObjectID = ''
    configTab.value = 'series'
    showDialog.value = true
    resolveDialog = resolve
  })
}

const save = () => {
  const resultConfig: TimeAggregatedBarChartConfig = {
    ChartConfig: {
      ...configData.value.ChartConfig,
      StartTime: toIsoUtc(configData.value.ChartConfig.StartTime) ?? '',
      EndTime: toIsoUtc(configData.value.ChartConfig.EndTime),
    },
    DataSeries: cloneSeries(seriesItems.value),
  }

  showDialog.value = false
  const result = JSON.parse(JSON.stringify(resultConfig)) as TimeAggregatedBarChartConfig
  resolveDialog?.(result)
  resolveDialog = null
  currentEditingSeries = null
  selectObject.value.show = false
}

const cancel = () => {
  showDialog.value = false
  resolveDialog?.(null)
  resolveDialog = null
  currentEditingSeries = null
  selectObject.value.show = false
}

const addSeries = () => {
  const nextIndex = seriesItems.value.length
  seriesItems.value.push({
    Name: `Series ${nextIndex + 1}`,
    Color: colorList.value[nextIndex % colorList.value.length],
    Variable: {
      Object: '',
      Name: 'Value',
    },
    Aggregation: 'Average',
  })
}

const deleteSeries = (index: number) => {
  seriesItems.value.splice(index, 1)
}

const moveSeriesUp = (index: number) => {
  if (index > 0) {
    const temp = seriesItems.value[index - 1]
    seriesItems.value[index - 1] = seriesItems.value[index]
    seriesItems.value[index] = temp
  }
}

const selectObjectForSeries = (series: TimeAggregatedBarChartDataSeries) => {
  currentEditingSeries = series
  const currObject = series.Variable.Object || ''
  let objectForModuleID = currObject

  if (objectForModuleID === '') {
    const nonEmptySeries = seriesItems.value.filter((s) => s.Variable.Object && s.Variable.Object !== '')
    if (nonEmptySeries.length > 0) {
      objectForModuleID = nonEmptySeries[nonEmptySeries.length - 1].Variable.Object
    }
  }

  const colonIndex = objectForModuleID.indexOf(':')
  if (colonIndex <= 0) {
    selectObject.value.selectedModuleID = selectObject.value.modules[0]?.ID ?? ''
  } else {
    selectObject.value.selectedModuleID = objectForModuleID.substring(0, colonIndex)
  }

  selectObject.value.selectedObjectID = currObject
  selectObject.value.show = true
}

const onObjectSelected = (obj: Obj) => {
  if (currentEditingSeries) {
    const variables = obj.Variables || []
    const name = obj.Name
    objectMap.value = {
      ...objectMap.value,
      [obj.ID]: {
        Name: name,
        Variables: variables,
      },
    }
    currentEditingSeries.Variable.Object = obj.ID
    if (variables.length === 1) {
      currentEditingSeries.Variable.Name = variables[0]
    } else if (!variables.includes(currentEditingSeries.Variable.Name)) {
      currentEditingSeries.Variable.Name = ''
    }
  }
  selectObject.value.show = false
  currentEditingSeries = null
}

const getObjectName = (objectId: string): string => {
  if (!objectId) return '(none)'
  if (objectMap.value[objectId]) {
    return objectMap.value[objectId].Name
  }
  const i = objectId.indexOf(':')
  if (i > 0) {
    return objectId.substring(i + 1)
  }
  return objectId
}

const getVariablesForObject = (objectId: string): string[] => {
  if (!objectId || !objectMap.value[objectId]) {
    return []
  }
  return objectMap.value[objectId].Variables
}

defineExpose({
  open,
})
</script>
