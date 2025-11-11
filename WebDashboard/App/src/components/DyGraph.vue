<script setup lang="ts">
import { ref, watch, onMounted, nextTick, onBeforeUnmount } from 'vue'
import Dygraph from 'dygraphs'
import 'dygraphs/dist/dygraph.css'
import MyAnnotations from '@/plugins/MyAnnotations'

const DygraphAny = Dygraph as any
if (DygraphAny.PLUGINS && DygraphAny.Plugins) {
  DygraphAny.PLUGINS = DygraphAny.PLUGINS.map((plugin: any) => {
    if (plugin === DygraphAny.Plugins.Annotations) {
      console.log('Replaced built-in DyGraphs Annotations plugin with MyAnnotations')
      return MyAnnotations
    }
    return plugin
  })
}

// Props
interface Props {
  graphData: any
  graphOptions: any
  graphStyle?: any
  graphResetZoom?: any
}

const props = withDefaults(defineProps<Props>(), {
  graphStyle: () => ({ width: '100%', height: '600px' }),
  graphResetZoom: 0,
})

// Generate unique ID using a more robust approach
const uid = ref(Date.now().toString() + Math.random().toString(36).substr(2, 9))
const graph = ref<any>(null)
const needFullUpdate = ref(false)
const id = ref('')

// Methods
const deepCopy = (obj: any) => {
  const str = JSON.stringify(obj)
  const copy = JSON.parse(str)
  copy.legendFormatter = obj.legendFormatter
  copy.zoomCallback = obj.zoomCallback
  copy.underlayCallback = obj.underlayCallback
  copy.drawPointCallback = obj.drawPointCallback
  return copy
}

const updateGraph = () => {
  if (graph.value !== null) {
    const obj = Object.assign({}, deepCopy(props.graphOptions), { file: props.graphData })
    graph.value.updateOptions(obj)
  }
}

const columnMatch = (data: any[][], options: any): boolean => {
  if (data.length === 0) return true
  const columns = data[0].length
  return columns === options.labels.length
}

// Watchers
watch(
  () => props.graphStyle,
  () => {
    if (graph.value !== null) {
      const gr = graph.value
      nextTick(() => {
        gr.resize()
      })
    }
  },
)

watch(
  () => props.graphData,
  (val, oldVal) => {
    if (val.length > 0 && oldVal.length > 0 && val[0].length !== oldVal[0].length) {
      if (columnMatch(val, props.graphOptions)) {
        updateGraph()
      } else {
        needFullUpdate.value = true
      }
      return
    }

    if (graph.value !== null) {
      const zoomed = graph.value.isZoomed('y')

      const axesValueRange = {
        y: {
          valueRange: null,
        },
        y2: {
          valueRange: null,
        },
      }

      if (zoomed) {
        const ranges = graph.value.yAxisRanges()
        axesValueRange.y.valueRange = ranges[0]
        axesValueRange.y2.valueRange = ranges[1]
      }

      graph.value.updateOptions({
        file: val,
        axes: axesValueRange,
      })
    }
  },
)

watch(
  () => props.graphOptions,
  (val) => {
    if (columnMatch(props.graphData, val)) {
      if (needFullUpdate.value) {
        needFullUpdate.value = false
        updateGraph()
      } else {
        graph.value.updateOptions(deepCopy(val))
      }
    }
  },
)

watch(
  () => props.graphResetZoom,
  () => {
    if (graph.value !== null) {
      graph.value.resetZoom()
    }
  },
)

// Lifecycle
onMounted(() => {
  const graphId = 'vue_dygraphs_' + uid.value
  id.value = graphId
  graph.value = new Dygraph(graphId, props.graphData, deepCopy(props.graphOptions))
})

onBeforeUnmount(() => {
  if (graph.value) {
    graph.value.destroy()
  }
})

// Expose _data for compatibility
defineExpose({
  _data: {
    graph: graph,
    id: uid,
  },
})
</script>

<template>
  <div
    :id="'vue_dygraphs_' + uid"
    :style="graphStyle"
  ></div>
</template>

<style>
.dygraph-legend {
  background-color: transparent !important;
  left: 80px !important;
  width: auto !important;
}

.dygraph-legend-line {
  border-bottom-width: 12px !important;
  bottom: 0px !important;
}
</style>
