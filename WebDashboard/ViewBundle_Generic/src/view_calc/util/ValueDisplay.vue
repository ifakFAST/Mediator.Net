<template>
  <div>
    <div style="word-break: break-all;" v-bind:style="{ color: qualityColor }">
      <span>{{ valueString }}</span>
      <span v-if="!showStruct && isStruct" @click="showStruct = !showStruct" style="cursor: pointer;"><v-icon>keyboard_arrow_right</v-icon></span>
      <span v-if=" showStruct && isStruct" @click="showStruct = !showStruct" style="cursor: pointer;"><v-icon>keyboard_arrow_down</v-icon></span>
    </div>
    <struct-view style="font-size: 11px;" v-if="showStruct && isStruct" :value="vtq.V" :vertical="isVertical"></struct-view>
  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import StructView from '../../components/StructView.vue'
import * as fast from '../../fast_types'

@Component({
  components: {
    StructView,
  },
})
export default class ValueDisplay extends Vue {

  @Prop(String) type: fast.DataType
  @Prop(Number) dimension: number
  @Prop(Object) vtq: fast.VTQ

  showStruct = false

  get valueString(): string {
    const MaxLen = 22
    const str = this.vtq.V
    if (str.length > MaxLen) {
      return str.substring(0, MaxLen) + '\u00A0...'
    }
    else {
      return str
    }
  }

  get isStruct(): boolean {
    return this.type === 'Struct' || this.type === 'Timeseries'
  }

  get isVertical(): boolean {
    return this.dimension !== 1 || this.type === 'Timeseries'
  }

  get qualityColor() {
    const q = this.vtq.Q
    if (q === 'Good') { return 'green' }
    if (q === 'Uncertain') { return 'orange' }
    return 'red'
  }
}

</script>
