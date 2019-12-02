<template>
  <div>

    <v-toolbar dense>
      <v-toolbar-title>{{ title }}</v-toolbar-title>
      <v-spacer></v-spacer>
      <v-btn text @click="save" :disabled="!isDirty">Save</v-btn>
      <v-btn text @click="deleteObject">Delete</v-btn>
      <v-btn v-for="type in childTypes" :key="type.TypeName" text @click="showAddChildDialog(type)">Add {{getShortTypeName(type.TypeName)}}</v-btn>
      <v-btn style="min-width: 18px;" text @click="moveUp" :disabled="selection === null || selection.First"><v-icon>arrow_upward</v-icon></v-btn>
      <v-btn style="min-width: 18px;" text @click="moveDown" :disabled="selection === null || selection.Last"><v-icon>arrow_downward</v-icon></v-btn>
    </v-toolbar>

    <v-toolbar flat dense color="white" style="margin-top: 8px;">
      <a v-bind:class="classObject(cat.Category)" text v-for="cat in categoryMembers" :key="cat.Category" @click="currentTab = cat.Category">{{cat.Category}}</a>
      <a v-bind:class="classObject('Variables')" text @click="currentTab = 'Variables'">Variables</a>
    </v-toolbar>

    <div v-for="cat in categoryMembers" :key="cat.Category">
      <div v-if="currentTab === cat.Category">
          <form>
            <table cellspacing="10">

                <template v-for="row in cat.Members">

                  <tr v-if="row.IsScalar" :key="row.Key">
                      <td>
                        <v-text-field v-model="row.Value" v-if="row.Type === 'String'"   :label="row.Name" :readonly="row.Name ==='ID'"></v-text-field>
                        <v-text-field v-model="row.Value" v-if="row.Type === 'JSON'"     :label="row.Name"></v-text-field>
                        <v-checkbox   v-model="row.Value" v-if="row.Type === 'Bool'"     :label="row.Name"></v-checkbox>
                        <v-text-field v-model="row.Value" v-if="row.Type === 'Int32'"    :label="row.Name"></v-text-field>
                        <v-text-field v-model="row.Value" v-if="row.Type === 'Int64'"    :label="row.Name"></v-text-field>
                        <v-text-field v-model="row.Value" v-if="row.Type === 'Duration'" :label="row.Name"></v-text-field>
                        <v-select     v-model="row.Value" v-if="row.Type === 'Enum'"     :label="row.Name" :items="row.EnumValues"></v-select>
                        <template                         v-if="row.Type === 'Struct'" >
                            <p class="bold">{{row.Name}}</p>
                            <struct-editor :members="row.StructMembers" :value="row.Value"></struct-editor>
                        </template>
                      </td>
                      <td>
                        <v-btn @click="loadBrowseable(row.Name, row.Value)" v-if="row.Type === 'String' && row.Browseable" style="min-width: 18px;">Browse</v-btn>
                      </td>
                      <td>&nbsp;</td>
                      <td>&nbsp;</td>
                  </tr>

                  <tr v-if="row.IsOption && row.Value !== null" :key="row.Key">
                      <td>
                        <v-text-field v-model="row.Value" v-if="row.Type === 'String'"   :label="row.Name"></v-text-field>
                        <v-text-field v-model="row.Value" v-if="row.Type === 'JSON'"     :label="row.Name"></v-text-field>
                        <v-checkbox   v-model="row.Value" v-if="row.Type === 'Bool'"     :label="row.Name"></v-checkbox>
                        <v-text-field v-model="row.Value" v-if="row.Type === 'Int32'"    :label="row.Name"></v-text-field>
                        <v-text-field v-model="row.Value" v-if="row.Type === 'Int64'"    :label="row.Name"></v-text-field>
                        <v-text-field v-model="row.Value" v-if="row.Type === 'Duration'" :label="row.Name"></v-text-field>
                        <v-select     v-model="row.Value" v-if="row.Type === 'Enum'"     :label="row.Name" :items="row.EnumValues"></v-select>
                        <template                         v-if="row.Type === 'Struct'" >
                            <p class="bold">{{row.Name}}</p>
                            <struct-editor :members="row.StructMembers" :value="row.Value"></struct-editor>
                        </template>
                      </td>
                      <td>
                        <v-btn class="small" @click="removeOptional(row)"><v-icon>delete_forever</v-icon></v-btn>
                      </td>
                      <td>&nbsp;</td>
                      <td>&nbsp;</td>
                  </tr>

                  <tr v-if="row.IsOption && row.Value === null" :key="row.Key">
                      <td>
                        <span class="bold">{{row.Name}}</span>: Not set
                      </td>
                      <td>
                        <v-btn class="small" @click="setOptional(row)">Set</v-btn>
                      </td>
                      <td>&nbsp;</td>
                      <td>&nbsp;</td>
                  </tr>

                  <template v-if="row.IsArray && row.Type !=='Struct'">
                      <tr :key="row.Key + 'r0'">
                        <td colspan="4"><p class="bold">{{row.Name}}</p></td>
                      </tr>
                      <tr v-for="(v, idx) in row.Value" :key="row.Key + '_' + idx">
                        <td>
                            <v-text-field v-model="row.Value[idx]" v-if="row.Type === 'String'"   :label="'[' + idx + ']'"></v-text-field>
                            <v-text-field v-model="row.Value[idx]" v-if="row.Type === 'JSON'"     :label="'[' + idx + ']'"></v-text-field>
                            <v-checkbox   v-model="row.Value[idx]" v-if="row.Type === 'Bool'"     :label="'[' + idx + ']'"></v-checkbox>
                            <v-text-field v-model="row.Value[idx]" v-if="row.Type === 'Int32'"    :label="'[' + idx + ']'"></v-text-field>
                            <v-text-field v-model="row.Value[idx]" v-if="row.Type === 'Int64'"    :label="'[' + idx + ']'"></v-text-field>
                            <v-text-field v-model="row.Value[idx]" v-if="row.Type === 'Duration'" :label="'[' + idx + ']'"></v-text-field>
                            <v-select     v-model="row.Value[idx]" v-if="row.Type === 'Enum'"     :label="'[' + idx + ']'" :items="row.EnumValues"></v-select>
                            <table v-if="row.Type === 'NamedValue'">
                              <tr>
                                  <td><v-text-field v-model="row.Value[idx].Name"  label="Name"></v-text-field></td>
                                  <td><v-text-field v-model="row.Value[idx].Value" label="Value"></v-text-field></td>
                              </tr>
                            </table>
                        </td>
                        <td><v-btn class="small"                @click="removeArrayItem(row.Value, idx)"><v-icon>delete_forever</v-icon></v-btn></td>
                        <td><v-btn class="small" v-if="idx > 0" @click="moveUpArrayItem(row.Value, idx)"><v-icon>keyboard_arrow_up</v-icon></v-btn></td>
                        <td>&nbsp;</td>
                      </tr>
                      <tr :key="row.Key + 'rl'">
                        <td style="text-align:right"><v-btn class="small" @click="addArrayItem(row)"><v-icon>add</v-icon></v-btn></td>
                        <td>&nbsp;</td>
                        <td>&nbsp;</td>
                        <td>&nbsp;</td>
                      </tr>
                  </template>

                  <template v-if="row.IsArray && row.Type ==='Struct'">
                      <tr :key="row.Key + '.0'">
                        <td colspan="4"><p class="bold">{{row.Name}}</p></td>
                      </tr>
                      <tr :key="row.Key + '.1'">
                        <td colspan="4"><struct-array-editor :members="row.StructMembers" :values="row.Value" :defaultValue="row.DefaultValue"></struct-array-editor></td>
                      </tr>
                  </template>

                </template>
            </table>
          </form>
      </div>
    </div>

    <div v-if="currentTab === 'Variables'">

      <v-data-table no-data-text="No variables" :headers="varHeaders" :items="selection.Variables" hide-default-footer class="elevation-1 mx-1 mt-2 mb-1">
          <template v-slot:item="{ item }">
            <tr>
              <td>{{ item.Name }}</td>
              <td class="text-right">
                <a @click.stop="editVar(item)">{{ item.V }}</a>
              </td>
              <td v-bind:style="{ color: qualityColor(item.Q) }" class="text-right">{{ item.Q }}</td>
              <td class="text-right">{{ item.T }}</td>
            </tr>
          </template>
      </v-data-table>

      <v-dialog v-model="editVarDialog" max-width="290" @keydown="editVarKeydown">
          <v-card>
            <v-card-title class="headline">Write Variable Value</v-card-title>
            <v-card-text>
            <v-text-field ref="editTextInput" label="Edit" v-model="editVarTmp" single-line autofocus clearable :rules="[validateVariableValue]"></v-text-field>
            </v-card-text>
            <v-card-actions>
                <v-spacer></v-spacer>
                <v-btn color="blue darken-1" text @click.stop="editVarDialog = false">Cancel</v-btn>
                <v-btn color="blue darken-1" text @click.stop="editVarWrite">Write</v-btn>
            </v-card-actions>
          </v-card>
      </v-dialog>

    </div>


    <v-dialog v-model="addDialog.show" persistent max-width="350px" @keydown="editKeydown">
      <v-card>
          <v-card-title>
            <span class="headline">Add new {{addDialog.typeNameShort}}</span>
          </v-card-title>
          <v-card-text>
            <v-text-field v-model="addDialog.newID"   label="ID"   ref="txtID" hint="The unique and immutable identifier of the new object" autofocus></v-text-field>
            <v-text-field v-model="addDialog.newName" label="Name" ref="txtName"></v-text-field>
          </v-card-text>
          <v-card-actions>
            <v-spacer></v-spacer>
            <v-btn color="blue darken-1" text @click.native="onAddNewObject">Add</v-btn>
            <v-btn color="red  darken-1" text @click.native="addDialog.show = false">Cancel</v-btn>
          </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="browseDialog.show" persistent scrollable max-width="700px" @keydown="browseKeydown">
      <v-card>
          <v-card-title>
            <v-text-field v-model="search" label="Search Text" ref="txtSearch" autofocus></v-text-field>
          </v-card-title>
          <v-divider></v-divider>
          <v-card-text style="height: 520px;">
            <v-radio-group v-model="browseDialog.selection" column>
              <v-radio v-for="(x, i) in filteredBrowseValues" :key="i" :label="x" :value="x" style="max-width: 95%;"></v-radio>
            </v-radio-group>
          </v-card-text>
          <v-divider></v-divider>
          <v-card-actions>
            <v-spacer></v-spacer>
            <v-btn color="blue darken-1" text @click.native="browseOK">OK</v-btn>
            <v-btn color="red  darken-1" text @click.native="browseDialog.show = false">Cancel</v-btn>
          </v-card-actions>
      </v-card>
    </v-dialog>

    <confirm ref="confirm"></confirm>
  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'

