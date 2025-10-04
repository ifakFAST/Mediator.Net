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
        {{ message }}
        <v-text-field
          ref="editText"
          v-model="value"
          :error="!canOK"
          :hint="error"
        ></v-text-field>
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
import { ref, computed, nextTick } from 'vue'

const dialog = ref(false)
const title = ref('')
const message = ref('')
const value = ref('')
const editText = ref()

let isValid: (s: string) => string = (x) => (x.length > 0 ? '' : 'Must not be empty')
let resolve: (v: string | null) => void = (x) => {}

const openWithValidator = (titleValue: string, messageValue: string, valueValue: string, valid: (str: string) => string): Promise<string | null> => {
  isValid = valid
  return open(titleValue, messageValue, valueValue)
}

const error = computed((): string => {
  return isValid(value.value)
})

const open = async (titleValue: string, messageValue: string, valueValue: string): Promise<string | null> => {
  title.value = titleValue
  message.value = messageValue
  value.value = valueValue
  dialog.value = true

  await nextTick()
  setTimeout(() => {
    if (editText.value) {
      editText.value.focus()
    }
  }, 100)

  return new Promise<string | null>((resolvePromise) => {
    resolve = resolvePromise
  })
}

const canOK = computed((): boolean => {
  return isValid(value.value) === ''
})

const ok = (): void => {
  resolve(value.value)
  dialog.value = false
}

const cancel = (): void => {
  resolve(null)
  dialog.value = false
}

defineExpose({
  open,
  openWithValidator,
})
</script>
