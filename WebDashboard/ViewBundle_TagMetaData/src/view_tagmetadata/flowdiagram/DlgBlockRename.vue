<template>
  <v-dialog
    v-model="show"
    scrollable
    max-width="460px"
    @keydown="onKeyDown"
  >
    <v-card>
      <v-card-title>
        <span class="headline">Rename block {{ blockName }}</span>
      </v-card-title>

      <v-card-text>
        <table>
          <tbody>
            <tr>
              <td><div>Block name</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-text-field
                  v-model="blockName"
                  ref="txtName"
                  variant="solo"
                  style="width: 300px"
                ></v-text-field>
              </td>
            </tr>
          </tbody>
        </table>
      </v-card-text>

      <v-card-actions>
        <v-spacer></v-spacer>
        <v-btn
          color="red-darken-1"
          variant="text"
          @click="close"
          >Cancel</v-btn
        >
        <v-btn
          color="blue-darken-1"
          variant="text"
          :disabled="!allValuesValid"
          @click="onOK"
          >OK</v-btn
        >
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import * as simu from './model_flow'

// Props
const props = defineProps<{
  show: boolean
  block: simu.Block | null
  diagram: simu.FlowDiagram | null
}>()

// Emits
const emit = defineEmits<{
  'update:show': [value: boolean]
  changed: [oldName: string, newName: string]
}>()

// Reactive state
const blockName = ref('')
const txtName = ref<HTMLElement | null>(null)

// Computed
const show = computed({
  get: () => props.show,
  set: (value: boolean) => emit('update:show', value),
})

const allValuesValid = computed((): boolean => {
  if (props.diagram === null) {
    return false
  }
  const name = blockName.value
  if (name === '') {
    return false
  }
  return props.diagram.blocks.every((b) => b.name !== name)
})

// Watchers
watch(
  () => props.block,
  (block: simu.Block | null) => {
    if (block === null) {
      blockName.value = ''
    } else {
      blockName.value = block.name

      setTimeout(() => {
        if (txtName.value) {
          ;(txtName.value as any).focus()
        }
      }, 300)
    }
  },
)

// Methods
const close = () => {
  emit('update:show', false)
}

const onOK = () => {
  close()

  if (blockName.value !== '' && props.block !== null) {
    emit('changed', props.block.name, blockName.value)
  }
}

const onKeyDown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    close()
  } else if (e.key === 'Enter' && allValuesValid.value) {
    onOK()
  }
}
</script>

<style></style>