import StructEditor from './StructEditor.vue'
import StructArrayEditor from './StructArrayEditor.vue'
import Confirm from '../components/Confirm.vue'
import { TreeNode, TypeMap, ObjMemInfo, ObjectMember, ChildType, AddObjectParams, SaveMember } from './types'

@Component({
  components: {
    StructEditor,
    StructArrayEditor,
    Confirm,
  },
})
export default class ObjectEditor extends Vue {

  @Prop(Object) selection: TreeNode
  @Prop(Array) members: ObjectMember[]
  @Prop(Array) childTypes: ChildType[]

  currentTab = ''
  addDialog = {
    show: false,
    memberName: '',
    typeNameShort: '',
    typeNameFull: '',
    newID: '',
    newName: '',
  }
  browseDialog = {
    show: false,
    memberName: '',
    memberIdx: 0,
    selection: '',
  }
  search = ''
  varHeaders = [
    { text: 'Variable', align: 'left', sortable: false, value: 'Name' },
    { text: 'Value', align: 'right', sortable: false, value: 'V' },
    { text: 'Quality', align: 'right', sortable: false, value: 'Q' },
    { text: 'Timestamp', align: 'right', sortable: false, value: 'T' },
  ]
  editVarDialog = false
  editVarName = ''
  editVarTmp = ''

