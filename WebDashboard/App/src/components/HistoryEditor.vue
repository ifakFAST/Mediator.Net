<template>
  <table cellspacing="10">
    <tbody>
      <tr>
        <td>
          <v-select
            v-model="model.Mode"
            :items="historyItems"
            label="Mode"
            style="width: 225px"
          />
        </td>
        <td>
          <v-tooltip location="right">
            <template #activator="{ props }">
              <v-text-field
                v-show="showInterval"
                v-bind="props"
                v-model="model.Interval"
                label="Interval"
                style="width: 90px"
              />
            </template>
            <span>Available time units: ms, s, min, h, d</span>
          </v-tooltip>
        </td>
        <td>
          <v-tooltip location="right">
            <template #activator="{ props }">
              <v-text-field
                v-show="showInterval"
                v-bind="props"
                v-model="model.Offset"
                label="Offset"
                style="width: 90px"
              />
            </template>
            <span>Available time units: ms, s, min, h, d</span>
          </v-tooltip>
        </td>
        <td>
          <v-tooltip location="right">
            <template #activator="{ props }">
              <text-field-nullable-number
                v-show="showDeadband"
                v-model="model.Deadband"
                v-bind="props"
                label="Deadband"
                style="width: 100px"
              ></text-field-nullable-number>
            </template>
            <span>Absolute deadband value for numeric types. Value change detected when |new - old| > deadband</span>
          </v-tooltip>
        </td>
      </tr>
    </tbody>
  </table>
</template>

<script setup lang="ts">
import { computed, watch } from 'vue'
import * as fast from '../fast_types'
import TextFieldNullableNumber from './TextFieldNullableNumber.vue'

const model = defineModel<fast.History>({ required: true })

const historyItems: fast.HistoryMode[] = fast.HistoryModeValues
const intervalModes = new Set<fast.HistoryMode>(['Interval', 'IntervalExact', 'IntervalOrChanged', 'IntervalExactOrChanged'])
const deadbandModes = new Set<fast.HistoryMode>(['ValueOrQualityChanged', 'IntervalOrChanged', 'IntervalExactOrChanged'])

const isIntervalMode = (mode: fast.HistoryMode): boolean => {
  return intervalModes.has(mode)
}

const isDeadbandMode = (mode: fast.HistoryMode): boolean => {
  return deadbandModes.has(mode)
}

const isBlankDuration = (value: fast.Duration | null | undefined): boolean => {
  return value === null || value === undefined || value.trim() === ''
}

const showInterval = computed((): boolean => {
  return isIntervalMode(model.value.Mode)
})

const showDeadband = computed((): boolean => {
  return isDeadbandMode(model.value.Mode)
})

watch(
  () => model.value.Mode,
  (newMode) => {
    if (!isIntervalMode(newMode)) {
      model.value.Interval = null
      model.value.Offset = null
    } else if (isBlankDuration(model.value.Interval)) {
      model.value.Interval = '10 s'
    }

    if (!isDeadbandMode(newMode)) {
      model.value.Deadband = null
    }
  },
)

watch(
  () => model.value.Interval,
  (newInterval) => {
    if (!isIntervalMode(model.value.Mode) || isBlankDuration(newInterval)) {
      model.value.Interval = null
    }
  },
)

watch(
  () => model.value.Offset,
  (newOffset) => {
    if (!isIntervalMode(model.value.Mode) || isBlankDuration(newOffset)) {
      model.value.Offset = null
    }
  },
)
</script>
