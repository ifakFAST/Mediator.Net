<template>
  <v-dialog
    v-model="dialog"
    max-width="350"
    @keydown.esc="cancel"
  >
    <v-card>
      <v-card-title>
        <span class="text-h5">Select Widget Type</span>
      </v-card-title>
      <v-card-text>
        <v-autocomplete
          v-model="type"
          item-title="name"
          item-value="id"
          :items="entries"
        ></v-autocomplete>
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
          :disabled="!canOK"
          variant="text"
          @click="ok"
          >OK</v-btn
        >
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

interface Entry {
  id: string
  name: string
}

const dialog = ref(false)
const type = ref<string | null>(null)
const entries = ref<Entry[]>([])

let resolve: (v: string | null) => void = (x) => {}

const open = (allTypes: string[], typeValue?: string): Promise<string | null> => {
  type.value = typeValue || null
  dialog.value = true
  entries.value = getEntries(allTypes)
  return new Promise<string | null>((resolvePromise) => {
    resolve = resolvePromise
  })
}

const canOK = computed((): boolean => {
  return type.value !== null
})

const ok = (): void => {
  resolve(type.value)
  dialog.value = false
}

const cancel = (): void => {
  resolve(null)
  dialog.value = false
}

const getEntries = (allTypes: string[]): Entry[] => {
  return allTypes.map((t) => {
    return { id: t, name: t }
  })
}

defineExpose({
  open,
})
</script>
