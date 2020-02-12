<template>

  <v-tabs v-model="selectedTab" style="margin-top: 8px;">

    <v-tab>Properties</v-tab>

    <v-tab-item>

      <table cellspacing="10">
        <member-row name="Name"          v-model="value.Name"          type="String"   :optional="false"></member-row>
        <member-row name="Type"          v-model="value.Type"          type="String"   :optional="false"></member-row>
        <member-row name="History"       v-model="value.History"       type="History"  :optional="true"></member-row>
        <member-row name="Definition"    v-model="value.Definition"    type="String"   :optional="false"></member-row>
        <member-row name="Cycle"         v-model="value.Cycle"         type="Duration" :optional="false"></member-row>
        <member-row name="WindowVisible" v-model="value.WindowVisible" type="Boolean"  :optional="false"></member-row>

      </table>

    </v-tab-item>


    <v-tab>Inputs</v-tab>

    <v-tab-item>

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

    </v-tab-item>


    <v-tab>Outputs</v-tab>

    <v-tab-item>

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


    </v-tab-item>

    <dlg-object-select v-model="selectObject.show"
            :object-id="selectObject.selectedObjectID"
            :module-id="selectObject.selectedModuleID"
            :modules="selectObject.modules"
            @onselected="selectObject_OK"></dlg-object-select>

  </v-tabs>

</template>

<script lang="ts">

import { Component, Prop, Vue } from 'vue-property-decorator'
import * as calcmodel from './model'
import MemberRow from './util/MemberRow.vue'
import * as global from './global'
import * as fast from '../fast_types'
import DlgObjectSelect from '../components/DlgObjectSelect.vue'
import { CalculationVariables, IoVar } from './conversion'

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
  },
})
export default class CalculationEditor extends Vue {

  @Prop(Object) value: calcmodel.Calculation
  @Prop(Object) variables: CalculationVariables

  assignStateValuesIn: AssignState[]  = [ 'Unassigned', 'Variable', 'Constant' ]
  assignStateValuesOut: AssignState[] = [ 'Unassigned', 'Variable' ]
  selectedTab: number = 0

  selectObject: SelectObject = {
    show: false,
    modules: [],
    selectedModuleID: '',
    selectedObjectID: '',
    variable: { Object: '', Name: '' },
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
      const vtq: fast.VTQ = map.find((it) => it.Key === output.ID)?.Value || { V: 'Nix', Q: 'Bad', T: ''}
      return { output, VarVal: vtq }
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