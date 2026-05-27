<template>
  <div>
    <v-dialog
      v-model="show"
      max-width="900px"
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
          <table style="border-collapse: collapse">
            <thead>
              <tr>
                <th
                  v-if="selectedFilter?.type === 'col'"
                  style="text-align: left"
                >Row</th>
                <th
                  v-if="selectedFilter?.type === 'row'"
                  style="text-align: left"
                >Column</th>
                <th style="text-align: left">Unit</th>
                <th style="text-align: left">Object Name</th>
                <th>&nbsp;</th>
                <th style="text-align: left">Member</th>
                <th style="text-align: left">Type</th>
                <th style="text-align: left">Min</th>
                <th style="text-align: left">Max</th>
                <th style="text-align: left">
                  <enum-values-column-header></enum-values-column-header>
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="{ item, idx } in filteredItems"
                :key="idx"
              >
                <td
                  v-if="selectedFilter?.type === 'col'"
                  style="font-size: 14px; padding-right: 1ex; white-space: break-word"
                >{{ rowLabelForIdx(idx) }}</td>
                <td
                  v-if="selectedFilter?.type === 'row'"
                  style="font-size: 14px; padding-right: 1ex; white-space: break-word"
                >{{ colLabelForIdx(idx) }}</td>
                <td>
                  <v-text-field
                    v-model="item.Unit"
                    class="mr-2"
                    style="width: 7ch"
                  ></v-text-field>
                </td>
                <td style="font-size: 16px; max-width: 20ch; word-wrap: break-word">
                  {{ objectID2Name(item.Object) }}
                </td>
                <td>
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
                    style="margin-left: 1ex; width: 14ch"
                  ></v-select>
                </td>
                <td>
                  <v-select
                    v-model="item.Type"
                    :items="['Range', 'Enum']"
                    style="margin-left: 1ex; width: 9ch"
                  ></v-select>
                </td>
                <td>
                  <v-text-field
                    v-if="item.Type === 'Range'"
                    v-model="item.MinValue"
                    style="margin-left: 1ex; width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-if="item.Type === 'Range'"
                    v-model="item.MaxValue"
                    style="margin-left: 1ex; width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <enum-values-field
                    v-if="item.Type === 'Enum'"
                    v-model="item.EnumValues"
                  ></enum-values-field>
                </td>
              </tr>
            </tbody>
          </table>
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

interface ItemConfig {
  Unit: string
  Object: string | null
  Member: string | null
  Type: 'Range' | 'Enum'
  MinValue: number | null
  MaxValue: number | null
  EnumValues: string
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
  const header = 'Row,Column,Unit,Object,Member,Type,MinValue,MaxValue,EnumValues'
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
