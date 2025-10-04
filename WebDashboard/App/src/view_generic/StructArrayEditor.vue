<template>
  <div style="overflow-x: auto">
    <table cellspacing="10">
      <tbody>
        <tr
          v-for="(item, idx) in values"
          :key="idx"
        >
          <td
            v-for="m in members"
            :key="m.Name"
          >
            <v-text-field
              v-if="m.Type === 'String' && m.IsScalar"
              :label="m.Name"
              :model-value="values[idx][m.Name]"
              @update:model-value="updateField(idx, m.Name, $event)"
            ></v-text-field>
            <v-checkbox
              v-if="m.Type === 'Bool' && !m.IsArray"
              :label="m.Name"
              :model-value="values[idx][m.Name]"
              :style="{ minWidth: minWidth(m.Name) + 'ex' }"
              @update:model-value="updateField(idx, m.Name, $event)"
            ></v-checkbox>
            <v-text-field
              v-if="m.Type === 'Int32' && !m.IsArray"
              :label="m.Name"
              :model-value="values[idx][m.Name]"
              @update:model-value="updateField(idx, m.Name, $event)"
            ></v-text-field>
            <v-text-field
              v-if="m.Type === 'Int64' && !m.IsArray"
              :label="m.Name"
              :model-value="values[idx][m.Name]"
              @update:model-value="updateField(idx, m.Name, $event)"
            ></v-text-field>
            <v-text-field
              v-if="m.Type === 'Duration' && !m.IsArray"
              :label="m.Name"
              :model-value="values[idx][m.Name]"
              style="width: 100px"
              @update:model-value="updateField(idx, m.Name, $event)"
            ></v-text-field>
            <v-select
              v-if="m.Type === 'Enum' && m.IsScalar"
              :items="m.EnumValues"
              :label="m.Name"
              :model-value="values[idx][m.Name]"
              @update:model-value="updateField(idx, m.Name, $event)"
            ></v-select>
          </td>
          <td>
            <v-btn
              icon="mdi-delete-forever"
              size="small"
              @click="removeArrayItem(values, idx)"
            ></v-btn>
          </td>
          <td>
            <v-btn
              v-if="idx > 0"
              icon="mdi-chevron-up"
              size="small"
              @click="moveUpArrayItem(values, idx)"
            ></v-btn>
          </td>
        </tr>
      </tbody>
    </table>
    <v-btn
      icon="mdi-plus"
      size="small"
      style="float: right"
      @click="addArrayItem()"
    ></v-btn>
  </div>
</template>

<script setup lang="ts">
interface StructMember {
  Name: string
  Type: string
  IsScalar: boolean
  IsArray: boolean
  EnumValues: string[]
}

interface Props {
  members: StructMember[]
  values: any[]
  defaultValue: string
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:values': [values: any[]]
}>()

const updateField = (index: number, fieldName: string, newValue: any) => {
  const updatedValues = [...props.values]
  updatedValues[index] = { ...updatedValues[index] }
  updatedValues[index][fieldName] = newValue
  emit('update:values', updatedValues)
}

const removeArrayItem = (array: any[], idx: number) => {
  const updatedValues = [...props.values]
  updatedValues.splice(idx, 1)
  emit('update:values', updatedValues)
}

const moveUpArrayItem = (array: any[], idx: number) => {
  if (idx > 0) {
    const updatedValues = [...props.values]
    const item = updatedValues[idx]
    updatedValues.splice(idx, 1)
    updatedValues.splice(idx - 1, 0, item)
    emit('update:values', updatedValues)
  }
}

const addArrayItem = () => {
  const updatedValues = [...props.values]
  updatedValues.push(JSON.parse(props.defaultValue))
  emit('update:values', updatedValues)
}

const minWidth = (text: string): number => {
  return 7 + 1.1 * text.length
}
</script>
