<template>
  <v-tooltip
    location="top"
    :open-delay="400"
  >
    <template #activator="{ props: tooltipProps }">
      <v-text-field
        :model-value="modelValue"
        :placeholder="enumValuesPlaceholder"
        v-bind="tooltipProps"
        :style="fieldStyle"
        @update:model-value="$emit('update:modelValue', $event)"
      ></v-text-field>
    </template>
    <span :style="enumValuesHelpStyle">{{ enumValuesHelpText }}</span>
  </v-tooltip>
</template>

<script setup lang="ts">
import { enumValuesHelpStyle, enumValuesHelpText, enumValuesPlaceholder } from './enumValuesHelp'

withDefaults(
  defineProps<{
    modelValue: string
    fieldStyle?: string
  }>(),
  {
    fieldStyle: 'width: 16ch',
  },
)

defineEmits<{
  'update:modelValue': [value: string]
}>()
</script>
