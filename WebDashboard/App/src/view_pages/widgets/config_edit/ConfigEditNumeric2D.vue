<template>
  <div>
    <div @contextmenu="onContextMenu">
      <v-table
        density="compact"
        :height="theHeight"
      >
        <thead>
          <tr>
            <th
              class="text-left"
              style="font-size: 14px"
            ></th>
            <th
              v-if="config.UnitRenderMode === 'ColumnLeft'"
              class="text-left"
              style="font-size: 14px"
            ></th>
            <template
              v-for="(column, colIdx) in config.Columns"
              :key="column"
            >
              <th
                class="text-right"
                style="font-size: 14px; height: 36px"
                :style="columnHeaderStyle(colIdx)"
                @click.stop="onColumnHeaderClick(colIdx)"
              >
                <v-tooltip
                  v-if="hasDefaultsInColumn(colIdx)"
                  location="bottom"
                >
                  <template #activator="{ props: tooltipProps }">
                    <span v-bind="tooltipProps">{{ column }}</span>
                  </template>
                  <span>Apply default values</span>
                </v-tooltip>
                <template v-else>{{ column }}</template>
              </th>
              <th
                v-if="config.UnitRenderMode === 'Cell'"
                class="text-left"
                style="font-size: 14px"
              ></th>
            </template>
            <th
              v-if="config.UnitRenderMode === 'ColumnRight'"
              class="text-left"
              style="font-size: 14px"
            ></th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="config.UnitRenderMode === 'Row'">
            <td></td>
            <td
              v-for="(col, colIdx) in config.Columns"
              :key="colIdx"
              class="text-right"
              style="font-size: 14px; height: 36px; padding-right: 0px"
            >
              {{ unitFromColumn(colIdx) }}
            </td>
          </tr>

          <tr
            v-for="(row, rowIdx) in config.Rows"
            :key="rowIdx"
          >
            <td
              class="text-left"
              style="font-size: 14px; height: 36px; padding-right: 0px"
              :style="rowLabelStyle(rowIdx)"
              @click.stop="onRowLabelClick(rowIdx)"
            >
              <v-tooltip
                v-if="hasDefaultsInRow(rowIdx)"
                location="end"
              >
                <template #activator="{ props: tooltipProps }">
                  <span v-bind="tooltipProps">{{ row }}</span>
                </template>
                <span>Apply default values</span>
              </v-tooltip>
              <template v-else>{{ row }}</template>
            </td>

            <td
              v-if="config.UnitRenderMode === 'ColumnLeft'"
              class="text-left"
              style="font-size: 14px; padding-right: 0px"
            >
              {{ unitFromRow(rowIdx) }}
            </td>

            <template
              v-for="(col, colIdx) in config.Columns"
              :key="col"
            >
              <td
                class="text-right"
                style="font-size: 14px; height: 36px; min-width: 67px"
                :style="varItemStyle(rowIdx, colIdx)"
                @click="onWriteItem(rowIdx, colIdx)"
              >
                {{ valueForItem(rowIdx, colIdx) }}
              </td>
              <td
                v-if="config.UnitRenderMode === 'Cell'"
                class="text-left"
                style="font-size: 14px; padding-left: 8px"
              >
                {{ itemFromRowColumn(rowIdx, colIdx).Unit }}
              </td>
            </template>

            <td
              v-if="config.UnitRenderMode === 'ColumnRight'"
              class="text-left"
              style="font-size: 14px"
            >
              {{ unitFromRow(rowIdx) }}
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
        <v-list-item @click="onConfigureLayout">
          <v-list-item-title>Configure Layout...</v-list-item-title>
        </v-list-item>
        <v-list-item @click="onConfigureItems">
          <v-list-item-title>Configure Items...</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <dlg-config-layout
      ref="dlgConfigLayout"
      :backend-async="backendAsync"
      :config="config"
    ></dlg-config-layout>
    <dlg-config-items2-d
      ref="dlgConfigItems"
      :backend-async="backendAsync"
      :config="config"
    ></dlg-config-items2-d>
    <dlg-text-input ref="textInput"></dlg-text-input>
    <dlg-enum-input ref="enumInput"></dlg-enum-input>
    <dlg-apply-defaults ref="dlgApplyDefaults"></dlg-apply-defaults>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import type { CSSProperties } from 'vue'
