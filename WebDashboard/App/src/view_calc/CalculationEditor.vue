<template>
  <div>
    <v-tabs
      v-model="selectedTab"
      style="margin-top: 8px"
      density="compact"
    >
      <v-tab value="Properties">Properties</v-tab>
      <v-tab
        v-if="hasCode"
        value="Code"
        >{{ definitionName }}</v-tab
      >
      <v-tab value="Inputs">Inputs</v-tab>
      <v-tab value="Outputs">Outputs</v-tab>
      <v-tab
        v-if="hasStates"
        value="States"
        >States</v-tab
      >
    </v-tabs>

    <div v-if="selectedTab === 'Properties'">
      <table cellspacing="10">
        <member-row
          v-model="model.Name"
          name="Name"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.Type"
          :enum-values="adapterTypes"
          name="Type"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-if="showSubtypes"
          v-model="model.Subtype"
          :enum-values="adapterSubtypes"
          name="Subtype"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-model="model.RunMode"
          :enum-values="runModes"
          name="RunMode"
          :tooltip-html="runModeTooltip"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-model="model.InputsRequired"
          :enum-values="inputsRequiredValues"
          name="Inputs Required"
          :tooltip-html="inputsRequiredTooltip"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-if="showInitialStartTime"
          v-model="model.InitialStartTime"
          name="Initial Start Time"
          :tooltip-html="initialStartTimeTooltip"
          :optional="false"
          type="Timestamp"
        />
        <member-row
          v-model="model.MaxInputAge"
          name="Max Input Age"
          :tooltip-html="maxInputAgeTooltip"
          :optional="true"
          type="Duration"
        />
        <member-row
          v-model="model.InitErrorResponse"
          :enum-values="initErrorResponses"
          name="Init Error Response"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-model="model.History"
          name="History"
          :optional="true"
          type="History"
        />
        <member-row
          v-model="model.HistoryScope"
          :enum-values="historyScopes"
          name="History Scope"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-model="model.Cycle"
          name="Cycle"
          :optional="false"
          type="Duration"
        />
        <member-row
          v-if="showCycleOffset"
          v-model="model.Offset"
          name="Cycle Offset"
          :optional="false"
          type="Duration"
        />
        <member-row
          v-if="isContinuous && model.Offset !== '0 s'"
          v-model="model.IgnoreOffsetForTimestamps"
          name="Ignore Offset For Timestamps"
          :tooltip-html="ignoreOffsetForTimestampsTooltip"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.Enabled"
          name="Enabled"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.EnableOutputVarWrite"
          name="Enable Output Var Write"
          :tooltip-html="enableOutputVarWriteTooltip"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-if="showWindowVisible"
          v-model="model.WindowVisible"
          name="WindowVisible"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-if="showDefinition && definitionType !== 'Code'"
          v-model="model.Definition"
          :name="definitionName"
          :optional="false"
          :type="definitionType"
        />
      </table>
    </div>

    <div v-if="selectedTab === 'Code'">
      <code-editor
        v-if="hasCode"
        v-model="model.Definition"
        height="calc(100vh - 250px)"
        :lang="codeLang"
        width="100%"
      />
    </div>

    <div v-if="selectedTab === 'Inputs'">
      <table cellspacing="10">
        <thead>
          <tr>
            <th class="align-left">Name</th>
            <th class="align-left">Unit</th>
            <th
              v-if="showInputTypeColumn"
              class="align-left"
            >
              Type
            </th>
            <th class="align-left">Value</th>
            <th class="align-left">Time</th>
            <th class="align-left">Value Source</th>
            <th class="align-left">Object / Constant</th>
            <th class="align-left">&nbsp;</th>
            <th class="align-left">Variable</th>
          </tr>
        </thead>

        <tbody>
          <tr
            v-for="{ input, VarVal } in inputList"
            :key="input.ID"
            style="vertical-align: top"
          >
            <td
              class="IOName"
              style="padding-top: 4px"
            >
              {{ input.Name }}
            </td>
            <td style="padding-top: 4px">{{ input.Unit }}</td>
            <td
              v-if="showInputTypeColumn"
              style="padding-top: 4px"
            >
              {{ input.Type }}
            </td>
            <td style="padding-top: 4px">
              <value-display
                :dimension="input.Dimension"
                :type="input.Type"
                :vtq="VarVal"
              />
            </td>
            <td style="padding-top: 4px">{{ VarVal.T }}</td>
            <td>
              <v-select
                class="tabcontent"
                :items="assignStateValuesIn"
                :model-value="inputAssignState(input)"
                style="width: 150px"
                @update:model-value="onValueTypeChanged(input, $event)"
              />
            </td>
            <td
              v-if="isVar(input)"
              style="padding-top: 4px; max-width: 32ch; overflow-wrap: anywhere"
            >
              {{ isVar(input) ? getObjectName(input.Variable!.Object) : '' }}
            </td>
            <td v-if="isVar(input)">
              <v-btn
                class="ml-0 mr-3"
                style="min-width: 36px; width: 36px"
                @click="onSelectObj(input.Variable!, input.Type)"
              >
                <v-icon>mdi-pencil</v-icon>
              </v-btn>
            </td>
            <td v-if="isVar(input)">
              <v-select
                v-model="input.Variable!.Name"
                class="tabcontent"
                :items="getObjectVariables(input.Variable!.Object)"
                style="width: 16ch"
              />
            </td>
            <td
              v-if="isConst(input)"
              colspan="3"
            >
              <v-text-field
                v-model="input.Constant"
                class="ml-0 tabcontent"
                style="width: 120px"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="selectedTab === 'Outputs'">
      <table cellspacing="10">
        <thead>
          <tr>
            <th class="align-left">Name</th>
            <th class="align-left">Unit</th>
            <th
              v-if="showOutputTypeColumn"
              class="align-left"
            >
              Type
            </th>
            <th class="align-left">Value</th>
            <th class="align-left">Time</th>
            <th class="align-left">Value Destination</th>
            <th class="align-left">Object</th>
            <th class="align-left">&nbsp;</th>
            <th class="align-left">Variable</th>
          </tr>
        </thead>

        <tbody>
          <tr
            v-for="{ output, VarVal } in outputList"
            :key="output.ID"
            style="vertical-align: top"
          >
            <td
              class="IOName"
              style="padding-top: 4px"
            >
              {{ output.Name }}
            </td>
            <td style="padding-top: 4px">{{ output.Unit }}</td>
            <td
              v-if="showOutputTypeColumn"
              style="padding-top: 4px"
            >
              {{ output.Type }}
            </td>
            <td style="padding-top: 4px">
              <value-display
                :dimension="output.Dimension"
                :type="output.Type"
                :vtq="VarVal"
              />
            </td>
            <td style="padding-top: 4px">{{ VarVal.T }}</td>
            <td>
              <v-select
                class="tabcontent"
                :items="assignStateValuesOut"
                :model-value="outputAssignState(output)"
                style="width: 130px"
                @update:model-value="onOutputValueTypeChanged(output, $event)"
              />
            </td>
            <td style="padding-top: 4px; max-width: 32ch; overflow-wrap: anywhere">
              {{ output.Variable !== null ? getObjectName(output.Variable.Object) : '' }}
            </td>
            <td>
              <v-btn
                v-if="outputAssignState(output) === 'Variable'"
                class="ml-0 mr-3"
                style="min-width: 36px; width: 36px"
                @click="onSelectObj(output.Variable!, output.Type)"
              >
                <v-icon>mdi-pencil</v-icon>
              </v-btn>
            </td>
            <td>
              <v-select
                v-if="output.Variable !== null"
                v-model="output.Variable.Name"
                class="tabcontent"
                :items="getObjectVariables(output.Variable.Object)"
                style="width: 18ch"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="selectedTab === 'States'">
      <table cellspacing="10">
        <thead>
          <tr>
            <th class="align-left">Name</th>
            <th class="align-left">Unit</th>
            <th class="align-left">Type</th>
            <th class="align-left">Value</th>
            <th class="align-left">Value Time</th>
          </tr>
        </thead>

        <tbody>
          <tr
            v-for="{ state, VarVal } in stateList"
            :key="state.ID"
            style="vertical-align: top"
          >
            <td
              class="IOName"
              style="padding-top: 4px"
            >
              {{ state.Name }}
            </td>
            <td style="padding-top: 4px">{{ state.Unit }}</td>
            <td style="padding-top: 4px">{{ state.Type }}</td>
            <td style="padding-top: 4px">
              <value-display
                :dimension="state.Dimension"
                :type="state.Type"
                :vtq="VarVal"
              />
            </td>
            <td style="padding-top: 4px">{{ VarVal.T }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <dlg-object-select
      v-model="selectObject.show"
      :module-id="selectObject.selectedModuleID"
      :modules="selectObject.modules"
      :object-id="selectObject.selectedObjectID"
      :type="selectObject.type"
      @onselected="selectObject_OK"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import * as calcmodel from './model'
import MemberRow from './util/MemberRow.vue'
import ValueDisplay from './util/ValueDisplay.vue'
import * as global from './global'
import * as fast from '../fast_types'
import type { CalculationVariables, IoVar } from './conversion'
import type { MemberTypeEnum } from './util/member_types'

type AssignState = 'Unassigned' | 'Constant' | 'Variable'

interface SelectObject {
  show: boolean
  modules: global.ModuleInfo[]
  selectedModuleID: string
  selectedObjectID: string
  type: fast.DataType
  variable: fast.VariableRef
}

interface Obj {
  ID: string
  Name: string
  Variables: string[]
}

const model = defineModel<calcmodel.Calculation>({ required: true })

const props = defineProps<{
  variables: CalculationVariables
  adapterTypesInfo: global.AdapterInfo[]
}>()

const selectedTab = ref('')
const selectObject = ref<SelectObject>({
  show: false,
  modules: [],
  selectedModuleID: '',
  selectedObjectID: '',
  type: 'Float64',
  variable: { Object: '', Name: '' },
})

const assignStateValuesIn: AssignState[] = ['Unassigned', 'Variable', 'Constant']
const assignStateValuesOut: AssignState[] = ['Unassigned', 'Variable']

const sortIOs = computed((): boolean => model.value.Type === 'Simba')

const showInputTypeColumn = computed((): boolean => {
  const inputs = model.value.Inputs
  if (inputs.length === 0) return false
  const type = inputs[0].Type
  return inputs.some((it) => it.Type !== type)
})

const showOutputTypeColumn = computed((): boolean => {
  const outputs = model.value.Outputs
  if (outputs.length === 0) return false
  const type = outputs[0].Type
  return outputs.some((it) => it.Type !== type)
})

const adapterTypes = computed((): string[] => props.adapterTypesInfo.map((info) => info.Type))

const runModes = computed((): string[] => ['Continuous', 'Triggered', 'InputDriven'])
const inputsRequiredValues = computed((): string[] => ['All', 'AtLeastOne', 'None'])

const runModeTooltip = `
<strong>Continuous</strong>:  Runs on the configured cycle (real-time).<br>
<strong>Triggered</strong>:   Runs when explicitly triggered by another calculation.<br>
<strong>InputDriven</strong>: Processes data from historian on Cycle/Offset-aligned timestamps. For each step, every variable input uses the value 
                              at that timestamp or the latest value before it. If no data exists at/after the step timestamp yet, the calculation waits
                              and continues later once new data has arrived. The cursor resumes from Last Run Timestamp + Cycle when available; 
                              otherwise Initial Start Time is used. Best for backfilling historic data and batch input streams.`

const inputsRequiredTooltip = `
<strong>All</strong>:        Run only when all inputs have valid values (value is not empty and not NaN, quality is not Bad).<br>
<strong>AtLeastOne</strong>: Run when at least one input has a valid value.<br>
<strong>None</strong>:       Always run, no matter what the input values are.`

const initialStartTimeTooltip = `
Used only in InputDriven mode.<br>
Defines the initial timestamp from which historical input processing starts.<br>
When switching to InputDriven, this value is set automatically and can be adjusted.<br>
If it is not aligned to Cycle/Offset, the next aligned timestamp is used.`

const maxInputAgeTooltip = `
Optional staleness limit for input values.
If an input is older than this duration, it is treated as missing/invalid for the run (quality is set to Bad, value is set to null/empty).
Can be combined with Inputs Required to skip calculation runs when inputs are too old.`

const ignoreOffsetForTimestampsTooltip = `
Controls the timestamp passed to the calculation in Continuous mode when Offset is non-zero.<br>
Disabled: step timestamp equals the actual offset-aligned run time.<br>
Enabled:  step timestamp uses cycle time without offset (timestamp = runTime - Offset).<br>
Note: this changes timestamps (and dt), not the schedule itself.`
const enableOutputVarWriteTooltip = `
Controls whether calculation outputs are written to the destination variables configured in Outputs.<br>
Enabled: mapped output variables are written after each run.<br>
Disabled: no external output-variable write is performed (useful for test/dry-run).<br>
Note: internal output/state values and history in the Calc object are still updated.`

const initErrorResponses = computed((): string[] => ['Fail', 'Retry', 'Stop'])

const historyScopes = computed((): string[] => ['All', 'ExcludeInputs', 'ExcludeStates', 'ExcludeInputsAndStates'])

const adapterSubtypes = computed((): string[] => {
  const type = model.value.Type
  const info = props.adapterTypesInfo.find((inf) => inf.Type === type)
  return info?.Subtypes || []
})

const nowTruncatedToHourISO = (): string => {
  const current = new Date()
  current.setMinutes(0, 0, 0)
  const iso = current.toISOString()
  return iso.slice(0, 16) + 'Z'
}

watch(
  () => model.value.Type,
  () => {
    const subtypes = adapterSubtypes.value
    if (subtypes.length === 0) {
      model.value.Subtype = ''
    } else if (model.value.Subtype === '') {
      model.value.Subtype = subtypes[0]
    }
  },
)

watch(
  () => model.value.RunMode,
  (newMode, oldMode) => {
    if (newMode === 'InputDriven' && oldMode !== 'InputDriven') {
      model.value.InitialStartTime = nowTruncatedToHourISO()
    }
  },
)

const showInitialStartTime = computed((): boolean => model.value.RunMode === 'InputDriven')

const showSubtypes = computed((): boolean => adapterSubtypes.value.length > 0)

const hasCode = computed((): boolean => showDefinition.value && definitionType.value === 'Code')

const hasStates = computed((): boolean => model.value.States.length > 0)

const definitionName = computed((): string => {
  const type = model.value.Type
  const info = props.adapterTypesInfo.find((inf) => inf.Type === type)
  return info?.DefinitionLabel || 'Definition'
})

const showWindowVisible = computed((): boolean => {
  const type = model.value.Type
  const info = props.adapterTypesInfo.find((inf) => inf.Type === type)
  return info?.Show_WindowVisible || false
})

const showDefinition = computed((): boolean => {
  const type = model.value.Type
  const info = props.adapterTypesInfo.find((inf) => inf.Type === type)
  return info?.Show_Definition || false
})

const isContinuous = computed((): boolean => {
  const mode = model.value.RunMode
  return mode === undefined || mode === 'Continuous'
})

const showCycleOffset = computed((): boolean => {
  const mode = model.value.RunMode
  return mode === undefined || mode === 'Continuous' || mode === 'InputDriven'
})

const definitionType = computed((): MemberTypeEnum => {
  const type = model.value.Type
  const info = props.adapterTypesInfo.find((inf) => inf.Type === type)
  if (!info) return 'String'
  return info.DefinitionIsCode ? 'Code' : 'String'
})

const codeLang = computed((): string => {
  const type = model.value.Type
  const info = props.adapterTypesInfo.find((inf) => inf.Type === type)
  return info?.CodeLang || 'csharp'
})

const shallowCopyArray = (arr: any[]): any[] => [...arr]

const sortedInputs = computed((): calcmodel.Input[] => {
  if (!sortIOs.value) return model.value.Inputs
  return shallowCopyArray(model.value.Inputs).sort((a, b) => a.Name.localeCompare(b.Name))
})

const sortedOutputs = computed((): calcmodel.Output[] => {
  if (!sortIOs.value) return model.value.Outputs
  return shallowCopyArray(model.value.Outputs).sort((a, b) => a.Name.localeCompare(b.Name))
})

const inputList = computed(() => {
  const map: IoVar[] = props.variables.Inputs
  return sortedInputs.value.map((input) => {
    const vtq: fast.VTQ = map.find((it) => it.Key === input.ID)?.Value || { V: '', Q: 'Bad', T: '' }
    return { input, VarVal: vtq }
  })
})

const outputList = computed(() => {
  const map: IoVar[] = props.variables.Outputs
  return sortedOutputs.value.map((output) => {
    const vtq: fast.VTQ = map.find((it) => it.Key === output.ID)?.Value || { V: '', Q: 'Bad', T: '' }
    return { output, VarVal: vtq }
  })
})

const stateList = computed(() => {
  const map: IoVar[] = props.variables.States
  return model.value.States.map((state) => {
    const vtq: fast.VTQ = map.find((it) => it.Key === state.ID)?.Value || { V: '', Q: 'Bad', T: '' }
    return { state, VarVal: vtq }
  })
})

const qualityColor = (q: fast.Quality): string => {
  if (q === 'Good') return 'green'
  if (q === 'Uncertain') return 'orange'
  return 'red'
}

const isVar = (input: calcmodel.Input): boolean => inputAssignState(input) === 'Variable'

const isConst = (input: calcmodel.Input): boolean => inputAssignState(input) === 'Constant'

const inputAssignState = (input: calcmodel.Input): AssignState => {
  if (input.Constant !== null) return 'Constant'
  if (input.Variable !== null) return 'Variable'
  return 'Unassigned'
}

const outputAssignState = (output: calcmodel.Output): AssignState => {
  if (output.Variable !== null) return 'Variable'
  return 'Unassigned'
}

const onValueTypeChanged = (input: calcmodel.Input, newValue: AssignState): void => {
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
      if (!input.Variable) {
        input.Variable = { Object: '', Name: '' }
      }
      break
  }
}

