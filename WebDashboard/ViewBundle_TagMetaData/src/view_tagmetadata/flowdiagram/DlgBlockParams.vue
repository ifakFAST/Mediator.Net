<template>
  <v-dialog
    v-model="show"
    scrollable
    max-width="560px"
    @keydown="onKeyDown"
  >
    <v-card>
      <v-card-title>
        <span class="headline">Parameter {{ blockName }}</span>
      </v-card-title>

      <v-card-text>
        <table>
          <tbody>
            <tr
              v-for="p in editableParameters"
              :key="p.id"
            >
              <td>
                <div>{{ p.name }}</div>
              </td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-text-field
                  v-model="p.value"
                  v-if="p.type === 'Numeric' || p.type === 'String'"
                  variant="solo"
                  :rules="[(v: string) => validateValue(v, p)]"
                  style="width: 300px"
                ></v-text-field>
                <v-checkbox
                  v-model="p.value"
                  v-if="p.type === 'Bool'"
                  true-value="true"
                  false-value="false"
                  style="width: 300px; font-size: 16px; padding-bottom: 16px"
                ></v-checkbox>
                <v-select
                  v-model="p.value"
                  v-if="p.type === 'Enum'"
                  :items="p.enumValues"
                  variant="solo"
                  style="width: 300px"
                ></v-select>
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
          :disabled="!allParamsValid"
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
import * as modules from './module_types'

export interface ParamEntry {
  id: string
  name: string
  type: modules.ParamType
  minValue?: number
  maxValue?: number
  value: string
  enumValues?: string[]
}

// Props
const props = defineProps<{
  show: boolean
  block: simu.Block | null
}>()

// Emits
const emit = defineEmits<{
  'update:show': [value: boolean]
  changed: [parameters: ParamEntry[]]
}>()

// Utility function
function makeParamEntries(block: simu.Block | null): ParamEntry[] {
  if (block === null || (block.type !== 'Module' && block.type !== 'Port')) {
    return []
  }

  let paramValues: simu.Parameters = {}
  let paramDefs: modules.ParameterDef[] = []

  if (block.type === 'Module') {
    paramValues = (block as simu.ModuleBlock).parameters
    const blockType: modules.ModuleBlockType = modules.MapOfModuleTypes[(block as simu.ModuleBlock).moduleType]
    paramDefs = blockType.parameters
  } else {
    // Port
    const portBlock = block as simu.PortBlock
    paramValues['index'] = portBlock.index + ''
    paramValues['io'] = portBlock.io
    paramValues['lineType'] = portBlock.lineType
    paramValues['dimension'] = portBlock.dimension + ''
    paramDefs = [
      modules.paramNumeric('index', 'Index', 1, 0, 1000),
      modules.paramEnum('io', 'IO', 'In', ['In', 'Out']),
      modules.paramEnum('lineType', 'Line Type', 'Water', ['Water', 'Signal', 'Air']),
      modules.paramNumeric('dimension', 'Line Dimension', 1, 1, 1000),
    ]
  }

  return paramDefs.map((p) => {
    return {
      id: p.id,
      name: p.name,
      type: p.type,
      minValue: p.minValue,
      maxValue: p.maxValue,
      value: paramValues[p.id] === undefined ? p.defaultValue : paramValues[p.id],
      enumValues: p.enumValues,
    }
  })
}

// Reactive state
const parameters = ref<ParamEntry[]>(makeParamEntries(props.block))

// Computed
const show = computed({
  get: () => props.show,
  set: (value: boolean) => emit('update:show', value),
})

const editableParameters = computed((): ParamEntry[] => {
  return parameters.value.filter((p) => p.type !== 'Custom')
})

const blockName = computed((): string => {
  return props.block === null ? '???' : props.block.name
})

const allParamsValid = computed((): boolean => {
  return parameters.value.every((p) => validateValue(p.value, p) === true)
})

// Watchers
watch(
  () => props.block,
  (block: simu.Block | null) => {
    parameters.value = makeParamEntries(block)
  },
)

// Methods
const close = () => {
  emit('update:show', false)
}

const onOK = () => {
  close()
  emit('changed', parameters.value)
}

const validateValue = (value: string, p: ParamEntry): boolean | string => {
  switch (p.type) {
    case 'Numeric':
      const num: number = Number(value)
      if (value === '' || isNaN(num)) {
        return 'Not numeric!'
      }
      if (p.minValue !== undefined && num < p.minValue) {
        return 'Value too small'
      }
      if (p.maxValue !== undefined && num > p.maxValue) {
        return 'Value too big'
      }
      return true
    default:
      return true
  }
}

const onKeyDown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    close()
  }
}
</script>

<style>
.v-messages {
  min-height: 0px;
}

.v-text-field.v-text-field--solo .v-input__control {
  min-height: 0px;
  padding: 0;
}
</style>
