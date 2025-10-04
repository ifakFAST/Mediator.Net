<template>
  <div>
    <div>
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
                :style="varHeaderStyle(colIdx)"
              >
                {{ column }}
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
            >
              {{ row }}
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

    <dlg-text-input ref="textInput"></dlg-text-input>
    <dlg-enum-input ref="enumInput"></dlg-enum-input>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import type { StyleValue } from 'vue'
import type { TimeRange } from '../../../utils'
import DlgTextInput from '../../DlgTextInput.vue'
import DlgEnumInput from './DlgEnumInput.vue'
import type { EnumValEntry } from './util'
import { parseEnumValues, onWriteItemEnum, onWriteItemNumeric } from './util'

export type UnitRenderMode = 'Hide' | 'Cell' | 'ColumnLeft' | 'ColumnRight' | 'Row'

interface Config {
  Rows: string[]
  Columns: string[]
  Items: ItemConfig[]
  UnitRenderMode: UnitRenderMode
}

interface ItemConfig {
  Unit: string
  Object: string | null
  Member: string | null
  Type: 'Range' | 'Enum'
  MinValue: number | null
  MaxValue: number | null
  EnumValues: string
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

// Template refs
const textInput = ref<InstanceType<typeof DlgTextInput> | null>(null)
const enumInput = ref<InstanceType<typeof DlgEnumInput> | null>(null)

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
  return { Unit: '', Object: null, Member: null, Type: 'Range', MinValue: null, MaxValue: null, EnumValues: '' }
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

const varHeaderStyle = (colIdx: number): StyleValue => {
  const zeroRightPadding: boolean = colIdx < props.config.Columns.length - 1
  const zeroLeftPadding: boolean = props.config.UnitRenderMode === 'Cell'
  const style: StyleValue = {}
  if (zeroLeftPadding) {
    style.paddingLeft = '0px'
  }
  if (zeroRightPadding) {
    style.paddingRight = '0px'
  }
  return style
}

const varItemStyle = (rowIdx: number, colIdx: number): StyleValue => {
  const color = colorForItem(rowIdx, colIdx)
  const zeroRightPadding: boolean = colIdx < props.config.Columns.length - 1
  const zeroLeftPadding: boolean = props.config.UnitRenderMode === 'Cell'
  const style: StyleValue = {}

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

const writeEnabled = (it: ItemConfig): boolean => {
  return true
}

const onWriteItem = async (rowIdx: number, colIdx: number): Promise<void> => {
  const it: ItemConfig = configItemFromRowColumn(rowIdx, colIdx)
  const value = valueForItem(rowIdx, colIdx)
  if (it.Type === 'Range') {
    await onWriteItemNumeric(it, value, textInputDlg, props.backendAsync)
  } else {
    await onWriteItemEnum(it, value, enumInputDlg, props.backendAsync)
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
