<template>
  <v-dialog
    v-model="dialog"
    :max-width="options.width"
    @keydown.esc="cancel"
  >
    <v-card>
      <v-toolbar
        :color="options.color"
        dark
        density="compact"
        flat
      >
        <v-toolbar-title class="text-white">{{ title }}</v-toolbar-title>
      </v-toolbar>
      <v-card-text v-show="!!message">{{ message }}</v-card-text>
      <v-card-actions class="pt-0">
        <v-spacer />
        <v-btn
          color="grey-darken-1"
          variant="text"
          @click="cancel"
        >
          Cancel
        </v-btn>
        <v-btn
          color="primary-darken-1"
          variant="text"
          @click="agree"
        >
          OK
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const dialog = ref(false)
const resolve = ref<((value: boolean) => void) | null>(null)
const message = ref<string | null>(null)
const title = ref<string | null>(null)
const options = ref({
  color: 'primary',
  width: 350,
})

const open = (titleText: string, messageText: string, dialogOptions: { color?: string; width?: number } = {}): Promise<boolean> => {
  dialog.value = true
  title.value = titleText
  message.value = messageText
  options.value = { ...options.value, ...dialogOptions }

  return new Promise((resolvePromise) => {
    resolve.value = resolvePromise
  })
}

const agree = (): void => {
  if (resolve.value) {
    resolve.value(true)
  }
  dialog.value = false
}

const cancel = (): void => {
  if (resolve.value) {
    resolve.value(false)
  }
  dialog.value = false
}

defineExpose({ open })
</script>
