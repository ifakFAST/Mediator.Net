<template>
  <table cellspacing="10">
    <tbody>
      <tr>
        <td
          v-for="m in members"
          :key="m.Name"
        >
          <v-text-field
            v-if="m.Type === 'String' && m.IsScalar"
            :label="m.Name"
            style="min-width: 16ch"
            :model-value="value[m.Name]"
            @update:model-value="updateField(m.Name, $event)"
          ></v-text-field>
          <v-checkbox
            v-if="m.Type === 'Bool' && !m.IsArray"
            :label="m.Name"
            :model-value="value[m.Name]"
            :style="{ minWidth: minWidth(m.Name) + 'ex' }"
            @update:model-value="updateField(m.Name, $event)"
          ></v-checkbox>
          <v-text-field
            v-if="m.Type === 'Int32' && !m.IsArray"
            :label="m.Name"
            :model-value="value[m.Name]"
            @update:model-value="updateField(m.Name, $event)"
          ></v-text-field>
          <v-text-field
            v-if="m.Type === 'Int64' && !m.IsArray"
            :label="m.Name"
            :model-value="value[m.Name]"
            @update:model-value="updateField(m.Name, $event)"
          ></v-text-field>
          <v-text-field
            v-if="m.Type === 'Duration' && !m.IsArray"
            :label="m.Name"
            :model-value="value[m.Name]"
            style="width: 100px"
            @update:model-value="updateField(m.Name, $event)"
          ></v-text-field>
          <v-select
            v-if="m.Type === 'Enum' && m.IsScalar"
            :items="m.EnumValues"
            :label="m.Name"
            :model-value="value[m.Name]"
            @update:model-value="updateField(m.Name, $event)"
          ></v-select>
        </td>
      </tr>
    </tbody>
  </table>
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
  value: any
  members: StructMember[]
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:value': [value: any]
}>()

const updateField = (fieldName: string, newValue: any) => {
  const updatedValue = { ...props.value }
  updatedValue[fieldName] = newValue
  emit('update:value', updatedValue)
}

const minWidth = (text: string): number => {
  return 7 + 1.1 * text.length
}
</script>
