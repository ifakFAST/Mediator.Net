<template>
  <v-text-field
    :label="label"
    :model-value="value"
    @update:model-value="updateValue"
  ></v-text-field>
</template>

<script setup lang="ts">
// Vue 3 port of TextFieldExt.vue from Vue 2 version
// Converts from @Component class syntax to Composition API

// Props definition
const props = defineProps<{
  value?: string | null
  label?: string
}>()

// Emits definition
const emit = defineEmits<{
  (e: 'update:model-value', value: string | null): void
}>()

// Methods
const updateValue = (originalValue: string) => {
  let v: string | null = originalValue
  if (v === '') {
    v = null
  }
  emit('update:model-value', v)
}
</script>
