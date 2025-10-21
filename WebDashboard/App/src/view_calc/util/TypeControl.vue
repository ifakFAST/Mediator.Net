<template>
  <div style="min-width: 360px">
    <history-editor
      v-if="type === 'History'"
      v-model="model"
    />
    <v-text-field
      v-if="type === 'String'"
      v-model="model"
    />
    <v-textarea
      v-if="type === 'Text'"
      v-model="model"
      outlined
      :rows="3"
    />
    <v-text-field
      v-if="type === 'Number'"
      :model-value="model"
      type="number"
      @update:model-value="model = convert2Num($event)"
    />
    <v-select
      v-if="type === 'DataType'"
      v-model="model"
      :items="dataTypes"
    />
    <v-tooltip
      v-if="type === 'Duration'"
      right
    >
      <template #activator="{ props }">
        <v-text-field
          v-bind="props"
          v-model="model"
        />
      </template>
      <span>Available time units: ms, s, min, h, d</span>
    </v-tooltip>
    <v-tooltip
      v-if="type === 'Timestamp'"
      right
    >
      <template #activator="{ props }">
        <v-text-field
          v-bind="props"
          v-model="model"
          placeholder="YYYY-MM-DDTHH:mm:ssZ"
        />
      </template>
      <span>ISO 8601 format, e.g. 2024-01-31T12:00:00Z</span>
    </v-tooltip>
    <v-switch
      v-if="type === 'Boolean'"
      v-model="model"
      color="primary"
      hide-details
    />
    <v-select
      v-if="type === 'Enum'"
      v-model="model"
      :items="enumValues"
    />
    <v-textarea
      v-if="type === 'Code'"
      v-model="model"
      outlined
      :rows="10"
    />
  </div>
</template>

<script setup lang="ts">
import type { MemberTypeEnum } from './member_types'
import * as fast from '../../fast_types'

const properties = defineProps<{
  type: MemberTypeEnum
  enumValues?: string[]
}>()

const model = defineModel<any>({ required: true })

const dataTypes: fast.DataType[] = fast.DataTypeValues

const convert2Num = (str: string): any => {
  if (str === '') {
    return ''
  }
  return Number(str)
}
</script>