import type { TimeRange } from '../../../utils'
import DlgTextInput from '../../DlgTextInput.vue'
import DlgEnumInput from './DlgEnumInput.vue'
import DlgConfigLayout from './DlgConfigLayout.vue'
import DlgConfigItems2D from './DlgConfigItems2D.vue'
import DlgApplyDefaults from './DlgApplyDefaults.vue'
import type { DefaultableItem } from './types'
import type { EnumValEntry } from './util'
import {
  parseEnumValues,
  onWriteItemEnum,
  onWriteItemNumeric,
  hasDefaultValue,
  resolveDefaultJsonValue,
  resolveDefaultDisplayValue,
} from './util'

export type UnitRenderMode = 'Hide' | 'Cell' | 'ColumnLeft' | 'ColumnRight' | 'Row'

interface Config {
  Rows: string[]
  Columns: string[]
  Items: ItemConfig[]
  UnitRenderMode: UnitRenderMode
}

interface ItemConfig extends DefaultableItem {
  Unit: string
  Object: string | null
  Member: string | null
}

interface VarItem {
  IsEmpty: boolean
  Value: string
  Unit: string
  CanEdit: boolean
}

// Props
interface Props {
  id?: string
  width?: string
  height?: string
  config: Config
  backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  eventName?: string
  eventPayload?: object
  timeRange?: TimeRange
  resize?: number
  dateWindow?: number[]
}

const props = withDefaults(defineProps<Props>(), {
  id: '',
  width: '',
  height: '',
  eventName: '',
  eventPayload: () => ({}),
  timeRange: () => ({}) as TimeRange,
  resize: 0,
  dateWindow: () => [],
})

// Reactive data
const items = ref<VarItem[]>([])
const canUpdateConfig = ref(false)
const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})

// Template refs
const dlgConfigLayout = ref<InstanceType<typeof DlgConfigLayout> | null>(null)
const dlgConfigItems = ref<InstanceType<typeof DlgConfigItems2D> | null>(null)
const textInput = ref<InstanceType<typeof DlgTextInput> | null>(null)
const enumInput = ref<InstanceType<typeof DlgEnumInput> | null>(null)
const dlgApplyDefaults = ref<InstanceType<typeof DlgApplyDefaults> | null>(null)

interface DefaultApplyPlanEntry {
  rowIdx: number
  colIdx: number
  item: ItemConfig
  currentDisplay: string
  newDisplay: string
  jsonValue: string
}

type AxisFilter =
  | { kind: 'row'; index: number }
  | { kind: 'column'; index: number }

// Computed properties
const theHeight = computed(() => {
  if (props.height.trim() === '') return 'auto'
  return props.height
})

const configItems = computed(() => {
  return props.config.Items ?? []
})

// Methods
const configItemFromRowColumn = (row: number, column: number): ItemConfig => {
  const cols = props.config.Columns.length
  const index = row * cols + column
  const configItems = props.config.Items
  if (index < configItems.length) {
    return configItems[index]
  }
  return {
    Unit: '',
    Object: null,
    Member: null,
    Type: 'Range',
    MinValue: null,
    MaxValue: null,
    EnumValues: '',
    DefaultValue: null,
  }
}

const itemFromRowColumn = (row: number, column: number): VarItem => {
  const cols = props.config.Columns.length
  const index = row * cols + column
  const itemsValue = items.value
  if (index < itemsValue.length) {
    return itemsValue[index]
  }
  return { IsEmpty: true, Value: '', Unit: '', CanEdit: false }
}

