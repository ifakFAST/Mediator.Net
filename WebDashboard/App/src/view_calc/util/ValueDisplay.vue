<template>
  <div>
    <div
      style="word-break: break-all"
      :style="{ color: qualityColor }"
    >
      <span>{{ valueString }}</span>
      <span
        v-if="!showStruct && isStruct"
        style="cursor: pointer"
        @click="showStruct = !showStruct"
      >
        <v-icon>mdi-chevron-right</v-icon>
      </span>
      <span
        v-if="showStruct && isStruct"
        style="cursor: pointer"
        @click="showStruct = !showStruct"
      >
        <v-icon>mdi-chevron-down</v-icon>
      </span>
    </div>
    <struct-view
      v-if="showStruct && isStruct"
      style="font-size: 11px"
      :value="vtq.V"
      :vertical="isVertical"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { VTQ, DataType } from '../../fast_types'

const props = defineProps<{
  type: DataType
  dimension: number
  vtq: VTQ
}>()

const showStruct = ref(false)

const valueString = computed((): string => {
  const MaxLen = 22
  const str = props.vtq.V
  if (str.length > MaxLen) {
    return str.substring(0, MaxLen) + '\u00A0...'
  } else {
    return str
  }
})

const isStruct = computed((): boolean => {
  return props.type === 'Struct' || props.type === 'Timeseries'
})

const isVertical = computed((): boolean => {
  return props.dimension !== 1 || props.type === 'Timeseries'
})

const qualityColor = computed((): string => {
  const q = props.vtq.Q
  if (q === 'Good') {
    return 'green'
  }
  if (q === 'Uncertain') {
    return 'orange'
  }
  return 'red'
})
</script>
