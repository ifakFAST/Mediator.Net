<template>
  <v-text-field v-bind:value="value" v-on:input="updateValue" :label="label"></v-text-field>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'

@Component
export default class TextFieldExt extends Vue {

  @Prop(String) value: string
  @Prop(String) label: string

  updateValue(originalValue: string) {
    let v = originalValue
    if (v === '') {
      v = null
    }
    this.$emit('input', v)
  }
}

</script>
