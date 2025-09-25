<template>
  <v-text-field
    :label="label"
    :model-value="valueStr"
    @update:model-value="updateValue"
  ></v-text-field>
</template>

<script setup lang="ts">
import { computed } from 'vue'

// Props
interface Props {
  modelValue?: number | null
  label?: string
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  label: '',
})

// Emits
const emit = defineEmits<{
  'update:modelValue': [value: number | null]
}>()

// Computed
const valueStr = computed(() => {
  const v = props.modelValue
  return v === null || v === undefined ? '' : JSON.stringify(props.modelValue)
})

// Methods
const updateValue = (originalValue: string) => {
  const v = originalValue.trim()
  if (v === '') {
    emit('update:modelValue', null)
  } else {
    try {
      const num = JSON.parse(v)
      if (typeof num === 'number') {
        emit('update:modelValue', num)
      }
    } catch {}
  }
}
</script>
