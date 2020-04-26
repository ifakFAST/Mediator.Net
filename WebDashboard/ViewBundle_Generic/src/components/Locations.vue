<template>
 <v-container class="pa-0">
    <v-row>
      <v-col v-for="level in allLevels" :key="level" >
        <v-select
            v-model="value[level+1]"
            :items="getLocations(level)"
            v-if="getLocations(level).length > 0"
            item-text="Name"
            item-value="ID"
            label="Locations"
            :menu-props="{ closeOnContentClick: true, closeOnClick: true }"
            multiple
            chips
            hide-details
            deletable-chips
        ></v-select>
      </v-col>
    </v-row>
  </v-container>

</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import * as fast from '../fast_types'

@Component
export default class Locations extends Vue {

  @Prop() locations: fast.LocationInfo[]
  @Prop() value: string[][]

  @Watch('value')
  watch_value(value: string[][], oldValue: string[][]): void {
    for (let i = 1; i < value.length; ++i) {
      const levelSelection: string[] = value[i]
      const locations: fast.LocationInfo[] = this.getLocations(i - 1)
      value[i] = levelSelection.filter((loc) => locations.some((loc2) => loc === loc2.ID))
    }
  }

  getLocations(level: number /*starting at 0 == Root */): fast.LocationInfo[] {
    if (level < 0 || level >= this.value.length) { return [] }
    const setOfParents: Set<string> = new Set<string>(this.value[level])
    const res = this.locations.filter((loc) => setOfParents.has(loc.Parent))
    return res
  }

  get allLevels(): number[] {
    const locs = this.locations
    const map = new Map<string, fast.LocationInfo>()
    for (const loc of locs) {
      map.set(loc.ID, loc)
    }
    let max = 0
    for (const loc of locs) {
      max = Math.max(max, this.deepth(loc, map))
    }
    const res: number[] = []
    for (let i = 0; i < max; ++i) {
      res.push(i)
    }
    return res
  }

  deepth(loc: fast.LocationInfo, map: Map<string, fast.LocationInfo>): number {
    let res = 0
    while (true) {
      const parent = map.get(loc.Parent)
      if (parent === undefined) { return res }
      res += 1
      loc = parent
    }
    return res
  }

}

</script>

<style>

</style>
