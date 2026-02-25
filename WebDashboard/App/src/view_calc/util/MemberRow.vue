<template>
  <tr>
    <td
      class="FieldName"
      style="vertical-align: top"
    >
      <div style="padding-top: 8px; display: flex; align-items: center; gap: 6px;">
        <span>{{ name }}</span>
        <v-tooltip
          v-if="tooltip || tooltipHtml"
          location="top"
        >
          <template #activator="{ props: tooltipProps }">
            <v-icon
              size="16"
              color="primary"
              style="cursor: help;"
              v-bind="tooltipProps"
            >
              mdi-help-circle-outline
            </v-icon>
          </template>
          <div
            :style="{ whiteSpace: tooltipHtml ? 'normal' : 'pre-line', maxWidth: '700px' }"
          >
            <span
              v-if="tooltipHtml"
              v-html="tooltipHtml"
            />
            <span v-else>{{ tooltip }}</span>
          </div>
        </v-tooltip>
      </div>
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
          @click="model = null"
        >
          <v-icon>mdi-delete</v-icon>
        </v-btn>
      </td>
    </template>

    <template v-if="optional && model === null">
      <td><div style="margin-top: 4px;">Not set</div></td>
      <td>
        <v-btn
          class="small"
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
  tooltip?: string
  tooltipHtml?: string
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
