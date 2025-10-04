<template>
  <v-dialog
    v-model="dialog"
    max-width="600"
    @keydown.esc="cancel"
  >
    <v-card>
      <v-card-title>
        <span class="text-h5">Config Variables</span>
      </v-card-title>
      <v-card-text>
        <v-container
          class="pa-0 pt-6"
          fluid
        >
          <template
            v-for="(variable, index) in configVariables"
            :key="index"
          >
            <v-row class="mb-2">
              <v-col
                class="py-0"
                cols="auto"
              >
                <v-text-field
                  v-model="variable.ID"
                  label="ID"
                  style="max-width: 115px"
                />
              </v-col>
              <v-col
                class="py-0"
                cols="fill"
              >
                <v-text-field
                  v-model="variable.DefaultValue"
                  label="Default Value"
                />
              </v-col>
              <v-col
                class="py-0 pl-0 d-flex"
                cols="auto"
              >
                <div class="d-flex flex-row">
                  <v-btn
                    color="error"
                    icon="mdi-delete"
                    @click="removeVariable(index)"
                  ></v-btn>
                  <v-btn
                    :disabled="index === 0"
                    icon="mdi-arrow-up"
                    @click="moveVariable(index, 'up')"
                  ></v-btn>
                </div>
              </v-col>
            </v-row>
          </template>
        </v-container>

        <div class="text-center">
          <v-btn
            class="mt-2"
            color="primary"
            variant="text"
            @click="addVariable"
          >
            Add Variable
          </v-btn>
        </div>
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
          @click="onOK"
          >OK</v-btn
        >
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { ConfigVariable } from './model'

const dialog = ref(false)
const configVariables = ref<ConfigVariable[]>([])

let resolve: (v: ConfigVariable[] | null) => void = (x) => {}

const open = (configVariablesValue: ConfigVariable[]): Promise<ConfigVariable[] | null> => {
  configVariables.value = JSON.parse(JSON.stringify(configVariablesValue)) // Deep copy
  dialog.value = true
  return new Promise<ConfigVariable[] | null>((resolvePromise) => {
    resolve = resolvePromise
  })
}

const moveVariable = (index: number, direction: 'up' | 'down'): void => {
  const newIndex = direction === 'up' ? index - 1 : index + 1

  if (newIndex < 0 || newIndex >= configVariables.value.length) {
    return
  }

  const temp = configVariables.value[index]
  configVariables.value[index] = configVariables.value[newIndex]
  configVariables.value[newIndex] = temp
}

const addVariable = (): void => {
  configVariables.value.push({
    ID: '',
    DefaultValue: '',
  })
}

const removeVariable = (index: number): void => {
  configVariables.value.splice(index, 1)
}

const onOK = async (): Promise<void> => {
  try {
    await (window.parent as any).dashboardApp.sendViewRequestAsync('SaveConfigVariables', { configVariables: configVariables.value })
    resolve(configVariables.value)
    dialog.value = false
  } catch (err) {
    const exp = err as Error
    alert(exp.message)
    return
  }
}

const cancel = (): void => {
  resolve(null)
  dialog.value = false
}

defineExpose({
  open,
})
</script>