  get categoryMembers() {
    const categories = []
    const members = this.members
    for (const member of members) {
      const cat = member.Category
      const idx = categories.findIndex((c) => c === cat)
      if (idx < 0) {
        categories.push(cat)
      }
    }
    return categories.map((c) => {
      return {
        Category: (c === '' ? 'Members' : c),
        Members: members.filter((m) => m.Category === c),
      }
    })
  }

  get isDirty() {
    return -1 !== this.members.findIndex((m) => JSON.stringify(m.ValueOriginal) !== JSON.stringify(m.Value))
  }

  get title() {
    if (this.selection === null || this.selection.Name === undefined) { return '' }
    return this.getShortTypeName(this.selection.Type) + ': ' + this.selection.Name
  }

  get browseValues() {
    if (this.browseDialog.memberIdx < this.members.length) {
      return this.members[this.browseDialog.memberIdx].BrowseValues
    }
    return []
  }

  get filteredBrowseValues() {
    const items = this.browseValues
    const s = this.search.toString().toLowerCase()
    if (s.trim() === '') { return items }
    const words = s.split(' ').filter((w) => w !== '')
    const isFilterMatch = (val) => {
      const valLower = val.toLowerCase()
      return words.every((word) => valLower.indexOf(word) !== -1)
    }
    return items.filter((item) => isFilterMatch(item))
  }

