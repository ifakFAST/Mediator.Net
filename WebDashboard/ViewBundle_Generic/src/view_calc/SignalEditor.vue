<template>

  <table cellspacing="25">

    <member-row name="Name"      v-model="value.Name"      type="String"   :optional="false"></member-row>
    <member-row name="Unit"      v-model="value.Unit"      type="String"   :optional="false"></member-row>
    <member-row name="Type"      v-model="value.Type"      type="DataType" :optional="false"></member-row>
    <member-row name="Dimension" v-model="value.Dimension" type="Number"   :optional="false"></member-row>
    <member-row name="Comment"   v-model="value.Comment"   type="Text"     :optional="false"></member-row>
    <member-row name="History"   v-model="value.History"   type="History"  :optional="true"></member-row>

  </table>

</template>

<script lang="ts">

import { Component, Prop, Vue } from 'vue-property-decorator'
import * as calcmodel from './model'
import MemberRow from './util/MemberRow.vue'

@Component({
  components: {
    MemberRow,
  },
})
export default class SignalEditor extends Vue {

  @Prop(Object) value: calcmodel.Signal

}

</script>
