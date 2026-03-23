<template>
  <div>
    <v-tabs v-model="tab" density="compact">
      <v-tab value="properties">Properties</v-tab>
      <v-tab value="history">History</v-tab>
    </v-tabs>
    <div v-if="tab === 'properties'">
      <table cellspacing="10">
        <member-row
          v-model="model.Name"
          name="Name"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.Unit"
          name="Unit"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.Type"
          name="Type"
          :optional="false"
          type="DataType"
        />
        <v-tooltip
          v-if="model.Type === 'Struct'"
          right
        >
          <template #activator="{ props }">
            <member-row
              v-bind="props"
              v-model="model.TypeConstraints"
              name="TypeConstraints"
              :optional="false"
              type="Text"
            />
          </template>
          <span>Specify struct members, e.g.: Value: number, Label: string</span>
        </v-tooltip>
        <member-row
          v-model="model.Dimension"
          name="Dimension"
          :optional="false"
          type="Number"
        />
        <member-row
          v-model="model.Comment"
          name="Comment"
          :optional="false"
          type="Text"
        />
        <member-row
          v-model="model.History"
          name="History"
          :optional="true"
          type="History"
        />
      </table>
    </div>
    <div v-else-if="tab === 'history'">
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { Signal } from './model'
import MemberRow from './util/MemberRow.vue'

const model = defineModel<Signal>({ required: true })
const tab = ref('properties')
</script>
