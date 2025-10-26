<template>
  <div>
    <div @contextmenu="onContextMenu">
      <xy-plot-scatter-dygraph
        :plotWidth="plotWidth"
        :plotHeight="plotHeight"
        :config="config"
        :series="data"
      />

      <div
        v-if="loading"
        style="text-align: center; padding: 20px"
      >
        Loading...
      </div>
      <div
        v-if="error"
        style="color: red; padding: 10px"
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
        <v-list-item @click="onRefresh">
          <v-list-item-title>Refresh Data</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <xy-plot-config-dlg ref="configDialog"></xy-plot-config-dlg>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import type { TimeRange } from '../../utils'
import xyPlotConfigDlg from './xyPlotConfigDlg.vue'
import xyPlotScatter from './xyPlotScatter.vue'
import xyPlotScatterDygraph from './xyPlotScatterDygraph.vue'
import type { ObjectMap } from './common'
import type { XyPlotConfig, XySeriesData } from './xyPlotTypes'

interface LoadDataResponse {
  Series: XySeriesData[]
}

const props = defineProps<{
  id: string
  width: string
  height: string
  config: XyPlotConfig
  backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  eventName: string
  eventPayload: object
  timeRange: TimeRange
  resize: number
  dateWindow: number[] | null
}>()

const loading = ref(false)
const error = ref('')
const data = ref<XySeriesData[]>([])
const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})
const configDialog = ref<InstanceType<typeof xyPlotConfigDlg> | null>(null)

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

    data.value = response.Series || []

    // print number of points loaded for each series:
    data.value.forEach((series) => {
      console.log(`Series "${series.Name}": ${series.Points.length} points`)
    })
  } 
  catch (err: any) {
    error.value = err.message || 'Failed to load data'
  } 
  finally {
    loading.value = false
  }
}

const onContextMenu = (e: MouseEvent): void => {
  e.preventDefault()
  e.stopPropagation()
  contextMenu.value.show = false
  contextMenu.value.clientX = e.clientX
  contextMenu.value.clientY = e.clientY
  setTimeout(() => {
    contextMenu.value.show = true
  }, 10)
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
          alert(err.message || 'Failed to save configuration')
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
  () => props.resize,
  () => {
    // Trigger re-render when widget is resized
  },
)

onMounted(() => {
  nextTick().then(() => {
    loadData()
  })
})
</script>
