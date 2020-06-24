<template>
  <div>

    <v-tabs v-model="selectedTab" style="margin-top: 8px;">
      <v-tab href="#Properties">Properties</v-tab>
      <v-tab href="#Code" v-if="hasCode">{{definitionName}}</v-tab>
      <v-tab href="#Inputs">Inputs</v-tab>
      <v-tab href="#Outputs">Outputs</v-tab>
      <v-tab href="#States" v-if="hasStates">States</v-tab>
    </v-tabs>

    <div v-if="selectedTab === 'Properties'">

      <table cellspacing="10">
        <member-row name="Name"            v-model="value.Name"          type="String"   :optional="false"></member-row>
        <member-row name="Type"            v-model="value.Type"          type="Enum"     :optional="false" :enumValues="adapterTypes"></member-row>
        <member-row name="History"         v-model="value.History"       type="History"  :optional="true"></member-row>
        <member-row name="Cycle"           v-model="value.Cycle"         type="Duration" :optional="false"></member-row>
        <member-row name="Enabled"         v-model="value.Enabled"       type="Boolean"  :optional="false"></member-row>
        <member-row name="WindowVisible"   v-model="value.WindowVisible" type="Boolean"  :optional="false" v-if="showWindowVisible"></member-row>
        <member-row :name="definitionName" v-model="value.Definition"    :type="definitionType" :optional="false" v-if="showDefinition && definitionType !== 'Code'"></member-row>
      </table>

    </div>

    <div v-if="selectedTab === 'Code'">

      <editor style="font-size: 18px!important;margin-top:8px;" v-if="hasCode" v-model="value.Definition"
        @init="editorInit" lang="csharp" theme="TextMate" :options="codeEditorOptions" width="100%" height="680"></editor>

    </div>

    <div v-if="selectedTab === 'Inputs'">

      <table cellspacing="16">

        <thead>
          <tr>
            <th align="left">Name</th>
            <th align="left">Unit</th>
            <th align="left">Type</th>
            <th align="left">Value</th>
            <th align="left">Value Time</th>
            <th align="left">Value Source</th>
            <th align="left">Object</th>
            <th align="left">&nbsp;</th>
            <th align="left">Variable</th>
            <th align="left"><div class="ml-4">Constant Value</div></th>

          </tr>
        </thead>

        <tr v-for="{ input, VarVal } in inputList" :key="input.ID">
          <td class="IOName">{{ input.Name }}</td>
          <td>{{ input.Unit }}</td>
          <td>{{ input.Type }}</td>

          <td v-bind:style="{ color: qualityColor(VarVal.Q) }">{{ VarVal.V }}</td>
          <td>{{ VarVal.T }}</td>

          <td><v-select :value="inputAssignState(input)" @input="onValueTypeChanged(input, $event)" :items="assignStateValuesIn" hide-details class="tabcontent" style="width: 125px;"></v-select></td>

          <td>             {{ isVar(input) ? getObjectName(input.Variable.Object) : ''  }}</td>
          <td><v-btn    v-if="isVar(input)" class="ml-2 mr-4" style="min-width:36px;width:36px;" @click="onSelectObj(input.Variable)"><v-icon>edit</v-icon></v-btn></td>
          <td><v-select v-if="isVar(input)" :items="getObjectVariables(input.Variable.Object)" v-model="input.Variable.Name" hide-details class="tabcontent" style="width:18ch;"></v-select></td>

          <td>
            <v-text-field class="ml-4" v-if="isConst(input)" v-model="input.Constant" hide-details  style="width: 100px;"></v-text-field>
          </td>
        </tr>

      </table>

    </div>

    <div v-if="selectedTab === 'Outputs'">

      <table cellspacing="16">

        <thead>
          <tr>
            <th align="left">Name</th>
            <th align="left">Unit</th>
            <th align="left">Type</th>
            <th align="left">Value</th>
            <th align="left">Value Time</th>
            <th align="left">Value Destination</th>
            <th align="left">Object</th>
            <th align="left">&nbsp;</th>
            <th align="left">Variable</th>

          </tr>
        </thead>

        <tr v-for="{ output, VarVal } in outputList" :key="output.ID">
          <td class="IOName">{{ output.Name }}</td>
          <td>{{ output.Unit }}</td>
          <td>{{ output.Type }}</td>
          <td v-bind:style="{ color: qualityColor(VarVal.Q) }">{{ VarVal.V }}</td>
          <td>{{ VarVal.T }}</td>

          <td><v-select :value="outputAssignState(output)" @input="onOutputValueTypeChanged(output, $event)" :items="assignStateValuesOut" hide-details class="tabcontent" style="width: 130px;"></v-select></td>

          <td>             {{ output.Variable !== null ? getObjectName(output.Variable.Object) : ''  }}</td>
          <td><v-btn v-if="outputAssignState(output) === 'Variable'" class="ml-2 mr-4" style="min-width:36px;width:36px;" @click="onSelectOutputObj(output)"><v-icon>edit</v-icon></v-btn></td>
          <td><v-select v-if="output.Variable !== null" :items="getObjectVariables(output.Variable.Object)" v-model="output.Variable.Name" hide-details class="tabcontent" style="width:18ch;"></v-select></td>
        </tr>

      </table>

    </div>

    <div v-if="selectedTab === 'States'">

      <table cellspacing="16">

        <thead>
          <tr>
            <th align="left">Name</th>
            <th align="left">Unit</th>
            <th align="left">Type</th>
            <th align="left">Value</th>
            <th align="left">Value Time</th>
          </tr>
        </thead>

        <tr v-for="{ state, VarVal } in stateList" :key="state.ID">
          <td class="IOName">{{ state.Name }}</td>
          <td>{{ state.Unit }}</td>
          <td>{{ state.Type }}</td>
          <td v-bind:style="{ color: qualityColor(VarVal.Q) }">{{ VarVal.V }}</td>
          <td>{{ VarVal.T }}</td>
        </tr>

      </table>

    </div>

    <dlg-object-select v-model="selectObject.show"
      :object-id="selectObject.selectedObjectID"
      :module-id="selectObject.selectedModuleID"
      :modules="selectObject.modules"
      @onselected="selectObject_OK"></dlg-object-select>

  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue } from 'vue-property-decorator'
