<template>
  <svg
    :width="svgWidth"
    :height="svgHeight"
    :style="{ border: '1px solid #ccc' }"
  >
    <!-- Grid lines -->
    <g v-if="config.PlotConfig.ShowGrid">
      <line
        v-for="(line, idx) in gridLines.vertical"
        :key="'v' + idx"
        :x1="line"
        :x2="line"
        :y1="margins.top"
        :y2="svgHeight - margins.bottom"
        stroke="#e0e0e0"
        stroke-width="1"
      />
      <line
        v-for="(line, idx) in gridLines.horizontal"
        :key="'h' + idx"
        :x1="margins.left"
        :x2="svgWidth - margins.right"
        :y1="line"
        :y2="line"
        stroke="#e0e0e0"
        stroke-width="1"
      />
    </g>

    <!-- 45-degree line (X=Y) -->
    <line
      v-if="config.PlotConfig.Show45DegreeLine && line45Degree.visible"
      :x1="line45Degree.x1"
      :y1="line45Degree.y1"
      :x2="line45Degree.x2"
      :y2="line45Degree.y2"
      :stroke="config.PlotConfig.Color45DegreeLine"
      stroke-width="1.5"
      stroke-dasharray="5,5"
      opacity="0.7"
    />

    <!-- Axes -->
    <line
      :x1="margins.left"
      :x2="svgWidth - margins.right"
      :y1="svgHeight - margins.bottom"
      :y2="svgHeight - margins.bottom"
      stroke="black"
      stroke-width="2"
    />
    <line
      :x1="margins.left"
      :x2="margins.left"
      :y1="margins.top"
      :y2="svgHeight - margins.bottom"
      stroke="black"
      stroke-width="2"
    />

    <!-- Axis labels -->
    <text
      :x="(margins.left + svgWidth - margins.right) / 2"
      :y="svgHeight - 5"
      text-anchor="middle"
      font-size="12"
    >
      {{ config.PlotConfig.XAxisName }}
    </text>
    <text
      :x="10"
      :y="(margins.top + svgHeight - margins.bottom) / 2"
      text-anchor="middle"
      font-size="12"
      :transform="`rotate(-90, 10, ${(margins.top + svgHeight - margins.bottom) / 2})`"
    >
      {{ config.PlotConfig.YAxisName }}
    </text>

    <!-- Axis tick labels -->
    <g>
      <text
        v-for="(tick, idx) in xAxisTicks"
        :key="'xt' + idx"
        :x="tick.pos"
        :y="svgHeight - margins.bottom + 15"
        text-anchor="middle"
        font-size="10"
      >
        {{ tick.label }}
      </text>
      <text
        v-for="(tick, idx) in yAxisTicks"
        :key="'yt' + idx"
        :x="margins.left - 10"
        :y="tick.pos"
        text-anchor="end"
        font-size="10"
        dominant-baseline="middle"
      >
        {{ tick.label }}
      </text>
    </g>

    <!-- Data series -->
    <g
      v-for="(series, seriesIdx) in plotData"
      :key="seriesIdx"
    >
      <circle
        v-for="(point, pointIdx) in series.points"
        :key="pointIdx"
        :cx="point.x"
        :cy="point.y"
        :r="series.size"
        :fill="point.color"
        :opacity="0.7"
      >
        <title>{{ point.tooltip }}</title>
      </circle>
    </g>

    <!-- Regression lines -->
    <g
      v-for="(regression, regressionIdx) in regressionLines"
      :key="'reg' + regressionIdx"
    >
      <line
        :x1="regression.x1"
        :y1="regression.y1"
        :x2="regression.x2"
        :y2="regression.y2"
        :stroke="regression.color"
        stroke-width="2"
        opacity="0.8"
      />
    </g>

    <!-- Legend -->
    <g v-if="config.PlotConfig.ShowLegend && legendItems.length > 0">
      <rect
        :x="svgWidth - margins.right - legendWidth"
        :y="margins.top + 10"
        :width="legendWidth"
        :height="legendItems.length * legendEntryHeight + legendPadding * 2"
        fill="white"
        stroke="#ccc"
        stroke-width="1"
        opacity="0.9"
      />
      <g
        v-for="(legendItem, idx) in legendItems"
        :key="'legend' + idx"
        :transform="`translate(${svgWidth - margins.right - legendWidth + legendPadding}, ${margins.top + 10 + legendPadding + idx * legendEntryHeight})`"
        style="cursor: pointer"
        @click.stop="toggleSeries(legendItem.name)"
      >
        <!-- Checkbox -->
        <rect
          x="0"
          y="0"
          :width="legendWidth - legendPadding * 2"
          :height="legendEntryHeight"
          fill="transparent"
        />
        <rect
          x="0"
          y="5"
          width="14"
          height="14"
          :fill="legendItem.visible ? '#fff' : '#f5f5f5'"
          stroke="#666"
          stroke-width="1"
          rx="2"
          ry="2"
        />
        <!-- Checkmark when visible -->
        <path
          v-if="legendItem.visible"
          d="M3 12 L6 15 L11 7"
          stroke="#2e7d32"
          stroke-width="2"
          fill="none"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
        <!-- Series marker circle -->
        <circle
          :cx="26"
          cy="12"
          :r="legendItem.size"
          :fill="legendItem.color"
          :opacity="legendItem.visible ? 1 : 0.3"
        />
        <!-- Series name -->
        <text
          x="36"
          y="12"
          font-size="12"
          dominant-baseline="middle"
          :fill="legendItem.visible ? '#000' : '#888'"
        >
          {{ legendItem.name.substring(0, 20) }}
        </text>
      </g>
    </g>
  </svg>
