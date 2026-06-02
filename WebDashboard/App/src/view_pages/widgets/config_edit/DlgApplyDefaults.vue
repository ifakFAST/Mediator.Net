<template>
  <v-dialog
    v-model="dialog"
    max-width="600"
    persistent
    @keydown.esc="cancel"
  >
    <v-card>
      <v-card-title>
        <span class="text-h5">Apply default values?</span>
      </v-card-title>
      <v-card-text>
        <v-table
          v-if="rows.length > 0"
          density="compact"
        >
          <thead>
            <tr>
              <th class="text-left">Setting</th>
              <th class="text-left">Current value</th>
              <th class="text-left">New value</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="(row, idx) in rows"
              :key="idx"
            >
              <td>{{ row.name }}</td>
              <td>{{ row.currentDisplay }}</td>
              <td>{{ row.newDisplay }}</td>
            </tr>
          </tbody>
        </v-table>
        <p
          v-else
          class="text-medium-emphasis"
        >
          No values can be applied.
        </p>
        <p
          v-if="skippedCount > 0"
          class="text-medium-emphasis mt-2 mb-0"
          style="font-size: 13px"
        >
          {{ skippedCount }} item{{ skippedCount === 1 ? '' : 's' }} skipped — not editable or not configured.
        </p>
      </v-card-text>
      <v-card-actions class="pt-0">
        <v-spacer></v-spacer>
        <v-btn
          color="grey-darken-1"
          variant="text"
          @click="cancel"
          >Cancel</v-btn
        >
        <v-btn
          color="primary-darken-1"
          :disabled="rows.length === 0"
          variant="text"
          @click="agree"
          >Apply</v-btn
        >
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'

export interface ApplyDefaultsRow {
  name: string
  currentDisplay: string
  newDisplay: string
}

const dialog = ref(false)
const rows = ref<ApplyDefaultsRow[]>([])
const skippedCount = ref(0)
let resolveDialog: ((value: boolean) => void) | null = null

const open = (planRows: ApplyDefaultsRow[], skipped: number): Promise<boolean> => {
  rows.value = planRows
  skippedCount.value = skipped
  dialog.value = true
  return new Promise<boolean>((resolve) => {
    resolveDialog = resolve
  })
}

const agree = (): void => {
  if (resolveDialog) {
    resolveDialog(true)
    resolveDialog = null
  }
  dialog.value = false
}

const cancel = (): void => {
  if (resolveDialog) {
    resolveDialog(false)
    resolveDialog = null
  }
  dialog.value = false
}

defineExpose({ open })
</script>
