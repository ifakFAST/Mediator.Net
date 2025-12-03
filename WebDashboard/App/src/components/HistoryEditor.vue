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

const showInterval = computed((): boolean => {
  return (
    model.value.Mode === 'Interval' ||
    model.value.Mode === 'IntervalExact' ||
    model.value.Mode === 'IntervalOrChanged' ||
    model.value.Mode === 'IntervalExactOrChanged'
  )
})

const showDeadband = computed((): boolean => {
  return (
    model.value.Mode === 'ValueOrQualityChanged' ||
    model.value.Mode === 'IntervalOrChanged' ||
    model.value.Mode === 'IntervalExactOrChanged'
  )
})

watch(
  () => model.value.Mode,
  (newMode) => {
    const noIntervalMode =
      newMode !== 'Interval' && newMode !== 'IntervalExact' && newMode !== 'IntervalOrChanged' && newMode !== 'IntervalExactOrChanged'
    if (noIntervalMode) {
      model.value.Interval = null
      model.value.Offset = null
    } else if (model.value.Interval === null || model.value.Interval.trim() === '') {
      model.value.Interval = '10 s'
    }

    const noDeadbandMode =
      newMode !== 'ValueOrQualityChanged' && newMode !== 'IntervalOrChanged' && newMode !== 'IntervalExactOrChanged'
    if (noDeadbandMode) {
      model.value.Deadband = null
    }
  },
)

watch(
  () => model.value.Interval,
  (newInterval) => {
    const noIntervalMode =
      model.value.Mode !== 'Interval' &&
      model.value.Mode !== 'IntervalExact' &&
      model.value.Mode !== 'IntervalOrChanged' &&
      model.value.Mode !== 'IntervalExactOrChanged'
    if (noIntervalMode || newInterval === null || newInterval.trim() === '') {
      model.value.Interval = null
    }
  },
)

watch(
  () => model.value.Offset,
  (newOffset) => {
    const noIntervalMode =
      model.value.Mode !== 'Interval' &&
      model.value.Mode !== 'IntervalExact' &&
      model.value.Mode !== 'IntervalOrChanged' &&
      model.value.Mode !== 'IntervalExactOrChanged'
    if (noIntervalMode || newOffset === null || newOffset.trim() === '') {
      model.value.Offset = null
    }
  },
)
</script>
