<template>
  <v-card
    :class="{ 'pa-3': paddingOverride === '' }"
    elevation="1"
    :style="paddingStyle"
    variant="outlined"
  >
    <p
      v-if="resolvedTitle !== ''"
      class="pl-0 mb-2"
      style="margin-top: -4px; font-weight: 500"
    >
      {{ resolvedTitle }}
    </p>

    <read-button
      v-if="type === 'ReadButton'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></read-button>

    <var-table
      v-if="type === 'VarTable'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></var-table>

    <var-table2-d
      v-if="type === 'VarTable2D'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></var-table2-d>

    <history-plot
      v-if="type === 'HistoryPlot'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :config-variables="configVariables"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></history-plot>

    <config-edit-numeric
      v-if="type === 'ConfigEditNumeric'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></config-edit-numeric>

    <config-edit-numeric2-d
      v-if="type === 'ConfigEditNumeric2D'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></config-edit-numeric2-d>

    <page-actions-log-View
      v-if="type === 'PageActionsLogView'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></page-actions-log-View>

    <text-display
      v-if="type === 'TextDisplay'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></text-display>

    <image-display
      v-if="type === 'ImageDisplay'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></image-display>

    <geo-map
      v-if="type === 'GeoMap'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :config-variables="configVariables"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :set-config-variable-values="setConfigVariableValues"
      :set-widget-title-var-values="setWidgetTitleVarValues"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></geo-map>

    <xy-plot
      v-if="type === 'xyPlot'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></xy-plot>

    <time-aggregated-bar-chart
      v-if="type === 'TimeAggregatedBarChart'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></time-aggregated-bar-chart>

    <time-aggregated-table
      v-if="type === 'TimeAggregatedTable'"
      :id="id"
      :backend-async="backendAsync"
      :config="config as any"
      :date-window="dateWindowForComponents as any"
      :event-name="eventName"
      :event-payload="eventPayload"
      :height="height"
      :resize="resize"
      :time-range="timeRange"
      :width="width"
      @date-window-changed="onDateWindowChanged"
    ></time-aggregated-table>
  </v-card>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import HistoryPlot from './widgets/HistoryPlot.vue'
import ReadButton from './widgets/ReadButton.vue'
import VarTable from './widgets/VarTable.vue'
import VarTable2D from './widgets/VarTable2D.vue'
import ConfigEditNumeric from './widgets/config_edit/ConfigEditNumeric.vue'
import ConfigEditNumeric2D from './widgets/config_edit/ConfigEditNumeric2D.vue'
import PageActionsLogView from './widgets/PageActionsLogView.vue'
import TextDisplay from './widgets/TextDisplay.vue'
import ImageDisplay from './widgets/ImageDisplay.vue'
import GeoMap from './widgets/GeoMap.vue'
import XyPlot from './widgets/xyPlot.vue'
import TimeAggregatedBarChart from './widgets/TimeAggregatedBarChart.vue'
import TimeAggregatedTable from './widgets/TimeAggregatedTable.vue'
import type { TimeRange } from '../utils'
import * as model from './model'

const props = defineProps<{
  id: string
  type: string
  title: string
  width: string
  height: string
  paddingOverride: string
  config: object
  backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  eventName: string
  eventPayload: object
  timeRange: TimeRange
  resize: number
  dateWindow: number[] | null
  configVariables: model.ConfigVariableValues
  setConfigVariableValues: (variableValues: Record<string, string>) => void
}>()

const emit = defineEmits<{
  'date-window-changed': [window: number[] | null]
}>()

const widgetTitleVarValues = ref<Record<string, string>>({})

// Handle date window - pass through as-is since some components expect null
const dateWindowForComponents = computed(() => props.dateWindow)

const setWidgetTitleVarValues = (newValues: Record<string, string>): void => {
  const newWidgetTitleVarValues = { ...widgetTitleVarValues.value }
  for (const key in newValues) {
    const value = newValues[key]
    newWidgetTitleVarValues[key] = value
  }
  widgetTitleVarValues.value = newWidgetTitleVarValues
}

const onDateWindowChanged = (window: number[] | null | undefined): void => {
  emit('date-window-changed', window ?? null)
}

const resolvedTitle = computed((): string => {
  let title = props.title
  title = model.VariableReplacer.replaceVariables(title, widgetTitleVarValues.value)
  title = model.VariableReplacer.replaceVariables(title, props.configVariables.VarValues, '?')
  return title
})

const paddingStyle = computed((): { padding?: string } => {
  return props.paddingOverride !== '' ? { padding: props.paddingOverride } : {}
})

defineExpose({
  setWidgetTitleVarValues,
})
</script>
