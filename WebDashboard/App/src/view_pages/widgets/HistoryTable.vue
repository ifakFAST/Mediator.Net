<template>
  <div
    class="history-table"
    @contextmenu="onContextMenu"
  >
    <div class="history-table-toolbar">
      <v-btn-group
        density="compact"
        variant="outlined"
      >
        <v-btn
          :disabled="!hasConfiguredVariable || historyLoading"
          title="Latest values (descending)"
          @click="latestDesc"
        >
          <v-icon>mdi-arrow-collapse-down</v-icon>
        </v-btn>
        <v-btn
          :disabled="!hasConfiguredVariable || historyLoading"
          title="Oldest values (ascending)"
          @click="oldestAsc"
        >
          <v-icon>mdi-arrow-collapse-up</v-icon>
        </v-btn>
        <v-btn
          :disabled="!hasConfiguredVariable || historyLoading || historyRows.length === 0"
          title="Move down"
          @click="moveDown"
        >
          <v-icon>mdi-chevron-down</v-icon>
        </v-btn>
        <v-btn
          :disabled="!hasConfiguredVariable || historyLoading || historyRows.length === 0"
          title="Move up"
          @click="moveUp"
        >
          <v-icon>mdi-chevron-up</v-icon>
        </v-btn>
      </v-btn-group>

      <v-spacer />

      <div class="history-table-status">
        <template v-if="hasConfiguredVariable">
          <span v-if="historyLoading">Loading...</span>
          <span v-else-if="totalCount !== null">Total: {{ totalCount.toLocaleString() }}</span>
          <v-btn
            v-else
            :loading="countLoading"
            density="compact"
            variant="text"
            @click="requestCount"
          >
            Count
          </v-btn>
        </template>
      </div>
    </div>

    <div
      v-if="!hasConfiguredVariable"
      class="history-table-placeholder"
    >
      No variable configured.
    </div>

    <div
      v-else-if="historyLoading && historyRows.length === 0"
      class="history-table-loading"
    >
      <v-progress-circular
        indeterminate
        size="30"
        width="4"
      />
    </div>

    <div
      v-else-if="historyRows.length === 0"
      class="history-table-placeholder"
    >
      No history data.
    </div>

    <div
      v-else
      class="history-table-scroll"
    >
      <v-table density="compact">
        <thead>
          <tr>
            <th class="text-left text-no-wrap">
              <v-tooltip location="top">
                <template #activator="{ props: tooltipProps }">
                  <span v-bind="tooltipProps">
                    {{ timeColumnTitle }}
                    <span v-if="sortDesc">&#9660;</span>
                    <span v-else>&#9650;</span>
                  </span>
                </template>
                <div>{{ globalState.timeZoneIanaId }}</div>
                <div>{{ globalState.timeZoneDisplayName }}</div>
              </v-tooltip>
            </th>
            <th
              v-if="showQualityColumn"
              class="text-left"
            >
              Quality
            </th>
            <template v-if="showStructColumns">
              <th
                v-for="member in structMembers"
                :key="member"
                class="text-left"
              >
                {{ member }}
              </th>
            </template>
            <th
              v-else
              class="text-left"
            >
              Value
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in displayedRows"
            :key="row.TJ"
          >
            <td class="text-no-wrap">{{ row.T }}</td>
            <td
              v-if="showQualityColumn"
              :style="{ color: qualityColor(row.Q) }"
            >
              {{ row.Q }}
            </td>
            <template v-if="showStructColumns">
              <td
                v-for="member in structMembers"
                :key="member"
                :style="{ color: qualityColor(row.Q) }"
              >
                {{ getStructMemberValue(row.V, member) }}
              </td>
            </template>
            <td
              v-else
              :style="{ color: qualityColor(row.Q) }"
            >
              {{ formatRawValue(row.V) }}
            </td>
          </tr>
        </tbody>
      </v-table>
    </div>

    <v-menu
      v-model="contextMenu.show"
      :target="[contextMenu.clientX, contextMenu.clientY]"
    >
      <v-list>
        <v-list-item @click="onConfigure">
          <v-list-item-title>Configure...</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <v-dialog
      v-model="configDialog.show"
      max-width="760px"
      persistent
    >
      <v-card>
        <v-card-title>Configure History Table</v-card-title>
        <v-card-text>
          <v-container>
            <v-row>
              <v-col cols="5">
                <v-text-field
                  :model-value="objectName(configDialog.config.Variable.Object)"
                  label="Object"
                  readonly
                  hide-details
                ></v-text-field>
              </v-col>
              <v-col
                cols="1"
                class="d-flex align-center"
              >
                <v-btn
                  icon="mdi-pencil"
                  size="small"
                  variant="text"
                  @click="selectVariableObject"
                ></v-btn>
              </v-col>
              <v-col cols="6">
                <v-combobox
                  v-model="configDialog.config.Variable.Name"
                  :items="variablesForObject(configDialog.config.Variable.Object)"
                  label="Variable"
                  hide-details
                  @update:model-value="loadDialogVariableInfo"
                ></v-combobox>
              </v-col>
            </v-row>

            <v-row>
              <v-col cols="4">
                <v-text-field
                  v-model.number="configDialog.config.RowCount"
                  label="Row count"
                  type="number"
                  min="1"
                  max="10000"
                ></v-text-field>
              </v-col>
              <v-col cols="4">
                <v-select
                  v-model="configDialog.config.InitialPosition"
                  label="Initial position"
                  :items="['Latest', 'Oldest']"
                ></v-select>
              </v-col>
              <v-col cols="4">
                <v-text-field
                  v-model.number="configDialog.config.FractionDigits"
                  label="Fraction digits"
                  type="number"
                ></v-text-field>
              </v-col>
            </v-row>

            <v-row>
              <v-col cols="6">
                <v-text-field
                  v-model="configDialog.config.TimeColumnTitle"
                  label="Time column title"
                ></v-text-field>
              </v-col>
              <v-col cols="6">
                <v-text-field
                  v-model="configDialog.config.TimestampFormat"
                  label="Timestamp format"
                ></v-text-field>
              </v-col>
            </v-row>

            <v-row>
              <v-col
                cols="12"
                class="d-flex align-center"
              >
                <v-switch
                  v-model="configDialog.config.ShowQualityColumn"
                  label="Show quality column"
                  hide-details
                ></v-switch>
              </v-col>
            </v-row>

            <v-row>
              <v-col
                v-for="field in qualityColorFields"
                :key="field.key"
                cols="4"
              >
                <div class="history-table-color-field">
                  <span
                    class="history-table-swatch"
                    :style="{ backgroundColor: configDialog.config[field.key] }"
                  ></span>
                  <v-text-field
                    v-model="configDialog.config[field.key]"
                    :label="field.label"
                  ></v-text-field>
                </div>
              </v-col>
            </v-row>

            <v-row v-if="dialogVariableInfo?.Type === 'Struct'">
              <v-col cols="12">
                <v-text-field
                  v-model="configDialog.config.StructColumns"
                  label="Struct columns"
                  hint="Comma-separated list, empty = all members"
                  persistent-hint
                ></v-text-field>
              </v-col>
            </v-row>
          </v-container>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn
            color="grey-darken-1"
            variant="text"
            @click="configDialog.show = false"
          >
            Cancel
          </v-btn>
          <v-btn
            color="primary-darken-1"
            variant="text"
            @click="saveConfig"
          >
            Save
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <dlg-object-select
      v-model="selectObject.show"
      :all-variable-types="true"
      :allow-config-variables="true"
      :module-id="selectObject.selectedModuleID"
      :modules="selectObject.modules"
      :object-id="selectObject.selectedObjectID"
      @onselected="onObjectSelected"
    ></dlg-object-select>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import DlgObjectSelect from '../../components/DlgObjectSelect.vue'
