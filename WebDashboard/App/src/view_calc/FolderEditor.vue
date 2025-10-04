<template>
  <table cellspacing="10">
    <member-row
      v-model="model.Name"
      name="Name"
      :optional="false"
      type="String"
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
import type { Folder } from './model'
import MemberRow from './util/MemberRow.vue'

const model = defineModel<Folder>({ required: true })
</script>
