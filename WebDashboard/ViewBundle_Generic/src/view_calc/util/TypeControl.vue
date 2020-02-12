<template>
  <div style="min-width: 360px;">
    <history-editor v-if="type === 'History'"  :value="value" @input="$emit('input', $event)" hide-details></history-editor>
    <v-text-field   v-if="type === 'String'"   :value="value" @input="$emit('input', $event)" hide-details></v-text-field>
    <v-textarea     v-if="type === 'Text'"     :value="value" @input="$emit('input', $event)" :rows="3" outlined hide-details></v-textarea>
    <v-text-field   v-if="type === 'Number'"   :value="value" @input="$emit('input', convert2Num($event))" type="number" hide-details></v-text-field>
    <v-select       v-if="type === 'DataType'" :value="value" @input="$emit('input', $event)" :items="dataTypes" hide-details></v-select>
    <v-text-field   v-if="type === 'Duration'" :value="value" @input="$emit('input', $event)" hide-details></v-text-field>
    <v-switch       v-if="type === 'Boolean'"  :input-value="value" @change="$emit('input', $event)" hide-details></v-switch>
  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import HistoryEditor from '../../components/HistoryEditor.vue'
import { MemberTypeEnum } from './member_types'
import * as fast from '../../fast_types'

@Component({
  components: {
    HistoryEditor,
  },
})
export default class TypeControl extends Vue {

  @Prop() value: any
  @Prop(String) type: MemberTypeEnum

  dataTypes: fast.DataType[]  = fast.DataTypeValues

  convert2Num(str: string): any {
    if (str === '') { return '' }
    return Number(str)
  }

}

</script>
