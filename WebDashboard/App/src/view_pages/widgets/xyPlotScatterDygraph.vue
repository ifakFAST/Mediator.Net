<template>
  <div>
    <dy-graph
      ref="theGraph"
      :graph-data="dygraphData"
      :graph-options="dygraphOptions"
      :graph-style="graphStyle"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import DyGraph from '../../components/DyGraph.vue'
import type { XyPlotConfig, XySeriesData } from './xyPlotTypes'
import type Dygraph from 'dygraphs'

const props = defineProps<{
  plotWidth: string
  plotHeight: string
  config: XyPlotConfig
  series: XySeriesData[]
}>()

// Template refs
const theGraph = ref<InstanceType<typeof DyGraph> | null>(null)

// Reactive state for series visibility
const seriesVisibility = ref<Record<string, boolean>>({})

// Initialize visibility from series
watch(
  () => props.series,
  (newSeries) => {
    const visibility = { ...seriesVisibility.value }
    let changed = false

    // Remove visibility entries for series that no longer exist
    Object.keys(visibility).forEach((name) => {
      if (!newSeries.some((item) => item.Name === name)) {
        delete visibility[name]
        changed = true
      }
    })

    // Add or update visibility entries for all series
    newSeries.forEach((item) => {
      if (!(item.Name in visibility)) {
        // New series: initialize with Checked value
        visibility[item.Name] = item.Checked
        changed = true
      } else if (visibility[item.Name] !== item.Checked) {
        // Existing series: update if Checked value changed
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

const isSeriesVisible = (name: string): boolean => {
  const visibility = seriesVisibility.value
  return visibility[name] !== false
}

const activeSeries = computed<XySeriesData[]>(() => props.series.filter((seriesItem) => isSeriesVisible(seriesItem.Name)))

const graphStyle = computed(() => ({
  width: props.plotWidth,
  height: props.plotHeight,
}))

// Transform XY scatter data to Dygraph format
// Use ALL series, not just active ones - visibility is controlled by Dygraph
const dygraphData = computed(() => {
  const data: any[][] = []
  const seriesList = props.series // Use all series, not activeSeries

  if (seriesList.length === 0) {
    return [[0]]
  }

  // Build lookup maps for each series (X value -> Y value)
  const seriesMaps: Map<number, number>[] = []
  const xValuesSet = new Set<number>()

  for (let i = 0; i < seriesList.length; i++) {
    const series = seriesList[i]
    const map = new Map<number, number>()
    const points = series.Points

    for (let j = 0; j < points.length; j++) {
      const point = points[j]
      map.set(point.X, point.Y)
      xValuesSet.add(point.X) // Collect unique X values in same loop
    }

    seriesMaps.push(map)
  }

  const xValues = Array.from(xValuesSet).sort((a, b) => a - b)

  // Build data matrix using O(1) lookups
  for (let i = 0; i < xValues.length; i++) {
    const xValue = xValues[i]
    const row: any[] = [xValue]

    for (let j = 0; j < seriesMaps.length; j++) {
      const yValue = seriesMaps[j].get(xValue)
      row.push(yValue !== undefined ? yValue : null)
    }

    data.push(row)
  }

  return data
})

const dyGraph = computed(() => {
  const gr = theGraph.value
  if (gr) {
    return gr._data.graph.value
  }
  return null
})

const dyGraphID = computed(() => {
  const gr = theGraph.value
  if (gr) {
    return gr._data.id.value
  }
  return undefined
})

// Draw 45-degree line (y = x)
const draw45DegreeLine = (ctx: CanvasRenderingContext2D, g: Dygraph) => {
  const cfg = props.config.PlotConfig
  const xRange = g.xAxisRange()
  const yRange = g.yAxisRange()

  const xMin = xRange[0]
  const xMax = xRange[1]
  const yMin = yRange[0]
  const yMax = yRange[1]

  // Find intersection of y=x with the plot area
  const minVal = Math.max(xMin, yMin)
  const maxVal = Math.min(xMax, yMax)

  if (minVal >= maxVal) {
    return // Line not visible in current view
  }

  // Convert to canvas coordinates
  const x1 = g.toDomXCoord(minVal)
  const y1 = g.toDomYCoord(minVal)
  const x2 = g.toDomXCoord(maxVal)
  const y2 = g.toDomYCoord(maxVal)

  ctx.save()
  ctx.strokeStyle = cfg.Color45DegreeLine
  ctx.lineWidth = 1.5
  ctx.setLineDash([5, 5])
  ctx.globalAlpha = 0.7
  ctx.beginPath()
  ctx.moveTo(x1, y1)
  ctx.lineTo(x2, y2)
  ctx.stroke()
  ctx.restore()
}

// Draw regression line
const drawRegressionLine = (ctx: CanvasRenderingContext2D, g: Dygraph, regression: { Slope: number; Offset: number; Color: string }) => {
  const xRange = g.xAxisRange()
  const xMin = xRange[0]
  const xMax = xRange[1]

  // Calculate y values at x extents: y = slope * x + offset
  const y1 = regression.Slope * xMin + regression.Offset
  const y2 = regression.Slope * xMax + regression.Offset

  // Convert to canvas coordinates
  const x1 = g.toDomXCoord(xMin)
  const canvasY1 = g.toDomYCoord(y1)
  const x2 = g.toDomXCoord(xMax)
  const canvasY2 = g.toDomYCoord(y2)

  ctx.save()
  ctx.strokeStyle = regression.Color
  ctx.lineWidth = 2
  ctx.globalAlpha = 0.8
  ctx.beginPath()
  ctx.moveTo(x1, canvasY1)
  ctx.lineTo(x2, canvasY2)
  ctx.stroke()
  ctx.restore()
}

// Configure Dygraph options
const dygraphOptions = computed(() => {
  const cfg = props.config.PlotConfig
  const seriesList = props.series // Use all series, not activeSeries

  // Build series configuration
  const seriesOptions: any = {}
  seriesList.forEach((series) => {
    seriesOptions[series.Name] = {
      drawPoints: true,
      strokeWidth: 0, // No connecting lines for scatter plot
      pointSize: series.Size,
      color: series.Color,
    }
  })

  // Calculate axis ranges based on visible series only
  const xExtent = calculateXExtent()
  const yExtent = calculateYExtent()

  const axes = {
    x: {
      drawGrid: cfg.ShowGrid,
      valueRange: xExtent,
    },
    y: {
      drawGrid: cfg.ShowGrid,
      valueRange: yExtent,
      independentTicks: true,
    },
  }

  // Legend formatter with checkboxes
  const legendFormatter = (data: any) => {
    const grID = dyGraphID.value
    if (grID === undefined) {
      return ''
    }
    const hasData = data.x != null
    const g = dyGraph.value

    const theSeries = data.series.map((series: any, i: number) => {
      const id = grID + '_varCheck_' + i

      // Get visibility from Dygraph's internal state for accuracy
      const isVisible = g ? g.visibility()[i] : series.isVisible
      const checked = isVisible ? 'checked' : ''
      const label = series.labelHTML

      // Get the original color from our series config
      const originalColor = seriesList[i]?.Color || series.color

      // Create custom dash HTML with original color
      const customDash = `<span style="color: ${originalColor}; font-weight: bold;">—</span>`

      return (
        `<div style="background-color: white; display: inline;">` +
        `<input type=checkbox id="${id}" ${checked} onClick="window.changeVarVisibility_${grID}('${id}', ${i})">&nbsp;&nbsp;` +
        `<span style='font-weight: bold; color: ${originalColor};'>${customDash} ${label}</span>` +
        (hasData && isVisible ? ': ' + series.yHTML : '') +
        '&nbsp;</div>'
      )
    })
    return (hasData ? 'X: ' + data.xHTML : '') + '<br>' + theSeries.join('<br>')
  }

  // Underlays for 45-degree line and regression lines
  const underlayCallback = (canvas: CanvasRenderingContext2D, _area: { x: number; y: number; w: number; h: number }, g: Dygraph) => {
    const ctx = canvas

    // Draw 45-degree line if enabled
    if (cfg.Show45DegreeLine) {
      draw45DegreeLine(ctx, g)
    }

    // Draw regression lines
    seriesList.forEach((series, idx) => {
      if (series.Regression && g.visibility()[idx]) {
        drawRegressionLine(ctx, g, series.Regression)
      }
    })
  }

  return {
    labels: ['X'].concat(seriesList.map((s) => s.Name)),
    legend: cfg.ShowLegend ? 'always' : 'never',
    series: seriesOptions,
    axes,
    xlabel: cfg.XAxisName,
    ylabel: cfg.YAxisName,
    visibility: seriesList.map((s) => isSeriesVisible(s.Name)),
    legendFormatter: cfg.ShowLegend ? legendFormatter : undefined,
    drawPoints: true,
    strokeWidth: 0,
    connectSeparatedPoints: false,
    underlayCallback,
  }
})

const calculateXExtent = (): [number, number] | null => {
  const cfg = props.config.PlotConfig

  if (cfg.XAxisLimitMin != null && cfg.XAxisLimitMax != null) {
    return [cfg.XAxisLimitMin, cfg.XAxisLimitMax]
  }

  let min = Infinity
  let max = -Infinity

  for (let i = 0; i < activeSeries.value.length; i++) {
    const series = activeSeries.value[i]
    for (let j = 0; j < series.Points.length; j++) {
      const point = series.Points[j]
      if (point.X < min) min = point.X
      if (point.X > max) max = point.X
    }
  }

  if (min === Infinity) return [0, 1]
  if (cfg.XAxisStartFromZero && min > 0) min = 0

  const range = max - min
  return [min - range * 0.1, max + range * 0.1]
}

const calculateYExtent = (): [number, number] | null => {
  const cfg = props.config.PlotConfig

  if (cfg.YAxisLimitMin != null && cfg.YAxisLimitMax != null) {
    return [cfg.YAxisLimitMin, cfg.YAxisLimitMax]
  }

  let minY = Infinity
  let maxY = -Infinity

  for (let i = 0; i < activeSeries.value.length; i++) {
    const series = activeSeries.value[i]
    for (let j = 0; j < series.Points.length; j++) {
      const point = series.Points[j]
      if (point.Y < minY) minY = point.Y
      if (point.Y > maxY) maxY = point.Y
    }
  }

  // Default to [0, 1] if no data
  if (minY === Infinity) return [0, 1]

  // Include zero if requested by the user
  if (cfg.YAxisStartFromZero) {
    if (minY > 0) minY = 0
    if (maxY < 0) maxY = 0
  }

  // Ensure valid scale
  if (minY === Infinity) minY = 0
  if (maxY === -Infinity) maxY = 1

  let span = maxY - minY
  // Special case: if we have no sense of scale, center on the sole value
  if (span === 0) {
    if (maxY !== 0) {
      span = Math.abs(maxY)
    } else {
      // If the sole value is zero, use range 0-1
      maxY = 1
      span = 1
    }
  }

  // Apply 10% padding (Dygraph's ypadCompat mode)
  const ypad = 0.1
  let minAxisY = minY - ypad * span
  let maxAxisY = maxY + ypad * span

  // Move a close-to-zero edge to zero (prevents invisible lines at edge)
  if (minAxisY < 0 && minY >= 0) minAxisY = 0
  if (maxAxisY > 0 && maxY <= 0) maxAxisY = 0

  return [minAxisY, maxAxisY]
}

// Setup legend checkbox interaction
onMounted(() => {
  const id = dyGraphID.value
  if (id) {
    ;(window as any)['changeVarVisibility_' + id] = (elementID: string, index: number) => {
      const checkBox: any = document.getElementById(elementID)
      const graphInstance = dyGraph.value
      if (graphInstance) {
        graphInstance.setVisibility(index, checkBox.checked)

        // Update our visibility tracking
        const seriesList = activeSeries.value
        if (index < seriesList.length) {
          const seriesName = seriesList[index].Name
          seriesVisibility.value = {
            ...seriesVisibility.value,
            [seriesName]: checkBox.checked,
          }
        }
      }
    }
  }
})
</script>

<style scoped>
/* Additional styling if needed */
</style>
