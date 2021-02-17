<template>

  <table cellspacing="10">
    <tr>
      <td><v-select     hide-details v-bind:value="value.Mode"     @input="update('Mode',     $event)" label="Mode"     :style="{ width: modeWidth + 'px' }" :items="historyItems"></v-select></td>
      <td>
        <v-tooltip right>
          <template v-slot:activator="{ on, attrs }">
            <v-text-field v-on="on" v-bind="attrs" hide-details v-bind:value="value.Interval" @input="update('Interval', $event)" label="Interval" style="width: 90px;" v-show="showInterval"></v-text-field>
          </template>
          <span>Available time units: ms, s, min, h, d</span>
        </v-tooltip>
      </td>
      <td>
        <v-tooltip right>
          <template v-slot:activator="{ on, attrs }">
            <v-text-field v-on="on" v-bind="attrs" hide-details v-bind:value="value.Offset"   @input="update('Offset',   $event)" label="Offset"   style="width: 90px;" v-show="showInterval"></v-text-field>
          </template>
          <span>Available time units: ms, s, min, h, d</span>
        </v-tooltip>
      </td>
    </tr>
  </table>

</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import * as fast from '../fast_types'

@Component
export default class HistoryEditor extends Vue {

  @Prop(Object) value: fast.History

  historyItems: fast.HistoryMode[] = fast.HistoryModeValues

  update(key: string, value: any): void {
    const newObj: fast.History = { ...this.value, [key]: value }
    const noIntervalMode = newObj.Mode !== 'Interval' && newObj.Mode !== 'IntervalExact' && newObj.Mode !== 'IntervalOrChanged' && newObj.Mode !== 'IntervalExactOrChanged'
    const interval = newObj.Interval
    if (noIntervalMode || interval === undefined || interval === null || interval.trim() === '') {
      newObj.Interval = null
    }
    const offset = newObj.Offset
    if (noIntervalMode || offset === undefined || offset === null || offset.trim() === '') {
      newObj.Offset = null
    }
    if (!noIntervalMode && newObj.Interval === null) {
      newObj.Interval = '10 s'
    }
    this.$emit('input', newObj)
  }

  get showInterval(): boolean {
    return this.value.Mode === 'Interval' || this.value.Mode === 'IntervalExact' || this.value.Mode === 'IntervalOrChanged' || this.value.Mode === 'IntervalExactOrChanged'
  }

  get modeWidth(): string {
    return this.showInterval ? '140' : '225'
  }
}

</script>