import * as calcmodel from './model'
import MemberRow from './util/MemberRow.vue'
import * as global from './global'
import * as fast from '../fast_types'
import DlgObjectSelect from '../components/DlgObjectSelect.vue'
import { CalculationVariables, IoVar } from './conversion'
import { MemberTypeEnum } from './util/member_types'

type AssignState = 'Unassigned' | 'Constant' | 'Variable'

interface SelectObject {
  show: boolean
  modules: global.ModuleInfo[]
  selectedModuleID: string
  selectedObjectID: string
  variable: fast.VariableRef
}

interface Obj {
  Type: string
  ID: string
  Name: string
  Variables: string[]
}

@Component({
  components: {
    MemberRow,
    DlgObjectSelect,
    editor: require('vue2-ace-editor'),
  },
})
export default class CalculationEditor extends Vue {

  @Prop(Object) value: calcmodel.Calculation
  @Prop(Object) variables: CalculationVariables
  @Prop(Array) adapterTypesInfo: global.AdapterInfo[]

  assignStateValuesIn: AssignState[]  = [ 'Unassigned', 'Variable', 'Constant' ]
  assignStateValuesOut: AssignState[] = [ 'Unassigned', 'Variable' ]
  selectedTab: string = ''

  selectObject: SelectObject = {
    show: false,
    modules: [],
    selectedModuleID: '',
    selectedObjectID: '',
    variable: { Object: '', Name: '' },
  }

  codeEditorOptions = {
    enableBasicAutocompletion: true,
  }

  get adapterTypes(): string[] {
    return this.adapterTypesInfo.map((info) => info.Type)
  }

  get hasCode(): boolean {
    return this.showDefinition && this.definitionType === 'Code'
  }

  get hasStates(): boolean {
    return this.value.States.length > 0
  }

  get definitionName(): string {
    const type = this.value.Type
    const info: global.AdapterInfo | undefined = this.adapterTypesInfo.find((inf) => inf.Type === type)
    if (info === undefined) { return 'Definition' }
    return info.DefinitionLabel
  }

  get showWindowVisible(): boolean {
    const type = this.value.Type
    const info: global.AdapterInfo | undefined = this.adapterTypesInfo.find((inf) => inf.Type === type)
    if (info === undefined) { return false }
    return info.Show_WindowVisible
  }

  get showDefinition(): boolean {
    const type = this.value.Type
    const info: global.AdapterInfo | undefined = this.adapterTypesInfo.find((inf) => inf.Type === type)
    if (info === undefined) { return false }
    return info.Show_Definition
  }

  get definitionType(): MemberTypeEnum {
    const type = this.value.Type
    const info: global.AdapterInfo | undefined = this.adapterTypesInfo.find((inf) => inf.Type === type)
    if (info === undefined) { return 'String' }
    if (info.DefinitionIsCode) {
      return 'Code'
    }
    return 'String'
  }