</template>

<script setup lang="ts">
import { computed, toRefs, ref, watch } from 'vue'
import type { XyPlotConfig, XySeriesData } from './xyPlotTypes'

interface AxisTick {
  pos: number
  label: string
}

interface PlotPoint {
  x: number
  y: number
  color: string
  tooltip: string
}

interface PlotSeries {
  name: string
  color: string
  size: number
  points: PlotPoint[]
}

interface LegendItem {
  name: string
  color: string
  size: number
  visible: boolean
}

const formatNumber = (value: number): string => value.toFixed(2)

const props = defineProps<{
  plotWidth: number
  plotHeight: number
  config: XyPlotConfig
  series: XySeriesData[]
}>()

const { plotWidth: svgWidth, plotHeight: svgHeight, config, series } = toRefs(props)

// Define margins internally
const margins = ref({ top: 20, right: 20, bottom: 50, left: 60 })

const seriesVisibility = ref<Record<string, boolean>>({})

const isSeriesVisible = (name: string): boolean => {
  const visibility = seriesVisibility.value
  return visibility[name] !== false
}

watch(
  series,
  (newSeries) => {
    const visibility = { ...seriesVisibility.value }
    let changed = false

    Object.keys(visibility).forEach((name) => {
      if (!newSeries.some((item) => item.Name === name)) {
        delete visibility[name]
        changed = true
      }
    })

    newSeries.forEach((item) => {
      if (!(item.Name in visibility)) {
        visibility[item.Name] = item.Checked
        changed = true
      }
    })

    if (changed) {
      seriesVisibility.value = visibility
    }
  },
  { immediate: true },
)

const toggleSeries = (name: string): void => {
  const visibility = { ...seriesVisibility.value }
  visibility[name] = !isSeriesVisible(name)
  seriesVisibility.value = visibility
}

const activeSeries = computed<XySeriesData[]>(() => series.value.filter((seriesItem) => isSeriesVisible(seriesItem.Name)))

const legendItems = computed<LegendItem[]>(() =>
  series.value.map((seriesItem) => ({
    name: seriesItem.Name,
    color: seriesItem.Color,
    size: seriesItem.Size,
    visible: isSeriesVisible(seriesItem.Name),
  })),
)

const legendWidth = 180
const legendEntryHeight = 20
const legendPadding = 9

const xExtent = computed<[number, number]>(() => {
  const cfg = config.value.PlotConfig

  if (cfg.XAxisLimitMin != null && cfg.XAxisLimitMax != null) {
    return [cfg.XAxisLimitMin, cfg.XAxisLimitMax]
  }

  let min = Infinity
  let max = -Infinity

  activeSeries.value.forEach((seriesItem) => {
    seriesItem.Points.forEach((point) => {
      if (point.X < min) min = point.X
      if (point.X > max) max = point.X
    })
  })

  if (min === Infinity) return [0, 1]
  if (cfg.XAxisStartFromZero && min > 0) min = 0

  const range = max - min
  return [min - range * 0.05, max + range * 0.05]
})

const yExtent = computed<[number, number]>(() => {
  const cfg = config.value.PlotConfig

  if (cfg.YAxisLimitMin != null && cfg.YAxisLimitMax != null) {
    return [cfg.YAxisLimitMin, cfg.YAxisLimitMax]
  }

  let min = Infinity
  let max = -Infinity

  activeSeries.value.forEach((seriesItem) => {
    seriesItem.Points.forEach((point) => {
      if (point.Y < min) min = point.Y
      if (point.Y > max) max = point.Y
    })
  })

  if (min === Infinity) return [0, 1]
  if (cfg.YAxisStartFromZero && min > 0) min = 0

  const range = max - min
  return [min - range * 0.05, max + range * 0.05]
})

