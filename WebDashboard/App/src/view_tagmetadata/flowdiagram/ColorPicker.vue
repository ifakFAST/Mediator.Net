<template>
  <input
    type="color"
    :value="modelValue"
    @input="onColorChanged"
    style="height: 25px"
  />
</template>

<script setup lang="ts">
// Props
defineProps<{
  modelValue: string
}>()

// Emits
const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

// Methods
const onColorChanged = (event: Event) => {
  if (event?.target) {
    const color = (event.target as HTMLInputElement).value
    if (color) {
      emit('update:modelValue', color)
    }
  }
}
</script>

<style></style>
