<template>
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
</template>

<script setup lang="ts">
import type { Signal } from './model'
import MemberRow from './util/MemberRow.vue'

const model = defineModel<Signal>({ required: true })
</script>