import { globalState } from '../../global'
import type { Quality, VariableRef } from '../../fast_types'
import type { ModuleInfo, Obj, ObjectMap, SelectObject, VariableInfo } from './common'
import * as model from '../model'

interface HistoryTableConfig {
  Variable: VariableRef
  RowCount: number
  ShowQualityColumn: boolean
  ColorQualityGood: string
  ColorQualityUncertain: string
  ColorQualityBad: string
  StructColumns: string
  InitialPosition: 'Latest' | 'Oldest'
  TimeColumnTitle: string
  TimestampFormat: string
  FractionDigits: number
}

interface HistoryRow {
  T: string
  TJ: number
  Q: Quality
  V: string
}

interface CountResponse {
  Count: number
}

const props = defineProps<{
  id: string
  width: string
  height: string
  config: HistoryTableConfig
  configVariables?: model.ConfigVariableValues
  backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  eventName: string
  eventPayload: object
}>()

const historyRows = ref<HistoryRow[]>([])
const historyLoading = ref(false)
let pendingLoad: { mode: 'First' | 'Last'; startJavaTicks: number; endJavaTicks: number; keepIfEmpty: boolean; paging: boolean } | null = null
const countLoading = ref(false)
const totalCount = ref<number | null>(null)
const sortDesc = ref(true)
const currentPosition = ref<'Latest' | 'Oldest' | 'Paged'>('Latest')
const variableInfo = ref<VariableInfo | null>(null)
const dialogVariableInfo = ref<VariableInfo | null>(null)
const canUpdateConfig = ref(false)
const objectMap = ref<ObjectMap>({})
const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})
const configDialog = ref({
  show: false,
  config: createDefaultConfig(),
})
const selectObject = ref<SelectObject>({
  show: false,
  modules: [],
  selectedModuleID: '',
  selectedObjectID: '',
})

