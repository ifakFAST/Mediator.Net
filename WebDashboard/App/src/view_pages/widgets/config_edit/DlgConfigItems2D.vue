<template>
  <div>
    <v-dialog
      v-model="show"
      max-width="1200px"
      persistent
      @keydown="onDialogKeydown"
    >
      <v-card>
        <v-card-title>Configure Items</v-card-title>
        <v-card-text>
          <v-select
            v-model="selectedFilter"
            :items="filterOptions"
            item-title="label"
            return-object
            label="Show items for"
            style="max-width: 320px; margin-bottom: 8px"
          ></v-select>
          <div class="items-table-scroll">
            <table class="items-table">
              <colgroup>
                <col class="label-column">
                <col class="unit-column">
                <col class="object-column">
                <col class="edit-column">
                <col class="member-column">
                <col class="type-column">
                <col class="number-column">
                <col class="number-column">
                <col class="enum-column">
                <col class="default-column">
              </colgroup>
              <thead>
                <tr>
                  <th v-if="selectedFilter?.type === 'col'">Row</th>
                  <th v-if="selectedFilter?.type === 'row'">Column</th>
                  <th>Unit</th>
                  <th>Object Name</th>
                  <th>&nbsp;</th>
                  <th>Member</th>
                  <th>Type</th>
                  <th>Min</th>
                  <th>Max</th>
                  <th>
                    <enum-values-column-header></enum-values-column-header>
                  </th>
                  <th>Default</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="{ item, idx } in filteredItems"
                  :key="idx"
                >
                  <td
                    v-if="selectedFilter?.type === 'col'"
                    class="label-cell"
                  >{{ rowLabelForIdx(idx) }}</td>
                  <td
                    v-if="selectedFilter?.type === 'row'"
                    class="label-cell"
                  >{{ colLabelForIdx(idx) }}</td>
                  <td>
                    <v-text-field
                      v-model="item.Unit"
                    ></v-text-field>
                  </td>
                  <td class="object-cell">
                    {{ objectID2Name(item.Object) }}
                  </td>
                  <td class="edit-cell">
                    <v-btn
                      icon="mdi-pencil"
                      size="small"
                      variant="text"
                      @click="selectObj(item)"
                    ></v-btn>
                  </td>
                  <td>
                    <v-select
                      v-model="item.Member"
                      :items="objectID2Members(item.Object)"
                    ></v-select>
                  </td>
                  <td>
                    <v-select
                      v-model="item.Type"
                      :items="['Range', 'Enum']"
                    ></v-select>
                  </td>
                  <td>
                    <v-text-field
                      v-if="item.Type === 'Range'"
                      v-model="item.MinValue"
                      type="number"
                    ></v-text-field>
                  </td>
                  <td>
                    <v-text-field
                      v-if="item.Type === 'Range'"
                      v-model="item.MaxValue"
                      type="number"
                    ></v-text-field>
                  </td>
                  <td>
                    <enum-values-field
                      v-if="item.Type === 'Enum'"
                      v-model="item.EnumValues"
                      field-style="width: 100%"
                    ></enum-values-field>
                  </td>
                  <td>
                    <v-text-field
                      v-model="item.DefaultValue"
                    ></v-text-field>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-btn
            variant="text"
            @click="replaceItemsFromCsvFromClipboard"
            >Import</v-btn
          >
          <v-btn
            variant="text"
            @click="copyItemsAsCsvToClipboard"
            >Export</v-btn
          >
          <v-spacer></v-spacer>
          <v-btn
            color="grey-darken-1"
            variant="text"
            @click="closeDialog"
            >Cancel</v-btn
          >
          <v-btn
            color="primary-darken-1"
            variant="text"
            @click="save"
            >Save</v-btn
          >
        </v-card-actions>
      </v-card>
    </v-dialog>

    <dlg-object-select
      v-model="selectObject.show"
      filter="WithMembers"
      :module-id="selectObject.selectedModuleID"
      :modules="selectObject.modules"
      :object-id="selectObject.selectedObjectID"
      @onselected="selectObject_OK"
    ></dlg-object-select>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ModuleInfo, Obj, SelectObject } from '../common'
import DlgObjectSelect from '../../../components/DlgObjectSelect.vue'
import EnumValuesColumnHeader from './EnumValuesColumnHeader.vue'
import EnumValuesField from './EnumValuesField.vue'
import type { DefaultableItem } from './types'
import { normalizeItemDefaultValue, validateItemDefaultValue } from './util'

interface ItemConfig extends DefaultableItem {
  Unit: string
  Object: string | null
  Member: string | null
}

interface ObjInfo {
  Name: string
  Members: string[]
}

interface ObjectMap {
  [key: string]: ObjInfo
}

