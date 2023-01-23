<template>

  <tr>

    <td class="FieldName"><div style="margin-top:4px;padding-top:12px;">{{ name }}</div></td>

    <td><div style="min-width: 10px;">&nbsp;</div></td>

    <template v-if="!optional">
      <td>
        <type-control :type="type" :value="value" @input="update($event)" :enumValues="enumValues"></type-control>
      </td>
      <td>&nbsp;</td>
    </template>

    <template v-if="optional && value !== null">
      <td>
        <type-control :type="type" :value="value" @input="update($event)" :enumValues="enumValues"></type-control>
      </td>
      <td><v-btn style="margin-top:16px;" class="small" @click="update(null)"><v-icon>delete_forever</v-icon></v-btn></td>
    </template>

    <template v-if="optional && value === null">
      <td><div style="margin-top:4px;padding-top:12px;">Not set</div></td>
      <td><v-btn style="margin-top:16px;" class="small" @click="updateSetDefault">Set</v-btn></td>
    </template>

  </tr>

</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import TypeControl from './TypeControl.vue'
import { MemberTypeEnum, defaultValueFromMemberType } from './member_types'

@Component({
  components: {
    TypeControl,
  },
})
export default class MemberRow extends Vue {

  @Prop(String) name: string
  @Prop(Object) value: object
  @Prop(String) type: MemberTypeEnum
  @Prop(Boolean) optional: boolean
  @Prop(Array) enumValues: string[]

  update(newValue: any): void {
    this.$emit('input', newValue)
  }

  updateSetDefault() {
    this.update(defaultValueFromMemberType(this.type))
  }
}

</script>

<style>

  .FieldName {
    font-weight: bold;
  }

</style>