const unitFromRow = (rowIndex: number): string => {
  const cols = props.config.Columns.length
  const startIndexOfRow = rowIndex * cols
  const endIndexOfRow = startIndexOfRow + cols
  const itemsValue = items.value
  for (let i = startIndexOfRow; i < endIndexOfRow; i++) {
    if (i < itemsValue.length && itemsValue[i].Unit !== '') {
      return itemsValue[i].Unit
    }
  }
  return ''
}

const unitFromColumn = (columnIndex: number): string => {
  const cols = props.config.Columns.length
  for (let i = columnIndex; i < items.value.length; i += cols) {
    if (items.value[i].Unit !== '') {
      return items.value[i].Unit
    }
  }
  return ''
}

const columnHeaderStyle = (colIdx: number): CSSProperties => {
  const style = varHeaderStyle(colIdx)
  if (hasDefaultsInColumn(colIdx)) {
    return { ...style, cursor: 'pointer' }
  }
  return style
}

const rowLabelStyle = (rowIdx: number): CSSProperties => {
  if (hasDefaultsInRow(rowIdx)) {
    return { cursor: 'pointer' }
  }
  return {}
}

const varHeaderStyle = (colIdx: number): CSSProperties => {
  const zeroRightPadding: boolean = colIdx < props.config.Columns.length - 1
  const zeroLeftPadding: boolean = props.config.UnitRenderMode === 'Cell'
  const style: CSSProperties = {}
  if (zeroLeftPadding) {
    style.paddingLeft = '0px'
  }
  if (zeroRightPadding) {
    style.paddingRight = '0px'
  }
  return style
}

const cellIndicesForAxis = (filter: AxisFilter): { rowIdx: number; colIdx: number }[] => {
  const rows = props.config.Rows.length
  const cols = props.config.Columns.length
  const indices: { rowIdx: number; colIdx: number }[] = []
  if (filter.kind === 'row') {
    for (let colIdx = 0; colIdx < cols; colIdx++) {
      indices.push({ rowIdx: filter.index, colIdx })
    }
  } else {
    for (let rowIdx = 0; rowIdx < rows; rowIdx++) {
      indices.push({ rowIdx, colIdx: filter.index })
    }
  }
  return indices
}

const hasDefaultsInRow = (rowIdx: number): boolean => {
  const cols = props.config.Columns.length
  for (let colIdx = 0; colIdx < cols; colIdx++) {
    if (hasDefaultValue(configItemFromRowColumn(rowIdx, colIdx))) {
      return true
    }
  }
  return false
}

const hasDefaultsInColumn = (colIdx: number): boolean => {
  const rows = props.config.Rows.length
  for (let rowIdx = 0; rowIdx < rows; rowIdx++) {
    if (hasDefaultValue(configItemFromRowColumn(rowIdx, colIdx))) {
      return true
    }
  }
  return false
}

const writeEnabledAt = (rowIdx: number, colIdx: number): boolean => {
  const it = configItemFromRowColumn(rowIdx, colIdx)
  const bound =
    it.Object !== undefined && it.Object !== null && it.Object !== '' && it.Member !== undefined && it.Member !== null && it.Member !== ''
  if (!bound) {
    return false
  }
  return itemFromRowColumn(rowIdx, colIdx).CanEdit
}

const buildApplyPlan = (filter: AxisFilter): DefaultApplyPlanEntry[] => {
  const plan: DefaultApplyPlanEntry[] = []
  for (const { rowIdx, colIdx } of cellIndicesForAxis(filter)) {
    const it = configItemFromRowColumn(rowIdx, colIdx)
    if (!hasDefaultValue(it)) {
      continue
    }
    if (!writeEnabledAt(rowIdx, colIdx)) {
      continue
    }
    const jsonValue = resolveDefaultJsonValue(it)
    const newDisplay = resolveDefaultDisplayValue(it)
    if (jsonValue === null || newDisplay === null) {
      continue
    }
    plan.push({
      rowIdx,
      colIdx,
      item: it,
      currentDisplay: valueForItem(rowIdx, colIdx),
      newDisplay,
      jsonValue,
    })
  }
  return plan
}