const qualityColorFields: { key: 'ColorQualityGood' | 'ColorQualityUncertain' | 'ColorQualityBad'; label: string }[] = [
  { key: 'ColorQualityGood', label: 'Good' },
  { key: 'ColorQualityUncertain', label: 'Uncertain' },
  { key: 'ColorQualityBad', label: 'Bad' },
]
const defaultTimestampFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss"

const hasConfiguredVariable = computed(() => !!props.config.Variable?.Object && !!props.config.Variable?.Name)
const showQualityColumn = computed(() => props.config.ShowQualityColumn ?? true)
const timeColumnTitle = computed(() => props.config.TimeColumnTitle || 'Timestamp')
const isStructColumnTable = computed(() => variableInfo.value?.Type === 'Struct' && variableInfo.value?.Dimension === 1)
const showStructColumns = computed(() => isStructColumnTable.value)

const displayedRows = computed(() => {
  if (sortDesc.value) {
    return [...historyRows.value].reverse()
  }
  return historyRows.value
})

const configuredStructMembers = computed(() => {
  const columns = props.config.StructColumns ?? ''
  return columns
    .split(',')
    .map((x) => x.trim())
    .filter((x) => x !== '')
})

const autoStructMembers = computed(() => {
  const set = new Set<string>()
  for (const row of historyRows.value) {
    try {
      const value = JSON.parse(row.V)
      if (value && typeof value === 'object' && !Array.isArray(value)) {
        for (const key of Object.keys(value)) {
          set.add(key)
        }
      }
    } catch {
      // skip unparseable values
    }
  }
  return Array.from(set)
})

const structMembers = computed(() => (configuredStructMembers.value.length > 0 ? configuredStructMembers.value : autoStructMembers.value))

function createDefaultConfig(): HistoryTableConfig {
  return {
    Variable: { Object: '', Name: '' },
    RowCount: 12,
    ShowQualityColumn: true,
    ColorQualityGood: 'black',
    ColorQualityUncertain: 'orange',
    ColorQualityBad: 'red',
    StructColumns: '',
    InitialPosition: 'Latest',
    TimeColumnTitle: 'Timestamp',
    TimestampFormat: defaultTimestampFormat,
    FractionDigits: 2,
  }
}

function configVars(): Record<string, string> {
  return props.configVariables?.VarValues || {}
}

function clampRowCount(value: number): number {
  if (!Number.isFinite(value)) return 12
  return Math.min(10000, Math.max(1, Math.floor(value)))
}

async function loadVariableInfo(): Promise<void> {
  if (!hasConfiguredVariable.value) {
    variableInfo.value = null
    return
  }
  try {
    variableInfo.value = await props.backendAsync('GetVariableInfo', { configVars: configVars() })
  } catch {
    variableInfo.value = null
  }
}

