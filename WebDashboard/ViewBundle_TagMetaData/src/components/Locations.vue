<template>
  <v-container
    class="pt-3 pb-1 pl-0"
    fluid
  >
    <v-row>
      <v-col
        v-for="level in allLevels"
        :key="level"
        cols="auto"
      >
        <v-select
          v-if="getLocations(level).length > 0"
          v-model="model[level + 1]"
          chips
          item-title="Name"
          item-value="ID"
          :items="getLocations(level)"
          label="Locations"
          :menu-props="{ closeOnContentClick: true }"
          multiple
          :return-object="false"
          variant="solo"
          :width="300"
        >
          <template #chip="{ props, item }">
            <v-chip
              v-bind="props"
              closable
            >
              {{ item.raw.Name }}
            </v-chip>
          </template>
        </v-select>
      </v-col>
    </v-row>
  </v-container>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import * as fast from '../fast_types'

interface Props {
  locations: fast.LocationInfo[]
}

const model = defineModel<string[][]>({ required: true })
const properties = defineProps<Props>()

const getLocations = (level: number): fast.LocationInfo[] => {
  if (level < 0 || level >= model.value.length) return []
  const setOfParents: Set<string> = new Set<string>(model.value[level])
  return properties.locations.filter((loc) => setOfParents.has(loc.Parent))
}

const deepth = (loc: fast.LocationInfo, map: Map<string, fast.LocationInfo>): number => {
  let res = 0
  while (true) {
    const parent = map.get(loc.Parent)
    if (parent === undefined) return res
    res += 1
    loc = parent
  }
}

const allLevels = computed((): number[] => {
  const locs = properties.locations
  const map = new Map<string, fast.LocationInfo>()
  for (const loc of locs) {
    map.set(loc.ID, loc)
  }
  let max = 0
  for (const loc of locs) {
    max = Math.max(max, deepth(loc, map))
  }
  const res: number[] = []
  for (let i = 0; i < max; ++i) {
    res.push(i)
  }
  return res
})
</script>