  editorInit(): void {
    require('brace/ext/language_tools')
    require('brace/mode/csharp')
    require('brace/theme/textmate')
    require('brace/snippets/csharp')
  }

  get inputList() {
    const map: IoVar[] = this.variables.Inputs
    return this.value.Inputs.map((input) => {
      const vtq: fast.VTQ = map.find((it) => it.Key === input.ID)?.Value || { V: '', Q: 'Bad', T: ''}
      return { input, VarVal: vtq }
    })
  }

  get outputList() {
    const map: IoVar[] = this.variables.Outputs
    return this.value.Outputs.map((output) => {
      const vtq: fast.VTQ = map.find((it) => it.Key === output.ID)?.Value || { V: '', Q: 'Bad', T: ''}
      return { output, VarVal: vtq }
    })
  }

  get stateList() {
    const map: IoVar[] = this.variables.States
    return this.value.States.map((state) => {
      const vtq: fast.VTQ = map.find((it) => it.Key === state.ID)?.Value || { V: '', Q: 'Bad', T: ''}
      return { state, VarVal: vtq }
    })
  }

  qualityColor(q: fast.Quality) {
    if (q === 'Good') { return 'green' }
    if (q === 'Uncertain') { return 'orange' }
    return 'red'
  }

  isVar(input: calcmodel.Input): boolean {
    return this.inputAssignState(input) === 'Variable'
  }

  isConst(input: calcmodel.Input): boolean {
    return this.inputAssignState(input) === 'Constant'
  }

  inputAssignState(input: calcmodel.Input): AssignState {
    if (input.Constant !== null) {
      return 'Constant'
    }
    else if (input.Variable !== null) {
      return 'Variable'
    }
    else {
      return 'Unassigned'
    }
  }

  outputAssignState(output: calcmodel.Output): AssignState {
    if (output.Variable !== null) {
      return 'Variable'
    }
    else {
      return 'Unassigned'
    }
  }

  onValueTypeChanged(input: calcmodel.Input, newValue: AssignState): void {
    console.info('newValue: ' + newValue)
    switch (newValue) {
    case 'Unassigned':
      input.Constant = null
      input.Variable = null
      break
    case 'Constant':
      input.Constant = '0.0'
      break
    case 'Variable':
      input.Constant = null
      input.Variable = { Object: '', Name: '' }
      break
    }
  }

  onOutputValueTypeChanged(output: calcmodel.Output, newValue: AssignState): void {
    console.info('newValue: ' + newValue)
    switch (newValue) {
    case 'Unassigned':
      output.Variable = null
      break
    case 'Variable':
      output.Variable = { Object: '', Name: '' }
      break
    }
  }

  getObjectName(id: string): string {
    const obj = global.mapObjects.get(id)
    return obj === undefined ? '???' : obj.Name
  }

  getObjectVariables(id: string): string[] {
    const obj = global.mapObjects.get(id)
    return obj === undefined ? [] : obj.Variables
  }

  onSelectOutputObj(out: calcmodel.Output): void {
    if (out.Variable === null) {
      out.Variable = { Object: '', Name: '' }
    }
    this.onSelectObj(out.Variable)
  }

  onSelectObj(v: fast.VariableRef | null): void {

    const currObj: string = v.Object
    let objForModuleID: string = currObj
    if (objForModuleID === '') {
      const nonEmptyItems = this.value.Inputs.filter((it) => it.Variable !== null)
      if (nonEmptyItems.length > 0) {
        objForModuleID = nonEmptyItems[0].Variable.Object
      }
    }

    const i = objForModuleID.indexOf(':')
    if (i <= 0) {
      this.selectObject.selectedModuleID = global.modules[0].ID
    }
    else {
      this.selectObject.selectedModuleID = objForModuleID.substring(0, i)
    }
    this.selectObject.selectedObjectID = currObj
    this.selectObject.variable = v
    this.selectObject.modules = global.modules
    this.selectObject.show = true
  }

  selectObject_OK(obj: Obj) {

    global.mapObjects.set(obj.ID, {
      ID: obj.ID,
      Name: obj.Name,
      Variables: obj.Variables,
    })

    this.selectObject.variable.Object = obj.ID
    if (obj.Variables.length === 1) {
      this.selectObject.variable.Name = obj.Variables[0]
    }
  }
}

</script>


<style>

  .IOName {
    font-weight: bold;
  }

  .tabcontent {
    padding-top: 0px !important;
    margin-top: 0px !important;
  }

</style>