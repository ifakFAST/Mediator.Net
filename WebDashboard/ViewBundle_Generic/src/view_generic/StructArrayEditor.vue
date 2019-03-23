<template>
  <div style="overflow-x:auto;">
    <table cellspacing="10">

      <tr v-for="(item, idx) in values" :key="idx">
        <td v-for="m in members" :key="m.Name">
          <v-text-field v-model="values[idx][m.Name]" v-if="m.Type === 'String'   && m.IsScalar"  :label="m.Name"></v-text-field>
          <v-checkbox   v-model="values[idx][m.Name]" v-if="m.Type === 'Bool'     && !m.IsArray"  :label="m.Name" v-bind:style="{ minWidth: minWidth(m.Name) + 'ex' }"></v-checkbox>
          <v-text-field v-model="values[idx][m.Name]" v-if="m.Type === 'Int32'    && !m.IsArray"  :label="m.Name"></v-text-field>
          <v-text-field v-model="values[idx][m.Name]" v-if="m.Type === 'Int64'    && !m.IsArray"  :label="m.Name"></v-text-field>
          <v-text-field v-model="values[idx][m.Name]" v-if="m.Type === 'Duration' && !m.IsArray"  :label="m.Name" style="width: 100px;"></v-text-field>
          <v-select     v-model="values[idx][m.Name]" v-if="m.Type === 'Enum'     && m.IsScalar"  :label="m.Name" :items="m.EnumValues"></v-select>
        </td>
        <td><v-btn class="small"                @click="removeArrayItem(values, idx)"><v-icon>delete_forever</v-icon></v-btn></td>
        <td><v-btn class="small" v-if="idx > 0" @click="moveUpArrayItem(values, idx)"><v-icon>keyboard_arrow_up</v-icon></v-btn></td>
      </tr>

    </table>
    <v-btn style="float: right;" class="small" @click="addArrayItem()"><v-icon>add</v-icon></v-btn>
  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'

@Component
export default class StructArrayEditor extends Vue {

  @Prop(Array) members: any[]
  @Prop(Array) values: any[]
  @Prop(String) defaultValue: string

  removeArrayItem(array: any[], idx: number) {
    array.splice(idx, 1)
  }

  moveUpArrayItem(array: any[], idx: number) {
    if (idx > 0) {
      const item = array[idx]
      array.splice(idx, 1)
      array.splice(idx - 1, 0, item)
    }
  }

  addArrayItem() {
    this.values.push(JSON.parse(this.defaultValue))
  }

  minWidth(text) {
    return 7 + 1.1 * text.length
  }

}

</script>
