<template>
  <v-select
    :value="value"
    @input="update($event)"
    :items="locations"
    item-text="LongName"
    item-value="ID"
    label="Location"
  ></v-select>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import * as fast from '../fast_types'

@Component
export default class Location extends Vue {

  @Prop() locations: fast.LocationInfo[]
  @Prop() value: string

  update(newValue: any): void {
    this.$emit('input', newValue)
  }

}

</script>

<style>

</style>
