<template>
  <div>
    <div
      @contextmenu="onContextMenu"
      :style="{ width: tableWidth, height: tableHeight, overflow: 'auto' }"
    >
      <v-table
        v-show="!loading && !error"
        density="compact"
        fixed-header
        :height="tableHeight"
      >
        <thead style="position: sticky; top: 0; z-index: 2; background: rgb(var(--v-theme-surface))">
          <tr>
            <th
              class="text-left"
              style="font-size: 14px; position: sticky; left: 0; background: rgb(var(--v-theme-surface)); z-index: 3"
            >
              Time Period
            </th>
            <th
              v-for="seriesName in seriesNames"
              :key="seriesName"
              class="text-right"
              style="font-size: 14px"
            >
              {{ seriesName }}
            </th>
            <th
              v-if="config.TableConfig.ShowTotalColumn"
              class="text-right"
              style="font-size: 14px; font-weight: bold"
            >
              {{ config.TableConfig.TotalColumnAggregation || 'Sum' }}
            </th>
          </tr>
        </thead>
        <tbody>
          <template
            v-for="(row, rowIndex) in displayedRows"
            :key="rowIndex"
          >
            <tr
              :style="getRowStyle(row)"
              @mouseenter="hoveredRow = rowIndex"
              @mouseleave="hoveredRow = null"
            >
              <td
                class="text-left"
                style="font-size: 14px; position: sticky; left: 0; background: rgb(var(--v-theme-surface)); z-index: 1"
              >
                <span :style="{ paddingLeft: `${row.Level * 20}px`, display: 'inline-flex', alignItems: 'center' }">
                  <v-icon
                    v-if="row.CanExpand"
                    size="small"
                    @click="toggleExpand(row, rowIndex)"
                    style="cursor: pointer; margin-right: 4px"
                  >
                    {{ row.IsExpanded ? 'mdi-minus-box-outline' : 'mdi-plus-box-outline' }}
                  </v-icon>
                  <span v-else style="margin-left: 24px"></span>
                  {{ formatRowLabel(row) }}
                </span>
              </td>
              <td
                v-for="(value, valueIndex) in row.Values"
                :key="valueIndex"
                class="text-right"
                style="font-size: 14px"
              >
                {{ formatValue(value) }}
              </td>
              <td
                v-if="config.TableConfig.ShowTotalColumn"
                class="text-right"
                style="font-size: 14px; font-weight: bold"
              >
                {{ formatValue(calculateRowTotal(row.Values)) }}
              </td>
            </tr>
          </template>
          <tr
            v-if="config.TableConfig.ShowTotalRow && totalRow"
            style="border-top: 2px solid #666; font-weight: bold"
          >
            <td
              class="text-left"
              style="font-size: 14px; position: sticky; left: 0; background: rgb(var(--v-theme-surface)); z-index: 1"
            >
              Total
            </td>
            <td
              v-for="(value, valueIndex) in totalRow"
              :key="valueIndex"
              class="text-right"
              style="font-size: 14px"
            >
              {{ formatValue(value) }}
            </td>
            <td
              v-if="config.TableConfig.ShowTotalColumn"
              class="text-right"
              style="font-size: 14px"
            >
              {{ formatValue(calculateRowTotal(totalRow)) }}
            </td>
          </tr>
        </tbody>
      </v-table>
      <div
        v-show="loading"
        class="text-center pa-4"
      >
        Loading...
      </div>
      <div
        v-show="error"
        class="text-red pa-4"
      >
        {{ error }}
      </div>
    </div>

    <v-menu
      v-model="contextMenu.show"
      :target="[contextMenu.clientX, contextMenu.clientY]"
    >
      <v-list>
        <v-list-item @click="onConfigure">
          <v-list-item-title>Configure...</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>
    <time-aggregated-table-config-dlg ref="configDialog"></time-aggregated-table-config-dlg>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import type { TimeRange } from '../../utils'
import type { ObjectMap } from './common'
import type {
  TimeAggregatedTableConfig,
  TimeAggregatedTableRow,
  LoadDataResponse,
  LoadChildDataResponse,
} from './TimeAggregatedTableTypes'
import TimeAggregatedTableConfigDlg from './TimeAggregatedTableConfigDlg.vue'
import { formatTimeLabel } from './dateFormatUtils'

const props = defineProps<{
  id: string
  width: string
  height: string
  config: TimeAggregatedTableConfig
  backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  eventName: string
  eventPayload: object
  timeRange: TimeRange
  resize: number
  dateWindow: number[] | null
}>()

const canUpdateConfig = ref(false)
const loading = ref(false)
const error = ref('')
const rows = ref<TimeAggregatedTableRow[]>([])
const seriesNames = ref<string[]>([])
const totalRow = ref<(number | null)[] | null>(null)
const hoveredRow = ref<number | null>(null)
const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})
const configDialog = ref<InstanceType<typeof TimeAggregatedTableConfigDlg> | null>(null)

const tableWidth = computed(() => {
  if (!props.width || props.width === '') {
    return '100%'
  }
  return props.width
})

const tableHeight = computed(() => {
  if (!props.height || props.height === '') {
    return '400px'
  }
  return props.height
})

const hasAnyData = (row: TimeAggregatedTableRow): boolean => {
  return row.Values.some(value => value !== null && value !== undefined)
}