const skippedDefaultsCountForAxis = (filter: AxisFilter): number => {
  let count = 0
  for (const { rowIdx, colIdx } of cellIndicesForAxis(filter)) {
    const it = configItemFromRowColumn(rowIdx, colIdx)
    if (!hasDefaultValue(it)) {
      continue
    }
    const bound =
      it.Object !== undefined && it.Object !== null && it.Object !== '' && it.Member !== undefined && it.Member !== null && it.Member !== ''
    if (!bound || !writeEnabledAt(rowIdx, colIdx) || resolveDefaultJsonValue(it) === null) {
      count++
    }
  }
  return count
}

const settingNameAt = (rowIdx: number, colIdx: number): string => {
  const row = props.config.Rows[rowIdx] ?? ''
  const col = props.config.Columns[colIdx] ?? ''
  return `${row}, ${col}`
}

const applyDefaultsForAxis = async (filter: AxisFilter): Promise<void> => {
  const plan = buildApplyPlan(filter)
  const skippedCount = skippedDefaultsCountForAxis(filter)
  if (!dlgApplyDefaults.value || (plan.length === 0 && skippedCount === 0)) {
    return
  }
  const dlgRows = plan.map((entry) => ({
    name: settingNameAt(entry.rowIdx, entry.colIdx),
    currentDisplay: entry.currentDisplay,
    newDisplay: entry.newDisplay,
  }))
  const ok = await dlgApplyDefaults.value.open(dlgRows, skippedCount)
  if (!ok) {
    return
  }
  if (plan.length > 0) {
    const values = plan.map((entry) => ({
      theObject: entry.item.Object,
      member: entry.item.Member,
      jsonValue: entry.jsonValue,
      displayValue: entry.newDisplay,
      oldValue: entry.currentDisplay,
    }))
    try {
      await props.backendAsync('WriteValues', { values })
    } catch (exp) {
      alert(exp)
      return
    }
  }
  await onLoadData()
}

const onApplyDefaultsForRow = (rowIdx: number): void => {
  if (!hasDefaultsInRow(rowIdx)) {
    return
  }
  void applyDefaultsForAxis({ kind: 'row', index: rowIdx })
}

const onApplyDefaultsForColumn = (colIdx: number): void => {
  if (!hasDefaultsInColumn(colIdx)) {
    return
  }
  void applyDefaultsForAxis({ kind: 'column', index: colIdx })
}

const onRowLabelClick = (rowIdx: number): void => {
  onApplyDefaultsForRow(rowIdx)
}

const onColumnHeaderClick = (colIdx: number): void => {
  onApplyDefaultsForColumn(colIdx)
}

const varItemStyle = (rowIdx: number, colIdx: number): CSSProperties => {
  const color = colorForItem(rowIdx, colIdx)
  const zeroRightPadding: boolean = colIdx < props.config.Columns.length - 1
  const zeroLeftPadding: boolean = props.config.UnitRenderMode === 'Cell'
  const style: CSSProperties = {}

  const varItem: VarItem = itemFromRowColumn(rowIdx, colIdx)

  if (varItem.CanEdit && varItem.IsEmpty === false) {
    style.cursor = 'pointer'
  }

  if (color !== '') {
    style.color = color
  }
  if (zeroLeftPadding) {
    style.paddingLeft = '0px'
  }
  if (zeroRightPadding) {
    style.paddingRight = '0px'
  }
  return style
}