interface Props {
  config: {
    Rows: string[]
    Columns: string[]
    Items: ItemConfig[]
  }
  backendAsync: (request: string, parameters: object) => Promise<any>
}

interface FilterOption {
  label: string
  type: 'row' | 'col'
  index: number
}

const props = defineProps<Props>()

const show = ref(false)
const items = ref<ItemConfig[]>([])
const selectedFilter = ref<FilterOption | null>(null)

const filterOptions = computed<FilterOption[]>(() => {
  const opts: FilterOption[] = []
  props.config.Rows.forEach((r, i) => opts.push({ label: `Row: ${r}`, type: 'row', index: i }))
  props.config.Columns.forEach((c, i) => opts.push({ label: `Col: ${c}`, type: 'col', index: i }))
  return opts
})

const filteredItems = computed(() => {
  const colCount = props.config.Columns.length
  if (colCount === 0 || selectedFilter.value === null) return []
  return items.value
    .map((item, idx) => ({ item, idx }))
    .filter(({ idx }) =>
      selectedFilter.value!.type === 'row'
        ? Math.floor(idx / colCount) === selectedFilter.value!.index
        : idx % colCount === selectedFilter.value!.index,
    )
})
const objectMap = ref<ObjectMap>({})
const selectObject = ref<SelectObject>({
  show: false,
  modules: [],
  selectedModuleID: '',
  selectedObjectID: '',
})
const currentItem = ref<ItemConfig | null>(null)
let resolveDialog: (v: boolean) => void = () => {}

const rowLabelForIdx = (idx: number): string => {
  const colCount = props.config.Columns.length
  if (colCount === 0) return ''
  return props.config.Rows[Math.floor(idx / colCount)] ?? ''
}

const colLabelForIdx = (idx: number): string => {
  const colCount = props.config.Columns.length
  if (colCount === 0) return ''
  return props.config.Columns[idx % colCount] ?? ''
}

const objectID2Name = (id: string | null): string => {
  if (id === null || id === undefined) return ''
  const obj = objectMap.value[id]
  return obj !== undefined ? obj.Name : id
}

const objectID2Members = (id: string | null): string[] => {
  if (id === null) return []
  const obj = objectMap.value[id]
  return obj !== undefined ? obj.Members : []
}

const selectObj = (item: ItemConfig): void => {
  const currObj = item.Object ?? ''
  let objForModuleID = currObj
  if (objForModuleID === '') {
    const nonEmpty = items.value.find((it) => it.Object !== null && it.Object !== '')
    if (nonEmpty) objForModuleID = nonEmpty.Object!
  }
  const i = objForModuleID.indexOf(':')
  if (i <= 0) {
    selectObject.value.selectedModuleID = selectObject.value.modules[0]?.ID ?? ''
  } else {
    selectObject.value.selectedModuleID = objForModuleID.substring(0, i)
  }
  selectObject.value.selectedObjectID = currObj
  selectObject.value.show = true
  currentItem.value = item
}

const selectObject_OK = (obj: Obj): void => {
  const members = obj.Members ?? []
  objectMap.value[obj.ID] = { Name: obj.Name, Members: members }
  if (currentItem.value !== null) {
    currentItem.value.Object = obj.ID
    if (members.length === 1) {
      currentItem.value.Member = members[0]
    }
  }
}

const showDialog = async (): Promise<boolean> => {
  const response: { ObjectMap: ObjectMap; Modules: ModuleInfo[] } =
    await props.backendAsync('GetItemsData', {})

  objectMap.value = response.ObjectMap
  selectObject.value.modules = response.Modules

  items.value = JSON.parse(JSON.stringify(props.config.Items))
  selectedFilter.value = filterOptions.value[0] ?? null
  show.value = true

  return new Promise<boolean>((resolve) => {
    resolveDialog = resolve
  })
}

const save = async (): Promise<void> => {
  for (const item of items.value) {
    normalizeItemDefaultValue(item)
  }
  for (const item of items.value) {
    const err = validateItemDefaultValue(item)
    if (err !== '') {
      alert(err)
      return
    }
  }
  try {
    await props.backendAsync('SaveItems', { items: items.value })
  } catch (exp) {
    alert(exp)
    return
  }
  show.value = false
  resolveDialog(true)
}

const closeDialog = (): void => {
  show.value = false
  resolveDialog(false)
}

const onDialogKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape' && !selectObject.value.show) {
    closeDialog()
  }
}

const defaultItemConfig = (): ItemConfig => ({
  Unit: '',
  Object: null,
  Member: null,
  Type: 'Range',
  MinValue: null,
  MaxValue: null,
  EnumValues: '',
  DefaultValue: null,
})

