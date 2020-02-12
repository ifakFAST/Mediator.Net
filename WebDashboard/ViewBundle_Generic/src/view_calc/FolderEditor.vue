<template>

  <table cellspacing="25">

    <member-row name="Name"    v-model="value.Name"    type="String"  :optional="false"></member-row>
    <member-row name="History" v-model="value.History" type="History" :optional="true"></member-row>

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
export default class FolderEditor extends Vue {

  @Prop(Object) value: calcmodel.Folder

}

</script>
