<template>
  <v-dialog
    v-model="dialog"
    max-width="900"
    @keydown.esc="cancel"
  >
    <v-card>
      <v-card-title class="px-6 pt-5 pb-1">
        <span class="text-h5">Config Variables</span>
      </v-card-title>
      <v-card-subtitle class="px-6 pb-2">
        Define the variable IDs and their default values.
      </v-card-subtitle>
      <v-card-text class="px-6 pt-4">
        <div
          v-for="(variable, index) in configVariables"
          :key="index"
          class="config-variable-row"
        >
          <v-text-field
            v-model="variable.ID"            
            hide-details="auto"
            label="ID"
            variant="outlined"
          />
          <v-text-field
            v-model="variable.DefaultValue"
            hide-details="auto"
            label="Default Value"
            variant="outlined"
          />
          <div class="config-variable-actions">
            <v-btn
              :aria-label="`Move variable ${index + 1} up`"
              :disabled="index === 0"
              icon="mdi-arrow-up"
              size="small"
              variant="text"
              @click="moveVariable(index, 'up')"
            ></v-btn>
            <v-btn
              :aria-label="`Delete variable ${index + 1}`"
              color="error"
              icon="mdi-delete-outline"
              size="small"
              variant="text"
              @click="removeVariable(index)"
            ></v-btn>
          </div>
        </div>

        <div class="mt-2">
          <v-btn
            color="primary"
            prepend-icon="mdi-plus"
            variant="text"
            @click="addVariable"
          >
            Add Variable
          </v-btn>
        </div>
      </v-card-text>
      <v-divider></v-divider>
      <v-card-actions class="px-6 py-3">
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

<style scoped>
.config-variable-row {
  display: grid;
  grid-template-columns: minmax(280px, 1fr) minmax(320px, 1.35fr) auto;
  gap: 12px;
  align-items: start;
  margin-bottom: 12px;
}

.config-variable-actions {
  display: flex;
  gap: 2px;
  padding-top: 4px;
}

@media (max-width: 720px) {
  .config-variable-row {
    grid-template-columns: 1fr;
  }

  .config-variable-actions {
    justify-content: flex-end;
    padding-top: 0;
  }
}
</style>
