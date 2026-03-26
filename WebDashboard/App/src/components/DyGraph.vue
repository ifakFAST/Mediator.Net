<script setup lang="ts">
import { ref, watch, onMounted, nextTick, onBeforeUnmount } from 'vue'
import Dygraph from 'dygraphs'
import 'dygraphs/dist/dygraph.css'
import MyAnnotations from '@/plugins/MyAnnotations'
import { globalState } from '@/global'

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
const deepCopy = (obj: any): any => {
  if (obj === null || typeof obj !== 'object') {
    return obj
  }

  if (Array.isArray(obj)) {
    return obj.map((item) => deepCopy(item))
  }

  const copy: Record<string, any> = {}
  for (const [key, value] of Object.entries(obj)) {
    copy[key] = deepCopy(value)
  }
  return copy
}

const GRAN_SECONDLY = 7
const GRAN_MINUTELY = 12
const GRAN_DAILY = 20
const GRAN_MONTHLY = 23
const GRAN_DECADAL = 27
const SHORT_MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

const tzZeropad = (x: number): string => (x < 10 ? '0' + x : '' + x)

const tzHmsString = (hh: number, mm: number, ss: number, ms: number): string => {
  let ret = tzZeropad(hh) + ':' + tzZeropad(mm)
  if (ss) {
    ret += ':' + tzZeropad(ss)
    if (ms) {
      const str = '' + ms
      ret += '.' + ('000' + str).substring(str.length)
    }
  }
  return ret
}

const getDatePartsInTZ = (date: Date, tz: string) => {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: tz,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  }).formatToParts(date)
  const map: Record<string, string> = {}
  for (const p of parts) map[p.type] = p.value
  const h = parseInt(map.hour)
  return {
    year: parseInt(map.year),
    month: parseInt(map.month) - 1,
    day: parseInt(map.day),
    hours: h === 24 ? 0 : h,
    mins: parseInt(map.minute),
    secs: parseInt(map.second),
    millis: date.getMilliseconds(),
  }
}

const buildTimeZoneOptions = () => {
  const tz = globalState.timeZoneIanaId
  if (!tz) return {}

  const axisLabelFormatter = (date: Date | number, granularity: number) => {
    if (!(date instanceof Date)) return String(date)
    const { year, month, day, hours, mins, secs, millis } = getDatePartsInTZ(date, tz)
    // console.log('axisLabelFormatter', date, granularity, { year, month, day, hours, mins, secs, millis })
    if (granularity >= GRAN_DECADAL) {
      return '' + year
    } 
    else if (granularity >= GRAN_MONTHLY) {
      return SHORT_MONTH_NAMES[month] + '&#160;' + year
    } 
    else {
      const frac = hours * 3600 + mins * 60 + secs + 1e-3 * millis
      if (frac === 0 || granularity >= GRAN_DAILY) {
        return tzZeropad(day) + '&#160;' + SHORT_MONTH_NAMES[month]
      } 
      else if (granularity < GRAN_SECONDLY) {
        const str = '' + millis
        return tzZeropad(secs) + '.' + ('000' + str).substring(str.length)
      } 
      else if (granularity > GRAN_MINUTELY) {
        return tzHmsString(hours, mins, secs, 0)
      } 
      else {
        return tzHmsString(hours, mins, secs, millis)
      }
    }
  }

  const valueFormatter = (millis: number) => {
    if (typeof millis !== 'number') return String(millis)
    const date = new Date(millis)
    const p = getDatePartsInTZ(date, tz)
    const frac = p.hours * 3600 + p.mins * 60 + p.secs + 1e-3 * p.millis
    let ret = p.year + '/' + tzZeropad(p.month + 1) + '/' + tzZeropad(p.day)
    if (frac) {
      ret += ' ' + tzHmsString(p.hours, p.mins, p.secs, p.millis)
    }
    return ret
  }

  return { axes: { x: { axisLabelFormatter, valueFormatter } } }
}

const withTimeZoneOptions = (opts: any): any => {
  const tzOpts = buildTimeZoneOptions()
  if (!tzOpts.axes) return opts
  const merged  = { ...opts }
  merged.axes   = { ...(opts.axes || {}) }
  merged.axes.x = { ...(opts.axes?.x || {}), ...tzOpts.axes.x }
  return merged
}

const updateGraph = () => {
  if (graph.value !== null) {
    const obj = withTimeZoneOptions(
      Object.assign({}, deepCopy(props.graphOptions), { file: props.graphData }),
    )
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
        graph.value.updateOptions(withTimeZoneOptions(deepCopy(val)))
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

watch(
  () => globalState.timeZoneIanaId,
  () => {
    updateGraph()
  },
)

// Lifecycle
onMounted(() => {
  const graphId = 'vue_dygraphs_' + uid.value
  id.value = graphId
  graph.value = new Dygraph(graphId, props.graphData, withTimeZoneOptions(deepCopy(props.graphOptions)))
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

.dygraph-axis-label,
.dygraph-axis-label-x,
.dygraph-axis-label-y,
.dygraph-axis-label-y2,
.dygraph-label,
.dygraph-title,
.dygraph-xlabel,
.dygraph-ylabel,
.dygraph-y2label {
  color: rgb(var(--v-theme-on-surface)) !important;
}
</style>