async function loadHistory(mode: 'First' | 'Last', startJavaTicks: number, endJavaTicks: number, keepIfEmpty = false, paging = false): Promise<void> {
  if (!hasConfiguredVariable.value) return

  if (historyLoading.value) {
    pendingLoad = { mode, startJavaTicks, endJavaTicks, keepIfEmpty, paging }
    return
  }

  historyLoading.value = true
  pendingLoad = null
  try {
    const rows: HistoryRow[] = await props.backendAsync('LoadHistory', {
      mode,
      startJavaTicks,
      endJavaTicks,
      configVars: configVars(),
    })
    if (rows.length > 0 || !keepIfEmpty) {
      historyRows.value = rows
      if (paging) {
        currentPosition.value = 'Paged'
      }
    }
  } catch (err: any) {
    historyRows.value = []
    console.error(err?.message || 'Failed to load history')
  } finally {
    historyLoading.value = false
    if (pendingLoad) {
      const { mode: m, startJavaTicks: s, endJavaTicks: e, keepIfEmpty: k, paging: p } = pendingLoad
      pendingLoad = null
      loadHistory(m, s, e, k, p)
    }
  }
}

function latestDesc(): void {
  sortDesc.value = true
  currentPosition.value = 'Latest'
  loadHistory('Last', 0, 0)
}

function oldestAsc(): void {
  sortDesc.value = false
  currentPosition.value = 'Oldest'
  loadHistory('First', 0, 0)
}

function moveDown(): void {
  if (historyRows.value.length === 0) return
  if (sortDesc.value) {
    loadHistory('Last', 0, historyRows.value[0].TJ - 1, true, true)
  } else {
    loadHistory('First', historyRows.value[historyRows.value.length - 1].TJ + 1, 0, true, true)
  }
}

function moveUp(): void {
  if (historyRows.value.length === 0) return
  if (sortDesc.value) {
    loadHistory('First', historyRows.value[historyRows.value.length - 1].TJ + 1, 0, true, true)
  } else {
    loadHistory('Last', 0, historyRows.value[0].TJ - 1, true, true)
  }
}

async function refreshInitial(): Promise<void> {
  totalCount.value = null
  await loadVariableInfo()
  if ((props.config.InitialPosition ?? 'Latest') === 'Oldest') {
    oldestAsc()
  } else {
    latestDesc()
  }
}

async function requestCount(): Promise<void> {
  countLoading.value = true
  try {
    const result: CountResponse = await props.backendAsync('CountHistory', { configVars: configVars() })
    totalCount.value = result.Count
  } finally {
    countLoading.value = false
  }
}

function qualityColor(q: Quality): string {
  if (q === 'Good') {
    const color = props.config.ColorQualityGood || 'rgb(var(--v-theme-on-surface))'
    return color.trim().toLowerCase() === 'black' ? 'rgb(var(--v-theme-on-surface))' : color
  }
  if (q === 'Uncertain') return props.config.ColorQualityUncertain || 'orange'
  return props.config.ColorQualityBad || 'red'
}

function formatNumber(value: number): string {
  const digits = props.config.FractionDigits ?? 2
  if (digits < 0) return String(value)
  return value.toFixed(Math.min(100, Math.floor(digits)))
}

function formatParsedValue(value: any): string {
  if (value === undefined || value === null) return ''
  if (typeof value === 'string') return value
  if (typeof value === 'number') return formatNumber(value)
  return JSON.stringify(value)
}

function formatRawValue(v: string): string {
  try {
    return formatParsedValue(JSON.parse(v))
  } catch {
    return v
  }
}

function getStructMemberValue(v: string, member: string): string {
  try {
    const obj = JSON.parse(v)
    if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return ''
    return formatParsedValue(obj[member])
  } catch {
    return ''
  }
}

function onContextMenu(e: MouseEvent): void {
  if (canUpdateConfig.value) {
    e.preventDefault()
    e.stopPropagation()
    contextMenu.value.show = false
    contextMenu.value.clientX = e.clientX
    contextMenu.value.clientY = e.clientY
    nextTick(() => {
      contextMenu.value.show = true
    })
  }
}

