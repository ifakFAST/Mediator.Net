<template>
  <tr>
    <td
      class="FieldName"
      style="vertical-align: top"
    >
      <div style="padding-top: 8px">Root Objects</div>
    </td>

    <td><div style="min-width: 10px">&nbsp;</div></td>

    <td colspan="2">
      <div style="min-width: 360px">
        <div
          v-for="(obj, index) in model"
          :key="index"
          class="d-flex align-center mb-1"
        >
          <v-text-field
            :model-value="obj"
            density="compact"
            hide-details
            readonly
            style="max-width: 400px"
          />
          <v-btn
            class="ml-1"
            icon
            size="small"
            @click="removeRootObject(index)"
          >
            <v-icon>mdi-delete</v-icon>
          </v-btn>
        </div>
        <v-btn
          size="small"
          @click="showSelectDialog"
        >
          <v-icon start>mdi-plus</v-icon>
          Add Root Object
        </v-btn>
      </div>
    </td>
  </tr>

  <DlgObjectSelect
    v-model="selectDialogVisible"
    :module-id="defaultModuleId"
    :modules="modules"
    object-id=""
    @onselected="onObjectSelected"
  />
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ModuleInfo } from './model'

const props = defineProps<{
  modules: ModuleInfo[]
}>()

const model = defineModel<string[]>({ required: true })

const selectDialogVisible = ref(false)

const defaultModuleId = computed((): string => {
  return props.modules.length > 0 ? props.modules[0].ID : ''
})

const removeRootObject = (index: number): void => {
  const copy = [...model.value]
  copy.splice(index, 1)
  model.value = copy
}

const showSelectDialog = (): void => {
  selectDialogVisible.value = true
}

const onObjectSelected = (obj: { ID: string; Name: string }): void => {
  const encoded = obj.ID
  if (encoded && encoded.includes(':')) {
    const alreadyExists = model.value.some((r) => r === encoded)
    if (!alreadyExists) {
      model.value = [...model.value, encoded]
    }
  }
}
</script>

<style>
.FieldName {
  font-weight: bold;
}
</style>
