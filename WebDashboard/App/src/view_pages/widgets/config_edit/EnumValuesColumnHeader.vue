<template>
  <span style="display: inline-flex; align-items: center; gap: 4px">
    Enum Values
    <v-tooltip
      location="top"
      :open-delay="250"
    >
      <template #activator="{ props: tooltipProps }">
        <v-icon
          size="16"
          color="primary"
          style="cursor: help"
          v-bind="tooltipProps"
          >mdi-help-circle-outline</v-icon
        >
      </template>
      <span :style="enumValuesHelpStyle">{{ enumValuesHelpText }}</span>
    </v-tooltip>
  </span>
</template>

<script setup lang="ts">
import { enumValuesHelpStyle, enumValuesHelpText } from './enumValuesHelp'
</script>
