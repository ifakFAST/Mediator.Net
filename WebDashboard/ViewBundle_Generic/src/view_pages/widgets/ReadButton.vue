<template>

  <v-card-text>
    {{ id }} {{ eventName }} {{ result }}
    <v-btn @click="onButton">Read</v-btn>
  </v-card-text>

</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import * as fast from '../../fast_types'

interface Config {
  Variable?: fast.VariableRef
}

@Component({
  components: {
  },
})
export default class ReadButton extends Vue {

  @Prop({ default() { return '' } }) id: string
  @Prop({ default() { return '' } }) width: string
  @Prop({ default() { return '' } }) height: string
  @Prop({ default() { return {} } }) config: Config
  @Prop() backendAsync: (request: string, parameters: object) => Promise<any>
  @Prop({ default() { return '' } }) eventName: string
  @Prop({ default() { return {} } }) eventPayload: any
  @Prop({ default() { return {} } }) timeRange: object
  @Prop({ default() { return 0 } }) resize: number

  result = ''

  async mounted(): Promise<void> {
    const result = await this.backendAsync('ReadVar', { })
    this.result = result
    console.info('mounted: ' + result)
  }

  async onButton(): Promise<void> {
    const para = {
      param1: 'Hallo!',
    }
    const result = await this.backendAsync('Button', para)
    this.result = result

    console.info('Config: ' + JSON.stringify(this.config))
  }

  @Watch('eventPayload')
  watch_event(newVal: any, old: any): void {
    if (this.eventName === 'OnVarChanged') {
      console.info('VarChanged event! ' + JSON.stringify(newVal))
      this.result = newVal.NewVal
    }
  }

}

</script>