const parseCsvLineToItemConfig = (
  parts: string[],
  header: string[] | undefined,
  existing: ItemConfig,
): ItemConfig => {
  const it: ItemConfig = { ...existing }

  header?.forEach((column, index) => {
    const value = parts[index] ?? ''
    switch (column) {
      case 'Unit':
        it.Unit = value
        break
      case 'Object':
        it.Object = value === '' ? null : value
        break
      case 'Member':
        it.Member = value === '' ? null : value
        break
      case 'Type':
        it.Type = value === 'Enum' ? 'Enum' : 'Range'
        break
      case 'MinValue':
        it.MinValue = value === '' ? null : Number(value)
        break
      case 'MaxValue':
        it.MaxValue = value === '' ? null : Number(value)
        break
      case 'EnumValues':
        it.EnumValues = value
        break
      case 'DefaultValue':
        it.DefaultValue = value === '' ? null : value
        break
    }
  })

  if (it.Type === 'Range') {
    it.EnumValues = ''
  } else {
    it.MinValue = null
    it.MaxValue = null
  }

  return it
}

const copyItemsAsCsvToClipboard = (): void => {
  const colCount = props.config.Columns.length
  const header = 'Row,Column,Unit,Object,Member,Type,MinValue,MaxValue,EnumValues,DefaultValue'
  const csvData = items.value
    .map((item, idx) => {
      const row = colCount > 0 ? (props.config.Rows[Math.floor(idx / colCount)] ?? '') : ''
      const column = colCount > 0 ? (props.config.Columns[idx % colCount] ?? '') : ''
      return [
        row,
        column,
        item.Unit,
        item.Object ?? '',
        item.Member ?? '',
        item.Type,
        item.MinValue ?? '',
        item.MaxValue ?? '',
        item.EnumValues,
        item.DefaultValue ?? '',
      ].join(',')
    })
    .join('\r\n')

  navigator.clipboard
    .writeText(header + '\r\n' + csvData)
    .then(() => {
      alert('CSV data copied to clipboard')
    })
    .catch((error) => {
      alert('Failed to copy CSV data to clipboard: ' + error)
    })
}

const replaceItemsFromCsvFromClipboard = (): void => {
  navigator.clipboard
    .readText()
    .then((csvData) => {
      const lines = csvData.split('\n')
      const header = lines.shift()?.trim().split(',')
      const colCount = props.config.Columns.length
      const hasRowColumn = header?.includes('Row') && header?.includes('Column')
      const updatedItems = items.value.map((item) => ({ ...item }))
      let sequentialIdx = 0

      lines
        .filter((line) => line.trim() !== '')
        .forEach((line) => {
          const parts = line.split(',')
          let targetIdx = sequentialIdx

          if (hasRowColumn) {
            const rowIdx = header!.indexOf('Row')
            const colIdx = header!.indexOf('Column')
            const rowLabel = parts[rowIdx] ?? ''
            const colLabel = parts[colIdx] ?? ''
            const r = props.config.Rows.indexOf(rowLabel)
            const c = props.config.Columns.indexOf(colLabel)
            if (r < 0 || c < 0 || colCount === 0) return
            targetIdx = r * colCount + c
          }

          if (targetIdx < 0 || targetIdx >= updatedItems.length) return

          const existing = updatedItems[targetIdx] ?? defaultItemConfig()
          updatedItems[targetIdx] = parseCsvLineToItemConfig(parts, header, existing)
          sequentialIdx++
        })

      items.value = updatedItems
    })
    .catch((error) => {
      alert('Failed to paste CSV data from clipboard: ' + error)
    })
}

defineExpose({ showDialog })
</script>

<style scoped>
.items-table-scroll {
  overflow-x: auto;
  width: 100%;
}

.items-table {
  border-collapse: collapse;
  table-layout: fixed;
  min-width: 1040px;
  width: 100%;
}

.items-table th,
.items-table td {
  padding-right: 8px;
  text-align: left;
  vertical-align: top;
}

.items-table th:last-child,
.items-table td:last-child {
  padding-right: 0;
}

.label-column {
  width: 160px;
}

.unit-column {
  width: 70px;
}

.object-column {
  width: 180px;
}

.edit-column {
  width: 44px;
}

.member-column {
  width: 148px;
}

.type-column {
  width: 100px;
}

.number-column {
  width: 86px;
}

.enum-column {
  width: 126px;
}

.default-column {
  width: 70px;
}

.label-cell {
  font-size: 14px;
  overflow-wrap: anywhere;
}

.object-cell {
  font-size: 16px;
  overflow-wrap: anywhere;
}

.edit-cell {
  text-align: center;
}

.items-table :deep(.v-input) {
  min-width: 0;
}
</style>