const xScale = (value: number): number => {
  const [min, max] = xExtent.value
  const range = max - min

  if (range === 0) {
    return margins.value.left + (svgWidth.value - margins.value.left - margins.value.right) / 2
  }

  return margins.value.left + ((value - min) / range) * (svgWidth.value - margins.value.left - margins.value.right)
}

const yScale = (value: number): number => {
  const [min, max] = yExtent.value
  const range = max - min

  if (range === 0) {
    return svgHeight.value - margins.value.bottom - (svgHeight.value - margins.value.top - margins.value.bottom) / 2
  }

  return svgHeight.value - margins.value.bottom - ((value - min) / range) * (svgHeight.value - margins.value.top - margins.value.bottom)
}

const plotData = computed<PlotSeries[]>(() =>
  activeSeries.value.map((seriesItem) => ({
    name: seriesItem.Name,
    color: seriesItem.Color,
    size: seriesItem.Size,
    points: seriesItem.Points.map((point) => ({
      x: xScale(point.X),
      y: yScale(point.Y),
      color: point.Color || seriesItem.Color,
      tooltip: `(${formatNumber(point.X)}, ${formatNumber(point.Y)})`,
    })),
  })),
)

const sectionsX = 5
const sectionsY = 5

const gridLines = computed(() => {
  const vertical: number[] = []
  const horizontal: number[] = []

  const [xMin, xMax] = xExtent.value
  const [yMin, yMax] = yExtent.value

  const xStep = (xMax - xMin) / sectionsX
  const yStep = (yMax - yMin) / sectionsY

  for (let i = 0; i <= sectionsX; i++) {
    vertical.push(xScale(xMin + i * xStep))
  }

  for (let i = 0; i <= sectionsY; i++) {
    horizontal.push(yScale(yMin + i * yStep))
  }

  return { vertical, horizontal }
})

const xAxisTicks = computed<AxisTick[]>(() => {
  const [xMin, xMax] = xExtent.value
  const ticks: AxisTick[] = []
  const step = (xMax - xMin) / sectionsX

  for (let i = 0; i <= sectionsX; i++) {
    const value = xMin + i * step
    ticks.push({
      pos: xScale(value),
      label: formatNumber(value),
    })
  }

  return ticks
})

const yAxisTicks = computed<AxisTick[]>(() => {
  const [yMin, yMax] = yExtent.value
  const ticks: AxisTick[] = []
  const step = (yMax - yMin) / sectionsY

  for (let i = 0; i <= sectionsY; i++) {
    const value = yMin + i * step
    ticks.push({
      pos: yScale(value),
      label: formatNumber(value),
    })
  }

  return ticks
})

const line45Degree = computed(() => {
  const [xMin, xMax] = xExtent.value
  const [yMin, yMax] = yExtent.value

  // Check if the line y=x intersects with the current plot area
  const minVal = Math.max(xMin, yMin)
  const maxVal = Math.min(xMax, yMax)

  // If the ranges don't overlap, the line won't be visible
  if (minVal >= maxVal) {
    return { visible: false, x1: 0, y1: 0, x2: 0, y2: 0 }
  }

  // Calculate the start and end points of the 45-degree line within the plot area

  return {
    visible: true,
    x1: xScale(minVal),
    y1: yScale(minVal),
    x2: xScale(maxVal),
    y2: yScale(maxVal),
  }
})

interface RegressionLine {
  x1: number
  y1: number
  x2: number
  y2: number
  color: string
}

const regressionLines = computed<RegressionLine[]>(() => {
  const lines: RegressionLine[] = []
  const [xMin, xMax] = xExtent.value

  activeSeries.value.forEach((seriesItem) => {
    if (seriesItem.Regression) {
      const regression = seriesItem.Regression
      // Calculate y = slope * x + offset for the start and end of the x range
      const y1 = regression.Slope * xMin + regression.Offset
      const y2 = regression.Slope * xMax + regression.Offset

      lines.push({
        x1: xScale(xMin),
        y1: yScale(y1),
        x2: xScale(xMax),
        y2: yScale(y2),
        color: regression.Color,
      })
    }
  })

  return lines
})
</script>

<style scoped>
svg {
  display: block;
  margin: auto;
}
</style>
