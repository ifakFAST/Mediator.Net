<template>
  <div @contextmenu="onContextMenu">
    <v-simple-table :height="theHeight" dense>
      <template v-slot:default>
        <thead v-if="config.ShowHeader">
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

    <v-menu v-model="contextMenu.show" :position-x="contextMenu.clientX" :position-y="contextMenu.clientY" absolute offset-y>
      <v-list>
        <v-list-item @click="onToggleShowHeader" >
          <v-list-item-title> {{ config.ShowHeader ? 'Hide Header' : 'Show Header' }}</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import { TimeRange } from '../../utils'

interface Config {
  ShowHeader: boolean
}

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
  @Prop({ default() { return {} } }) config: Config
  @Prop() backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  @Prop({ default() { return '' } }) eventName: string
  @Prop({ default() { return {} } }) eventPayload: object
  @Prop({ default() { return {} } }) timeRange: TimeRange
  @Prop({ default() { return 0 } }) resize: number
  @Prop({ default() { return null } }) dateWindow: number[]

  items: LogEntry[] = []

  contextMenu = {
    show: false,
    clientX: 0,
    clientY: 0,
  }

  canUpdateConfig = false

  mounted(): void {
    this.onLoadData()
    this.canUpdateConfig = window.parent['dashboardApp'].canUpdateViewConfig()
  }

  onContextMenu(e: any): void {
    if (this.canUpdateConfig) {
      e.preventDefault()
      e.stopPropagation()
      this.contextMenu.show = false
      this.contextMenu.clientX = e.clientX
      this.contextMenu.clientY = e.clientY
      const context = this
      this.$nextTick(() => {
        context.contextMenu.show = true
      })
    }
  }

  get theHeight(): string {
    if (this.height.trim() === '') { return 'auto' }
    return this.height
  }

  async onLoadData(): Promise<void> {
    const items: LogEntry[] = await this.backendAsync('ReadValues', { })
    this.items = items
  }

  async onToggleShowHeader(): Promise<void> {
    try {
      await this.backendAsync('ToggleShowHeader', {})
    } 
    catch (err) {
      alert(err.message)
    }
  }

  @Watch('eventPayload')
  watch_event(newVal: object, old: object): void {
    if (this.eventName === 'OnValuesChanged') {
      this.items = this.eventPayload as LogEntry[]
    }
  }
}

</script>