  @Watch('categoryMembers')
  watch_categoryMembers(v, oldV) {
    const tab = this.currentTab
    if (tab === 'Variables') { return }
    const categories = this.categoryMembers
    if (categories.findIndex((cat) => cat.Category === tab) >= 0) { return }
    this.currentTab = (categories.length === 0 ? '' : categories[0].Category)
  }

  classObject(tab) {
    const sel = this.currentTab === tab
    return {
      selectedtab: sel,
      nonselectedtab: !sel,
    }
  }

  loadBrowseable(memberName, memberValue) {
    const idx = this.members.findIndex((m) => m.Name === memberName)
    this.$emit('browse', memberName, idx)
    this.browseDialog.memberName = memberName
    this.browseDialog.memberIdx = idx
    this.browseDialog.selection = ''
    this.search = ''
    this.browseDialog.show = true
    const txt = this.$refs.txtSearch as HTMLElement
    const doFocus = () => {
      txt.focus()
    }
    setTimeout(doFocus, 100)
  }

  browseOK() {
    this.members[this.browseDialog.memberIdx].Value = this.browseDialog.selection
    this.browseDialog.show = false
  }

  browseKeydown(e) {
    if (e.keyCode === 27) {
      this.browseDialog.show = false
    }
    else if (e.keyCode === 13) {
      this.browseOK()
    }
  }

  editVar(item) {
    this.editVarName = item.Name
    this.editVarTmp = item.V
    this.editVarDialog = true
    const context = this
    const doFocus = () => {
      const txt = context.$refs.editTextInput as HTMLElement
      txt.focus()
    }
    setTimeout(doFocus, 100)
  }

  editVarWrite() {
    this.editVarDialog = false
    this.writeVariable(this.selection.ID, this.editVarName, this.editVarTmp)
  }

  editVarKeydown(e) {
    if (e.keyCode === 27) {
      this.editVarDialog = false
    }
    else if (e.keyCode === 13) {
      this.editVarWrite()
    }
  }

