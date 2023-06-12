<template>

  <v-card elevation="1" class="pa-3" outlined >

    <span v-if="title !== ''" class="pl-4 pb-5" style="font-weight: 500;">{{ title }}</span>

    <read-button v-if="type === 'ReadButton'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></read-button>

    <var-table v-if="type === 'VarTable'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></var-table>

    <history-plot v-if="type === 'HistoryPlot'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></history-plot>

    <config-edit-numeric v-if="type === 'ConfigEditNumeric'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></config-edit-numeric>

    <page-actions-log-View v-if="type === 'PageActionsLogView'"
      :id="id" :width="width" :height="height" :config="config" :backendAsync="backendAsync"
      :eventName="eventName" :eventPayload="eventPayload" :timeRange="timeRange"
      :resize="resize" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged"></page-actions-log-View>

  </v-card>

</template>


<script lang="ts">

import { Component, Prop, Vue } from 'vue-property-decorator'
import HistoryPlot from './widgets/HistoryPlot.vue'
import ReadButton from './widgets/ReadButton.vue'
import VarTable from './widgets/VarTable.vue'
import ConfigEditNumeric from './widgets/config_edit/ConfigEditNumeric.vue'
import PageActionsLogView from './widgets/PageActionsLogView.vue'
import { TimeRange } from '../utils'

@Component({
  components: {
    HistoryPlot,
    ReadButton,
    VarTable,
    ConfigEditNumeric,
    PageActionsLogView,
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

  onDateWindowChanged(window: number[]): void {
    this.$emit('date-window-changed', window)
  }

}

</script>
