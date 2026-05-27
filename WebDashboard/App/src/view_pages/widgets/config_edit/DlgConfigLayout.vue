<template>
  <v-dialog
    v-model="show"
    max-width="600px"
    persistent
    @keydown="onDialogKeydown"
  >
    <v-card>
      <v-card-title>Configure Layout</v-card-title>
      <v-card-text>
        <v-text-field
          v-model="rowsText"
          label="Rows"
          density="compact"
          hide-details="auto"
          :error-messages="rowsError"
          class="mb-4"
          hint="Comma-separated list of row names"
          persistent-hint
        ></v-text-field>
        <v-text-field
          v-model="columnsText"
          label="Columns"
          density="compact"
          hide-details="auto"
          :error-messages="columnsError"
          class="mb-4"
          hint="Comma-separated list of column names"
          persistent-hint
        ></v-text-field>
        <v-select
          v-model="unitRenderMode"
          label="Unit Display"
          :items="['Hide', 'Cell', 'ColumnLeft', 'ColumnRight', 'Row']"
          density="compact"
          hide-details
        ></v-select>
      </v-card-text>
      <v-card-actions>
        <v-spacer></v-spacer>
        <v-btn
          color="grey-darken-1"
          variant="text"
          @click="closeDialog"
          >Cancel</v-btn
        >
        <v-btn
          color="primary-darken-1"
          :disabled="!isValid"
          variant="text"
          @click="save"
          >Save</v-btn
        >
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

type UnitRenderMode = 'Hide' | 'Cell' | 'ColumnLeft' | 'ColumnRight' | 'Row'

interface Props {
  config: {
    Rows: string[]
    Columns: string[]
    UnitRenderMode: UnitRenderMode
  }
  backendAsync: (request: string, parameters: object) => Promise<any>
}

const props = defineProps<Props>()

const show = ref(false)
const rowsText = ref('')
const columnsText = ref('')
const unitRenderMode = ref<UnitRenderMode>('Hide')
let resolveDialog: (v: boolean) => void = () => {}

const parseEntries = (text: string): string[] =>
  text.split(',').map((s) => s.trim()).filter((s) => s !== '')

const validateEntries = (text: string): string => {
  const entries = parseEntries(text)
  if (entries.length === 0) return 'At least one entry is required'
  if (new Set(entries).size !== entries.length) return 'Entries must be unique'
  return ''
}

const rowsError = computed(() => validateEntries(rowsText.value))
const columnsError = computed(() => validateEntries(columnsText.value))
const isValid = computed(() => rowsError.value === '' && columnsError.value === '')

const showDialog = async (): Promise<boolean> => {
  rowsText.value = props.config.Rows.join(', ')
  columnsText.value = props.config.Columns.join(', ')
  unitRenderMode.value = props.config.UnitRenderMode
  show.value = true
  return new Promise<boolean>((resolve) => {
    resolveDialog = resolve
  })
}

const save = async (): Promise<void> => {
  try {
    await props.backendAsync('SaveLayout', {
      rows: parseEntries(rowsText.value),
      columns: parseEntries(columnsText.value),
      unitRenderMode: unitRenderMode.value,
    })
  } catch (exp) {
    alert(exp)
    return
  }
  show.value = false
  resolveDialog(true)
}

const closeDialog = (): void => {
  show.value = false
  resolveDialog(false)
}

const onDialogKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    closeDialog()
  }
}

defineExpose({ showDialog })
</script>