const onOutputValueTypeChanged = (output: calcmodel.Output, newValue: AssignState): void => {
  switch (newValue) {
    case 'Unassigned':
      output.Variable = null
      break
    case 'Variable':
      output.Variable = { Object: '', Name: '' }
      break
  }
}

const getObjectName = (id: string): string => {
  const obj = global.mapObjects.get(id)
  return obj?.Name || '???'
}

const getObjectVariables = (id: string): string[] => {
  const obj = global.mapObjects.get(id)
  return obj?.Variables || []
}

const onSelectObj = (v: fast.VariableRef, type: fast.DataType): void => {
  const currObj: string = v.Object
  let objForModuleID: string = currObj
  if (objForModuleID === '') {
    const nonEmptyItems = model.value.Inputs.filter((it) => it.Variable !== null)
    if (nonEmptyItems.length > 0) {
      objForModuleID = nonEmptyItems[0].Variable!.Object
    }
  }

  const i = objForModuleID.indexOf(':')
  selectObject.value.selectedModuleID = i <= 0 ? global.modules[0].ID : objForModuleID.substring(0, i)
  selectObject.value.selectedObjectID = currObj
  selectObject.value.variable = v
  selectObject.value.modules = global.modules
  selectObject.value.type = type
  selectObject.value.show = true
}

const selectObject_OK = (obj: Obj): void => {
  global.mapObjects.set(obj.ID, {
    ID: obj.ID,
    Name: obj.Name,
    Variables: obj.Variables,
  })

  selectObject.value.variable.Object = obj.ID
  if (obj.Variables.length === 1) {
    selectObject.value.variable.Name = obj.Variables[0]
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

.align-left {
  text-align: left;
}
</style>
