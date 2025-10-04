<template>
  <v-dialog
    v-model="show"
    max-width="500px"
    persistent
    @keydown.esc="onCancel"
  >
    <v-card>
      <v-card-title>
        {{ title }}
      </v-card-title>
      <v-card-text>
        <v-container>
          <v-row>
            <v-col cols="12">
              <p>Select the row and column where you want to move this widget:</p>
            </v-col>
          </v-row>
          <v-row>
            <v-col
              cols="12"
              sm="6"
            >
              <v-select
                ref="rowSelect"
                v-model="selectedRow"
                item-title="text"
                item-value="id"
                :items="rowOptions"
                label="Target Row"
                @update:model-value="onRowChanged"
              ></v-select>
            </v-col>
            <v-col
              cols="12"
              sm="6"
            >
              <v-select
                v-model="selectedColumn"
                :disabled="selectedRow === null"
                item-title="text"
                item-value="id"
                :items="availableColumns"
                label="Target Column"
              ></v-select>
            </v-col>
          </v-row>
        </v-container>
      </v-card-text>
      <v-card-actions>
        <v-spacer></v-spacer>
        <v-btn
          color="blue-darken-1"
          variant="text"
          @click="onCancel"
          >Cancel</v-btn
        >
        <v-btn
          color="blue-darken-1"
          :disabled="!isValidNewPosition"
          variant="text"
          @click="onOK"
          >Move</v-btn
        >
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'

const show = ref(false)
const title = ref('')
const rowOptions = ref<{ id: number; text: string }[]>([])
const columnOptionsMap = ref<{ [rowId: number]: { id: number; text: string }[] }>({})
const selectedRow = ref<number | null>(null)
const selectedColumn = ref<number | null>(null)
const initialRow = ref(-1)
const initialColumn = ref(-1)
const rowSelect = ref()

let resolve: ((value: { targetRow: number; targetCol: number } | null) => void) | null = null

const availableColumns = computed((): { id: number; text: string }[] => {
  if (selectedRow.value === null) return []
  return columnOptionsMap.value[selectedRow.value] || []
})

const isSelectionValid = computed((): boolean => {
  return selectedRow.value !== null && selectedColumn.value !== null
})

const isValidNewPosition = computed((): boolean => {
  if (!isSelectionValid.value) return false

  return !(selectedRow.value === initialRow.value && selectedColumn.value === initialColumn.value)
})

const onRowChanged = (): void => {
  selectedColumn.value = null
}

watch(show, async (val: boolean) => {
  if (val) {
    await nextTick()
    if (rowSelect.value) {
      rowSelect.value.focus()
    }
  }
})

const open = async (
  titleValue: string,
  options: {
    rows: { id: number; text: string }[]
    columns: { [rowId: number]: { id: number; text: string }[] }
  },
  currentRow: number,
  currentColumn: number,
): Promise<{ targetRow: number; targetCol: number } | null> => {
  title.value = titleValue
  rowOptions.value = options.rows
  columnOptionsMap.value = options.columns
  selectedRow.value = currentRow
  selectedColumn.value = currentColumn
  initialRow.value = currentRow
  initialColumn.value = currentColumn
  show.value = true

  return new Promise((resolvePromise) => {
    resolve = resolvePromise
  })
}

const onOK = (): void => {
  if (resolve && selectedRow.value !== null && selectedColumn.value !== null) {
    resolve({
      targetRow: selectedRow.value,
      targetCol: selectedColumn.value,
    })
  }
  show.value = false
  resolve = null
}

const onCancel = (): void => {
  if (resolve) {
    resolve(null)
  }
  show.value = false
  resolve = null
}

defineExpose({
  open,
})
</script>
