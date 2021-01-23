<template>
  <v-text-field v-bind:value="valueStr" v-on:input="updateValue" :label="label"></v-text-field>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'

@Component
export default class TextFieldNullableNumber extends Vue {

  @Prop(Number) value: number | null
  @Prop(String) label: string

  get valueStr() {
    const v = this.value
    return (v === null || v === undefined) ? '' : JSON.stringify(this.value)
  }

  updateValue(originalValue: string) {
    const v = originalValue.trim()
    if (v === '') {
      this.$emit('input', null)
    }
    else {
      try {
        const num = JSON.parse(v)
        if (typeof num === 'number') {
          this.$emit('input', num)
        }
      } catch { }
    }
  }
}

</script>