  validateVariableValue(v) {
    if (v === '') { return 'No empty value allowed.' }
    try {
      JSON.parse(v)
      return true
    }
    catch (error) {
      return 'Not valid JSON'
    }
  }

  writeVariable(objID, varName, newVal) {
    try {
      JSON.parse(newVal)
      const params = { ObjID: objID, Var: varName, V: newVal }
      window.parent['dashboardApp'].sendViewRequest('WriteVariable', params, (strResponse) => { })
    }
    catch (error) {
      console.log('Bad JSON: ' + error)
    }
  }

  getShortTypeName(fullTypeName) {
    if (fullTypeName === undefined) { return '' }
    const i = fullTypeName.lastIndexOf('.')
    if (i > 0) {
      return fullTypeName.substring(i + 1)
    }
    return fullTypeName
  }

  async deleteObject() {
    const confirm = this.$refs.confirm as any
    const obj: TreeNode = this.selection
    const name = this.getShortTypeName(obj.Type) + ' ' + obj.Name
    if (await confirm.open('Confirm Delete', 'Do you want to delete ' + name + '?', { color: 'red' })) {
      this.$emit('delete', obj)
    }
  }

  qualityColor(q) {
    if (q === 'Good')      { return 'green' }
    if (q === 'Uncertain') { return 'orange' }
    return 'red'
  }

  save() {
    const msChanged = this.members.filter((m) => JSON.stringify(m.ValueOriginal) !== JSON.stringify(m.Value))
    const mem: SaveMember[] = msChanged.map((m) => {
      console.log('Orig: ' + JSON.stringify(m.ValueOriginal))
      console.log('Upda: ' + JSON.stringify(m.Value))
      return {
        Name: m.Name,
        Value: JSON.stringify(m.Value),
      }
    })
    this.$emit('save', this.selection.ID, this.selection.Type, mem)
  }

  showAddChildDialog(t) {
    if (t.Members.length > 1) {
      alert('Multiple members for type ' + t.TypeName)
      return
    }
    const context = this
    window.parent['dashboardApp'].sendViewRequest('GetNewID', JSON.stringify(t.TypeName), (strResponse) => {
      const theID = JSON.parse(strResponse)
      context.addDialog.newID = theID
    })
    this.addDialog.memberName = t.Members[0]
    this.addDialog.typeNameShort = this.getShortTypeName(t.TypeName)
    this.addDialog.typeNameFull = t.TypeName
    this.addDialog.newID = ''
    this.addDialog.newName = ''
    this.addDialog.show = true
    const txtName = this.$refs.txtName as HTMLElement
    const doFocus = () => {
      txtName.focus()
    }
    setTimeout(doFocus, 100)
  }

  editKeydown(e) {
    if (e.keyCode === 27) {
      this.addDialog.show = false
    }
  }

  onAddNewObject() {
    this.addDialog.show = false
    const info: AddObjectParams = {
      ParentObjID:  this.selection.ID,
      ParentMember: this.addDialog.memberName,
      NewObjID:     this.addDialog.newID,
      NewObjType:   this.addDialog.typeNameFull,
      NewObjName:   this.addDialog.newName,
    }
    this.$emit('add', info)
  }

  removeOptional(member) {
    member.Value = null
  }

  setOptional(member) {
    member.Value = JSON.parse(member.DefaultValue)
  }

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

  addArrayItem(member) {
    member.Value.push(JSON.parse(member.DefaultValue))
  }

  moveUp() {
    this.$emit('move', this.selection.ID, true)
  }

  moveDown() {
    this.$emit('move', this.selection.ID, false)
  }
}

</script>


<style>

  .v-data-table-header th {
    font-size: 16px;
    font-weight: bold;
  }

  .v-data-table tbody td {
    font-size: 16px;
    height: auto;
    padding-top: 9px !important;
    padding-bottom: 9px !important;
  }

</style>