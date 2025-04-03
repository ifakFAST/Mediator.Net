<template>

  <v-card elevation="1" class="pa-3" outlined >

    <p v-if="resolvedTitle !== ''" class="pl-0 mb-2" style="margin-top:-4px; font-weight: 500;">{{ resolvedTitle }}</p>

    <read-button v-if="type === 'ReadButton'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></read-button>

    <var-table v-if="type === 'VarTable'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></var-table>

    <var-table2-d v-if="type === 'VarTable2D'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></var-table2-d>

    <history-plot v-if="type === 'HistoryPlot'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"
      :configVariables="configVariables"></history-plot>

    <config-edit-numeric v-if="type === 'ConfigEditNumeric'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></config-edit-numeric>

    <config-edit-numeric2-d v-if="type === 'ConfigEditNumeric2D'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></config-edit-numeric2-d>

    <page-actions-log-View v-if="type === 'PageActionsLogView'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></page-actions-log-View>

    <text-display v-if="type === 'TextDisplay'" 
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></text-display>

    <image-display v-if="type === 'ImageDisplay'" 
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></image-display>

    <geo-map v-if="type === 'GeoMap'" 
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"
      :configVariables="configVariables" :setConfigVariableValues="setConfigVariableValues" ></geo-map>

  </v-card>

</template>


<script lang="ts">

import { Component, Prop, Vue } from 'vue-property-decorator'
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
import { TimeRange } from '../utils'
import * as model from './model'

@Component({
  components: {
    HistoryPlot,
    ReadButton,
    VarTable,
    VarTable2D,
    ConfigEditNumeric,
    ConfigEditNumeric2D,
    PageActionsLogView,
    TextDisplay,
    ImageDisplay,
    GeoMap,
  },
})
export default class WidgetWrapper extends Vue {

  @Prop({ default() { return '' } }) id: string
  @Prop({ default() { return '' } }) type: string
  @Prop({ default() { return '' } }) title: string
  @Prop({ default() { return '' } }) width: string
  @Prop({ default() { return '' } }) height: string
  @Prop({ default() { return {} } }) config: object
  @Prop() backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  @Prop({ default() { return '' } }) eventName: string
  @Prop({ default() { return {} } }) eventPayload: object
  @Prop({ default() { return {} } }) timeRange: TimeRange
  @Prop({ default() { return 0 } }) resize: number
  @Prop({ default() { return null } }) dateWindow: number[]
  @Prop({ default() { return {} } }) configVariables: model.ConfigVariableValues
  @Prop() setConfigVariableValues: (variableValues: Record<string, string>) => void

  onDateWindowChanged(window: number[]): void {
    this.$emit('date-window-changed', window)
  }

  get resolvedTitle(): string {
    return model.VariableReplacer.replaceVariables(this.title, this.configVariables.VarValues)
  }

}

</script>
