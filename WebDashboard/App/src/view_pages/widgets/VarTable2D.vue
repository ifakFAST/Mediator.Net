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
              >
                <v-tooltip
                  location="right"
                  :open-delay="250"
                >
                  <template #activator="{ props }">
                    <span v-bind="props">
                      <span :style="{ color: itemFromRowColumn(rowIdx, colIdx).ValueColor }">
                        {{ itemFromRowColumn(rowIdx, colIdx).Value }}
                      </span>
                    </span>
                  </template>
                  <span>{{ varItemInfo(itemFromRowColumn(rowIdx, colIdx)) }}</span>
                </v-tooltip>
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
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import type { StyleValue } from 'vue'
import * as fast from '../../fast_types'
import type { TimeRange } from '../../utils'
import type { ModuleInfo, ObjectMap, Obj, SelectObject, ObjInfo } from './common'

export type UnitRenderMode = 'Hide' | 'Cell' | 'ColumnLeft' | 'ColumnRight' | 'Row'

interface Config {
  Rows: string[]
  Columns: string[]
  Items: ItemConfig[]
  UnitRenderMode: UnitRenderMode
}

interface ItemConfig {
  Variable: fast.VariableRef
  WarnBelow: number | null
  WarnAbove: number | null
  AlarmBelow: number | null
  AlarmAbove: number | null
  EnumValues: string
}

interface VarItem {
  IsEmpty: boolean
  Value: string
  ValueColor: string
  Unit: string
  Time: string
  Warning?: string
  Alarm?: string
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

// Computed properties
const theHeight = computed(() => {
  if (props.height.trim() === '') return 'auto'
  return props.height
})

const configItems = computed(() => {
  return props.config.Items ?? []
})

// Methods
const itemFromRowColumn = (row: number, column: number): VarItem => {
  const cols = props.config.Columns.length
  const index = row * cols + column
  const itemsValue = items.value
  if (index < itemsValue.length) {
    return itemsValue[index]
  }
  return { IsEmpty: true, Value: '', ValueColor: '', Unit: '', Time: '' }
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
  const item: VarItem = itemFromRowColumn(rowIdx, colIdx)
  const zeroRightPadding: boolean = colIdx < props.config.Columns.length - 1
  const zeroLeftPadding: boolean = props.config.UnitRenderMode === 'Cell'
  const style: StyleValue = {}
  if (!!item.Alarm || !!item.Warning) {
    style.fontWeight = 'bold'
    style.color = varItemColor(item)
  }
  if (zeroLeftPadding) {
    style.paddingLeft = '0px'
  }
  if (zeroRightPadding) {
    style.paddingRight = '0px'
  }
  return style
}

const varItemInfo = (item: VarItem): string => {
  if (item.Alarm) return item.Alarm
  if (item.Warning) return item.Warning
  return item.Time
}

const varItemColor = (item: VarItem): string => {
  if (item.Alarm) return 'red'
  if (item.Warning) return 'orange'
  return ''
}

const onLoadData = async (): Promise<void> => {
  const loadedItems: VarItem[] = await props.backendAsync('LoadData', {})
  items.value = loadedItems
}

// Watchers
watch(
  () => props.eventPayload,
  () => {
    if (props.eventName === 'OnVarChanged') {
      const updatedItems: VarItem[] = props.eventPayload as any
      if (updatedItems.length !== items.value.length) return
      items.value.forEach((item, index) => {
        const updated = updatedItems[index]
        if (!updated.IsEmpty) {
          item.Value = updated.Value
          item.ValueColor = updated.ValueColor
          item.Time = updated.Time
          item.Warning = updated.Warning
          item.Alarm = updated.Alarm
        }
      })
    }
  },
)

// Lifecycle
onMounted(() => {
  items.value = props.config.Items.map(() => {
    const ret: VarItem = {
      IsEmpty: false,
      Value: '',
      ValueColor: '',
      Unit: '',
      Time: '',
    }
    return ret
  })
  onLoadData()
  canUpdateConfig.value = (window.parent as any)['dashboardApp'].canUpdateViewConfig()
})
</script>
