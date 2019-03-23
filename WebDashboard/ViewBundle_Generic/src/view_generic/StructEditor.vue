<template>
  <table cellspacing="10">
    <tr>
      <td v-for="m in members" :key="m.Name">
        <v-text-field   v-model="value[m.Name]" v-if="m.Type === 'String'   && m.IsScalar"  :label="m.Name"></v-text-field>
        <v-checkbox     v-model="value[m.Name]" v-if="m.Type === 'Bool'     && !m.IsArray"  :label="m.Name" v-bind:style="{ minWidth: minWidth(m.Name) + 'ex' }"></v-checkbox>
        <v-text-field   v-model="value[m.Name]" v-if="m.Type === 'Int32'    && !m.IsArray"  :label="m.Name"></v-text-field>
        <v-text-field   v-model="value[m.Name]" v-if="m.Type === 'Int64'    && !m.IsArray"  :label="m.Name"></v-text-field>
        <text-field-ext v-model="value[m.Name]" v-if="m.Type === 'Duration' && !m.IsArray"  :label="m.Name" style="width: 100px;"></text-field-ext>
        <v-select       v-model="value[m.Name]" v-if="m.Type === 'Enum'     && m.IsScalar"  :label="m.Name" :items="m.EnumValues"></v-select>
      </td>
    </tr>
  </table>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'

import TextFieldExt from './TextFieldExt.vue'

@Component({
  components: {
    TextFieldExt,
  },
})
export default class StructEditor extends Vue {

  @Prop(Object) value: any
  @Prop(Array) members: any[]

  minWidth(text) {
    return 7 + 1.1 * text.length
  }

}

</script>
