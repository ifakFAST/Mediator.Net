<template>

  <v-card elevation="1" class="pa-3" outlined >

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

  </v-card>

</template>


<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import HistoryPlot from './widgets/HistoryPlot.vue'
import ReadButton from './widgets/ReadButton.vue'
import VarTable from './widgets/VarTable.vue'
import { TimeRange } from '../utils'

@Component({
  components: {
    HistoryPlot,
    ReadButton,
    VarTable,
  },
})
export default class WidgetWrapper extends Vue {

  @Prop({ default() { return '' } }) id: string
  @Prop({ default() { return '' } }) type: string
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
