<template>
  <v-select
    item-title="LongName"
    item-value="ID"
    :items="locations"
    :label="label"
    :model-value="modelValue"
    @update:model-value="$emit('update:modelValue', $event)"
  ></v-select>
</template>

<script setup lang="ts">
import type { LocationInfo } from '../fast_types'

interface Props {
  locations: LocationInfo[]
  modelValue: string
  label?: string
}

defineProps<Props>()
defineEmits<{
  'update:modelValue': [value: string]
}>()
</script>