async function onConfigure(): Promise<void> {
  const response: { ObjectMap: ObjectMap; Modules: ModuleInfo[] } = await props.backendAsync('GetItemsData', {})
  objectMap.value = response.ObjectMap || {}
  selectObject.value.modules = response.Modules || []
  configDialog.value.config = normalizeConfig(props.config)
  configDialog.value.show = true
  await loadDialogVariableInfo()
}

function normalizeConfig(config: HistoryTableConfig | undefined): HistoryTableConfig {
  return {
    ...createDefaultConfig(),
    ...(JSON.parse(JSON.stringify(config ?? {})) as Partial<HistoryTableConfig>),
    Variable: {
      Object: config?.Variable?.Object ?? '',
      Name: config?.Variable?.Name ?? '',
    },
  }
}

async function loadDialogVariableInfo(): Promise<void> {
  dialogVariableInfo.value = null
  try {
    dialogVariableInfo.value = await props.backendAsync('GetVariableInfo', { variable: configDialog.value.config.Variable, configVars: configVars() })
  } catch {
    dialogVariableInfo.value = null
  }
}

function selectVariableObject(): void {
  const currObj = configDialog.value.config.Variable.Object
  const colonIndex = currObj.indexOf(':')
  selectObject.value.selectedModuleID = colonIndex > 0 ? currObj.substring(0, colonIndex) : (selectObject.value.modules[0]?.ID ?? '')
  selectObject.value.selectedObjectID = currObj
  selectObject.value.show = true
}

function onObjectSelected(obj: Obj): void {
  objectMap.value = {
    ...objectMap.value,
    [obj.ID]: {
      Name: obj.Name,
      Variables: obj.Variables || [],
    },
  }
  configDialog.value.config.Variable.Object = obj.ID
  const variables = obj.Variables || []
  if (variables.length === 1) {
    configDialog.value.config.Variable.Name = variables[0]
  } else if (!variables.includes(configDialog.value.config.Variable.Name)) {
    configDialog.value.config.Variable.Name = ''
  }
  selectObject.value.show = false
  loadDialogVariableInfo()
}

function objectName(objectId: string): string {
  if (!objectId) return ''
  return objectMap.value[objectId]?.Name ?? objectId
}

function variablesForObject(objectId: string): string[] {
  return objectMap.value[objectId]?.Variables ?? []
}

async function saveConfig(): Promise<void> {
  const config = normalizeConfig(configDialog.value.config)
  config.RowCount = clampRowCount(config.RowCount)
  await props.backendAsync('SaveConfig', { config, configVars: configVars() })
  configDialog.value.show = false
  await refreshInitial()
}

watch(
  () => props.config,
  () => {
    refreshInitial()
  },
  { deep: true },
)

watch(
  () => props.configVariables?.VarValues,
  () => {
    refreshInitial()
  },
  { deep: true },
)

watch(
  () => props.eventPayload,
  () => {
    if (props.eventName === 'OnHistoryChanged' && currentPosition.value === 'Latest') {
      latestDesc()
    }
  },
)

onMounted(() => {
  canUpdateConfig.value = (window.parent as any)['dashboardApp'].canUpdateViewConfig()
  refreshInitial()
})
</script>

<style scoped>
.history-table {
  width: 100%;
}

.history-table-toolbar {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 8px;
}

.history-table-status {
  align-items: center;
  color: rgba(var(--v-theme-on-surface), 0.72);
  display: inline-flex;
  font-size: 0.875rem;
  min-height: 32px;
}

.history-table-scroll {
  overflow-x: auto;
}

.history-table-loading,
.history-table-placeholder {
  align-items: center;
  color: rgba(var(--v-theme-on-surface), 0.72);
  display: flex;
  justify-content: center;
  min-height: 96px;
}

.history-table-color-field {
  align-items: flex-start;
  display: flex;
  gap: 8px;
}

.history-table-swatch {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.3);
  display: inline-block;
  flex: 0 0 auto;
  height: 24px;
  margin-top: 16px;
  width: 24px;
}
</style>
