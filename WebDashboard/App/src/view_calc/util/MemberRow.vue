<template>
  <tr>
    <td class="FieldName">
      <div style="margin-top: 4px; padding-top: 12px">{{ name }}</div>
    </td>

    <td><div style="min-width: 10px">&nbsp;</div></td>

    <template v-if="!optional">
      <td>
        <TypeControl
          v-model="model"
          :enum-values="enumValues"
          :type="type"
        />
      </td>
      <td>&nbsp;</td>
    </template>

    <template v-if="optional && model !== null">
      <td>
        <TypeControl
          v-model="model"
          :enum-values="enumValues"
          :type="type"
        />
      </td>
      <td>
        <v-btn
          class="small"
          style="margin-top: 16px"
          @click="model = null"
        >
          <v-icon>mdi-delete</v-icon>
        </v-btn>
      </td>
    </template>

    <template v-if="optional && model === null">
      <td><div style="margin-top: 4px; padding-top: 12px">Not set</div></td>
      <td>
        <v-btn
          class="small"
          style="margin-top: 16px"
          @click="updateSetDefault"
        >
          Set
        </v-btn>
      </td>
    </template>
  </tr>
</template>

<script setup lang="ts">
import TypeControl from './TypeControl.vue'
import type { MemberTypeEnum } from './member_types'
import { defaultValueFromMemberType } from './member_types'

const props = defineProps<{
  name: string
  type: MemberTypeEnum
  optional: boolean
  enumValues?: string[]
}>()

const model = defineModel<any>({ required: true })

const updateSetDefault = (): void => {
  model.value = defaultValueFromMemberType(props.type)
}
</script>

<style>
.FieldName {
  font-weight: bold;
}
</style>
