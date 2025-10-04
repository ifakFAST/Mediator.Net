<template>
  <v-dialog
    v-model="dialog"
    max-width="350"
    @keydown.esc="cancel"
  >
    <v-card>
      <v-card-title>
        <span class="text-h5">Select Column Width</span>
      </v-card-title>
      <v-card-text>
        <v-autocomplete
          v-model="width"
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
          variant="text"
          @click="agree"
          >OK</v-btn
        >
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import * as model from './model'

interface Entry {
  id: model.ColumnWidth
  name: string
}

const dialog = ref(false)
const width = ref<model.ColumnWidth>('Fill')
const entries = ref<Entry[]>([])

let resolve: (v: model.ColumnWidth) => void = (x) => {}

const open = (w: model.ColumnWidth): Promise<model.ColumnWidth> => {
  width.value = w
  dialog.value = true
  entries.value = getEntries()
  return new Promise<model.ColumnWidth>((resolvePromise) => {
    resolve = resolvePromise
  })
}

const agree = (): void => {
  resolve(width.value)
  dialog.value = false
}

const cancel = (): void => {
  resolve(width.value)
  dialog.value = false
}

const getEntries = (): Entry[] => {
  return [
    { id: 'Fill', name: 'Fill' },
    { id: 'Auto', name: 'Auto' },
    { id: 'OneOfTwelve', name: '1 of 12' },
    { id: 'TwoOfTwelve', name: '2 of 12' },
    { id: 'ThreeOfTwelve', name: '3 of 12' },
    { id: 'FourOfTwelve', name: '4 of 12' },
    { id: 'FiveOfTwelve', name: '5 of 12' },
    { id: 'SixOfTwelve', name: '6 of 12' },
    { id: 'SevenOfTwelve', name: '7 of 12' },
    { id: 'EightOfTwelve', name: '8 of 12' },
    { id: 'NineOfTwelve', name: '9 of 12' },
    { id: 'TenOfTwelve', name: '10 of 12' },
    { id: 'ElevenOfTwelve', name: '11 of 12' },
    { id: 'TwelveOfTwelve', name: '12 of 12' },
  ]
}

defineExpose({
  open,
})
</script>
