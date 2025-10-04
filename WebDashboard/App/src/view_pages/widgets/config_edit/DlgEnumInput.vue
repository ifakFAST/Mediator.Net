<template>
  <v-dialog
    v-model="dialog"
    max-width="350"
    @keydown.esc="cancel"
  >
    <v-card>
      <v-card-title>
        <span class="text-h5">{{ title }}</span>
      </v-card-title>
      <v-card-text>
        <p>{{ message }}</p>
        <v-container>
          <v-row>
            <v-btn
              v-for="it in values"
              :key="it"
              class="mx-2"
              :color="it === value ? 'primary' : ''"
              @click="value = it"
            >
              {{ it }}
            </v-btn>
          </v-row>
        </v-container>
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

// Reactive data
const dialog = ref(false)
const title = ref('')
const message = ref('')
const value = ref('')
const valueOriginal = ref('')
const values = ref<string[]>([])
let resolve: (v: string | null) => void = () => {}

// Computed properties
const canOK = computed(() => {
  return value.value !== valueOriginal.value
})

// Methods
const open = (titleText: string, messageText: string, valueText: string, valuesArray: string[]): Promise<string | null> => {
  title.value = titleText
  message.value = messageText
  value.value = valueText
  valueOriginal.value = valueText
  values.value = valuesArray
  dialog.value = true
  return new Promise<string | null>((resolvePromise) => {
    resolve = resolvePromise
  })
}

const ok = (): void => {
  resolve(value.value)
  dialog.value = false
}

const cancel = (): void => {
  resolve(null)
  dialog.value = false
}

// Expose methods for parent component
defineExpose({
  open,
})
</script>
