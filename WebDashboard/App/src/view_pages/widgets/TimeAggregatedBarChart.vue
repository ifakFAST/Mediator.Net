<template>
  <div>
    <div
      @contextmenu="onContextMenu"
      :style="{ width: plotWidth, height: plotHeight }"
    >
      <Bar
        v-if="!loading && !error && chartData"
        :data="chartData"
        :options="chartOptions"
        :style="{ width: '100%', height: '100%' }"
      />
      <div
        v-if="loading"
        class="text-center pa-4"
      >
        Loading...
      </div>
      <div
        v-if="error"
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
    <time-aggregated-bar-chart-config-dlg ref="configDialog"></time-aggregated-bar-chart-config-dlg>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import { Bar } from 'vue-chartjs'
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  BarElement,
  CategoryScale,
  LinearScale,
  type ChartData,
  type ChartOptions,
  type Plugin,
} from 'chart.js'
import type { TimeRange } from '../../utils'
import type { ObjectMap } from './common'
import type { TimeAggregatedBarChartConfig, LoadDataResponse } from './TimeAggregatedBarChartTypes'
import TimeAggregatedBarChartConfigDlg from './TimeAggregatedBarChartConfigDlg.vue'

const totalLabelPlugin: Plugin<'bar'> = {
  id: 'totalLabel',
  afterDatasetsDraw(chart) {
    const datasets = chart.data.datasets
    if (!datasets || datasets.length === 0) return

    const ctx = chart.ctx
    const lastDatasetIndex = datasets.length - 1
    const lastMeta = chart.getDatasetMeta(lastDatasetIndex)

    lastMeta.data.forEach((bar, index) => {
      const total = datasets.reduce((sum, ds) => {
        const value = ds.data[index] as number | undefined
        return sum + (typeof value === 'number' ? value : 0)
      }, 0)

      if (!Number.isFinite(total)) {
        return
      }

      ctx.save()
      ctx.fillStyle = '#000'
      ctx.font = 'bold 12px sans-serif'
      ctx.textAlign = 'center'
      ctx.textBaseline = 'bottom'
      ctx.fillText(total.toFixed(0), bar.x, bar.y - 4)
      ctx.restore()
    })
  },
}

ChartJS.register(Title, Tooltip, Legend, BarElement, CategoryScale, LinearScale, totalLabelPlugin)

const props = defineProps<{
  id: string
  width: string
  height: string
  config: TimeAggregatedBarChartConfig
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
const chartData = ref<ChartData<'bar'> | null>(null)
const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})
const configDialog = ref<InstanceType<typeof TimeAggregatedBarChartConfigDlg> | null>(null)

const plotWidth = computed(() => {
  if (!props.width || props.width === '') {
    return '100%'
  }
  return props.width
})

const plotHeight = computed(() => {
  if (!props.height || props.height === '') {
    return '300px'
  }
  return props.height
})

const chartOptions = computed<ChartOptions<'bar'>>(() => ({
  responsive: true,
  maintainAspectRatio: false,
  scales: {
    x: {
      stacked: true,
      ticks: {
        autoSkip: true,
        maxRotation: 45,
        minRotation: 0,
      },
    },
    y: {
      stacked: true,
      beginAtZero: true,
      ticks: {
        precision: 0,
      },
      title: {
        display: false,
      },
    },
  },
  plugins: {
    legend: {
      display: true,
      position: 'top',
    },
    tooltip: {
      mode: 'index',
      intersect: false,
    },
  },
}))

const onRefresh = async (): Promise<void> => {
  await loadData()
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

    chartData.value = {
      labels: response.Labels,
      datasets: response.Series.map((series) => ({
        label: series.Name,
        data: series.Values,
        backgroundColor: series.Color || '#1BA1E2',
        borderColor: series.Color || '#1BA1E2',
        borderWidth: 1,
        stack: 'stacked-bars',
      })),
    }

    console.log(`Loaded ${response.Series.length} series with ${response.Labels.length} time buckets`)
  } catch (err: any) {
    error.value = err?.message || 'Failed to load data'
    chartData.value = null
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