const displayedRows = computed(() => {
  const result: TimeAggregatedTableRow[] = []

  const processRow = (row: TimeAggregatedTableRow) => {
    if (hasAnyData(row)) {
      result.push(row)
    }
    if (row.IsExpanded && row.Children && row.Children.length > 0) {
      row.Children.forEach(processRow)
    }
  }

  rows.value.forEach(processRow)
  return result
})

const formatValue = (value: number | null | undefined): string => {
  if (value === null || value === undefined) {
    return '--'
  }
  const fractionDigits = props.config.TableConfig.FractionDigits ?? 2
  return value.toFixed(fractionDigits)
}

const formatRowLabel = (row: TimeAggregatedTableRow): string => {
  return formatTimeLabel(
    row.StartTime,
    row.Granularity,
    row.Level,
    props.config.TableConfig.WeekStart
  )
}

const calculateRowTotal = (values: (number | null)[] | null): number | null => {
  if (!values || values.length === 0) {
    return null
  }

  const validValues = values.filter(v => v !== null && v !== undefined) as number[]

  if (validValues.length === 0) {
    return null
  }

  const aggregation = props.config.TableConfig.TotalColumnAggregation || 'Sum'

  switch (aggregation) {
    case 'Sum':
      return validValues.reduce((acc, val) => acc + val, 0)
    case 'Average':
      return validValues.reduce((acc, val) => acc + val, 0) / validValues.length
    case 'Min':
      return Math.min(...validValues)
    case 'Max':
      return Math.max(...validValues)
    default:
      return validValues.reduce((acc, val) => acc + val, 0) // fallback to Sum
  }
}

const getRowStyle = (row: TimeAggregatedTableRow) => {
  const baseStyle: any = {
    height: '36px',
  }

  if (row.Level === 1) {
    baseStyle.backgroundColor = 'rgba(0, 0, 0, 0.05)'
  } else if (row.Level === 2) {
    baseStyle.backgroundColor = 'rgba(0, 0, 0, 0.08)'
  }

  const rowIndex = displayedRows.value.indexOf(row)
  if (hoveredRow.value === rowIndex) {
    baseStyle.backgroundColor = 'rgba(0, 0, 0, 0.1)'
  }

  return baseStyle
}

const toggleExpand = async (row: TimeAggregatedTableRow, rowIndex: number): Promise<void> => {
  if (!row.CanExpand) return

  if (row.IsExpanded) {
    // Collapse
    row.IsExpanded = false
    row.Children = []
  } else {
    // Expand - load child data
    if (!row.Children || row.Children.length === 0) {
      try {

        const response: LoadChildDataResponse = await props.backendAsync('LoadChildData', {
          level: row.Level,
          startTime: row.StartTime,
          endTime: row.EndTime,
          configVars: {},
        })

        row.Children = response.Rows.map((childRow) => ({
          ...childRow,
          IsExpanded: false,
          Children: [],
        }))
      } catch (err: any) {
        error.value = err?.message || 'Failed to load child data'
        return
      }
    }
    row.IsExpanded = true
  }
}

const loadData = async (): Promise<void> => {
  if (loading.value) {
    console.log('Data load already in progress, skipping.')
    return
  }

  loading.value = true
  error.value = ''

  try {
    const response: LoadDataResponse = await props.backendAsync('LoadData', {
      timeRange: props.timeRange,
      configVars: {},
    })

    rows.value = response.Rows.map((row) => ({
      ...row,
      IsExpanded: false,
      Children: [],
    }))

    seriesNames.value = response.SeriesNames
    totalRow.value = response.TotalRow ?? null

    console.log(`Loaded ${response.Rows.length} rows with ${response.SeriesNames.length} series`)
  } catch (err: any) {
    error.value = err?.message || 'Failed to load data'
    rows.value = []
    seriesNames.value = []
    totalRow.value = null
  } finally {
    loading.value = false
  }
}

const onContextMenu = (e: MouseEvent): void => {
  if (canUpdateConfig.value) {
    e.preventDefault()
    e.stopPropagation()
    contextMenu.value.show = false
    contextMenu.value.clientX = e.clientX
    contextMenu.value.clientY = e.clientY
    setTimeout(() => {
      contextMenu.value.show = true
    }, 10)
  }
}

const onConfigure = async (): Promise<void> => {
  try {
    const response: any = await props.backendAsync('GetItemsData', {})

    const objectMap: ObjectMap = response?.ObjectMap || {}
    const modules = response?.Modules || []

    if (configDialog.value) {
      const result = await configDialog.value.open(props.config, objectMap, modules)

      if (result) {
        try {
          await props.backendAsync('SaveConfig', { config: result })
          await loadData()
        } catch (err: any) {
          alert(err?.message || 'Failed to save configuration')
        }
      }
    }
  } catch (err: any) {
    console.error('Failed to load items data:', err)
  }
}

watch(
  () => props.timeRange,
  () => {
    loadData()
  },
  { deep: true },
)

watch(
  () => props.config,
  () => {
    loadData()
  },
  { deep: true },
)

onMounted(() => {
  nextTick().then(() => {
    loadData()
  })
  canUpdateConfig.value = (window.parent as any)['dashboardApp'].canUpdateViewConfig()
})
</script>
