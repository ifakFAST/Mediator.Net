<template>
  <v-dialog
    v-model="state.show"
    max-width="420px"
    persistent
    @keydown="onKeydown"
  >
    <v-card>
      <v-card-title class="text-wrap">Insert Data Point</v-card-title>
      <v-card-subtitle v-if="state.itemName">
        {{ state.itemName }}
      </v-card-subtitle>
      <v-card-text>
        <v-text-field
          v-model="state.timestampText"
          label="Timestamp"
          hint="YYYY-MM-DD HH:mm:ss"
          persistent-hint
        ></v-text-field>
        <v-text-field
          v-model="state.valueText"
          label="Value"
        ></v-text-field>
      </v-card-text>
      <v-card-actions>
        <v-spacer></v-spacer>
        <v-btn
          color="grey-darken-1"
          variant="text"
          @click="onCancel"
          >Cancel</v-btn
        >
        <v-btn
          color="primary-darken-1"
          variant="text"
          @click="onSave"
          >Insert</v-btn
        >
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { reactive } from 'vue'

type Axis = 'Left' | 'Right'

interface ItemConfig {
  Name: string
  Axis: Axis
}

interface InsertDataPointResult {
  timestamp: number
  value: number
}

interface DialogState {
  show: boolean
  timestampText: string
  valueText: string
  itemName: string
}

const state = reactive<DialogState>({
  show: false,
  timestampText: '',
  valueText: '',
  itemName: '',
})

let resolver: ((result: InsertDataPointResult | null) => void) | null = null

const pad = (value: number): string => {
  return value.toString().padStart(2, '0')
}

const formatTimestampForDisplay = (timestamp: number): string => {
  const date = new Date(timestamp)
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(
    date.getMinutes(),
  )}:${pad(date.getSeconds())}`
}

const parseTimestampFromDisplay = (text: string): number => {
  const trimmed = text.trim()
  if (trimmed === '') {
    return Date.now()
  }
  const normalized = trimmed.replace(' ', 'T')
  const parsed = Date.parse(normalized)
  if (Number.isNaN(parsed)) {
    return Date.now()
  }
  return parsed
}

const open = async (timestamp: number, yvalue: number, item: ItemConfig): Promise<InsertDataPointResult | null> => {
  state.timestampText = formatTimestampForDisplay(timestamp)
  state.valueText = yvalue.toString()
  state.itemName = item.Name
  state.show = true

  return await new Promise<InsertDataPointResult | null>((resolve) => {
    resolver = resolve
  })
}

const closeDialog = (): void => {
  state.show = false
}

const resolveAndReset = (result: InsertDataPointResult | null): void => {
  const currentResolver = resolver
  resolver = null
  currentResolver?.(result)
}

const onCancel = (): void => {
  closeDialog()
  resolveAndReset(null)
}

const onSave = (): void => {
  const timestamp = parseTimestampFromDisplay(state.timestampText)
  const value = Number(state.valueText)
  closeDialog()
  resolveAndReset({
    timestamp,
    value,
  })
}

const onKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    onCancel()
  }
}

defineExpose({
  open,
})
</script>
