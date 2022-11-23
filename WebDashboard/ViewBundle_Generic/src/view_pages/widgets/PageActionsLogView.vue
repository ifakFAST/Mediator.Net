<template>
  <v-simple-table :height="theHeight" dense>
    <template v-slot:default>
      <thead>
        <tr>
          <th class="text-left" style="font-size:14px; height:36px;">Who</th>
          <th class="text-left" style="font-size:14px; height:36px;">Time</th>
          <th class="text-left" style="font-size:14px; height:36px;">Change</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="item in items" :key="item.Timestamp">
          <td class="text-left" style="font-size:14px; height:36px;">
           {{ item.User }}
          </td>
          <td class="text-left" style="font-size:14px; height:36px;">
            {{ item.Time }}
          </td>
          <td class="text-left" style="font-size:14px; height:36px;">
            {{ item.Action }}
          </td>        
        </tr>
      </tbody>
    </template>
  </v-simple-table> 
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import { TimeRange } from '../../utils'

interface LogEntry {
  Timestamp: number
  Time: string
  User: string
  Action: string
}

@Component({
  components: {
  },
})
export default class PageActionsLogView extends Vue {

  @Prop({ default() { return '' } }) id: string
  @Prop({ default() { return '' } }) width: string
  @Prop({ default() { return '' } }) height: string
  @Prop({ default() { return {} } }) config: any
  @Prop() backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  @Prop({ default() { return '' } }) eventName: string
  @Prop({ default() { return {} } }) eventPayload: object
  @Prop({ default() { return {} } }) timeRange: TimeRange
  @Prop({ default() { return 0 } }) resize: number
  @Prop({ default() { return null } }) dateWindow: number[]

  items: LogEntry[] = []

  mounted(): void {
    this.onLoadData()
  }

  get theHeight(): string {
    if (this.height.trim() === '') { return 'auto' }
    return this.height
  }

  async onLoadData(): Promise<void> {
    const items: LogEntry[] = await this.backendAsync('ReadValues', { })
    this.items = items
  }

  @Watch('eventPayload')
  watch_event(newVal: object, old: object): void {
    if (this.eventName === 'OnValuesChanged') {
      this.items = this.eventPayload as LogEntry[]
    }
  }
}

</script>