const valueForItem = (rowIdx: number, colIdx: number): string => {
  const configItem: ItemConfig = configItemFromRowColumn(rowIdx, colIdx)
  const varItem: VarItem = itemFromRowColumn(rowIdx, colIdx)
  if (configItem.Type === 'Enum') {
    const vals: EnumValEntry[] = parseEnumValues(configItem.EnumValues)
    const v = varItem.Value
    const vnum: number = parseFloat(v)
    for (const item of vals) {
      if (item.num === vnum) {
        return item.label
      }
    }
    return v
  }
  return varItem.Value
}

const colorForItem = (rowIdx: number, colIdx: number): string => {
  const configItem: ItemConfig = configItemFromRowColumn(rowIdx, colIdx)
  if (configItem.Type === 'Enum') {
    const varItem: VarItem = itemFromRowColumn(rowIdx, colIdx)
    const vals: EnumValEntry[] = parseEnumValues(configItem.EnumValues)
    const v = varItem.Value
    const vnum: number = parseFloat(v)
    for (const item of vals) {
      if (item.num === vnum) {
        return item.color ?? ''
      }
    }
    return ''
  }
  return ''
}

const onLoadData = async (): Promise<void> => {
  const loadedItems: VarItem[] = await props.backendAsync('ReadValues', {})
  items.value = loadedItems
}

const onWriteItem = async (rowIdx: number, colIdx: number): Promise<void> => {
  const it: ItemConfig = configItemFromRowColumn(rowIdx, colIdx)
  const value = valueForItem(rowIdx, colIdx)

  if (it.Object === null || it.Member === null || it.Object === '' || it.Member === '' || it.Object === undefined || it.Member === undefined) {
    return
  }

  const item: DefaultableItem & { Object: string; Member: string } = {
    Type: it.Type,
    Object: it.Object,
    Member: it.Member,
    MinValue: it.MinValue,
    MaxValue: it.MaxValue,
    EnumValues: it.EnumValues,
    DefaultValue: it.DefaultValue,
  }

  // Set title as row, column:
  const title = `${props.config.Rows[rowIdx] ?? ''}, ${props.config.Columns[colIdx] ?? ''}`

  if (it.Type === 'Range') {
    await onWriteItemNumeric(item, title, value, textInputDlg, props.backendAsync)
  } else {
    await onWriteItemEnum(item, title, value, enumInputDlg, props.backendAsync)
  }
}

const textInputDlg = async (title: string, message: string, value: string, valid: (str: string) => string): Promise<string | null> => {
  if (!textInput.value) return null
  return textInput.value.openWithValidator(title, message, value, valid)
}

const enumInputDlg = async (title: string, message: string, value: string, values: string[]): Promise<string | null> => {
  if (!enumInput.value) return null
  return enumInput.value.open(title, message, value, values)
}

const onContextMenu = (e: MouseEvent): void => {
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

const onConfigureLayout = async (): Promise<void> => {
  if (!dlgConfigLayout.value) return
  const ok = await dlgConfigLayout.value.showDialog()
  if (ok) {
    await onLoadData()
  }
}

const onConfigureItems = async (): Promise<void> => {
  if (!dlgConfigItems.value) return
  const ok = await dlgConfigItems.value.showDialog()
  if (ok) {
    await onLoadData()
  }
}

// Watchers
watch(
  () => props.eventPayload,
  () => {
    if (props.eventName === 'OnValuesChanged') {
      const updatedItems: VarItem[] = props.eventPayload as any
      if (updatedItems.length !== items.value.length) return
      items.value.forEach((item, index) => {
        const updated = updatedItems[index]
        if (!updated.IsEmpty) {
          item.Value = updated.Value
          item.Unit = updated.Unit
          item.CanEdit = updated.CanEdit
        }
      })
    }
  },
)

// Lifecycle
onMounted(() => {
  items.value = props.config.Items.map((it) => {
    const ret: VarItem = {
      IsEmpty: false,
      Value: '',
      Unit: it.Unit,
      CanEdit: false,
    }
    return ret
  })
  onLoadData()
  canUpdateConfig.value = (window.parent as any)['dashboardApp'].canUpdateViewConfig()
})
</script>
