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
          <div
            v-if="items.length === 0"
            class="text-center py-8"
          >
            <div class="text-medium-emphasis mb-4">No items configured.</div>
            <v-btn
              icon="mdi-plus"
              size="small"
              variant="text"
              @click="editorItems_AddItem"
            ></v-btn>
          </div>
          <div
            v-else
            class="items-table-scroll"
          >
            <table class="items-table">
              <colgroup>
                <col class="name-column">
                <col class="unit-column">
                <col class="object-column">
                <col class="icon-column">
                <col class="member-column">
                <col class="type-column">
                <col class="number-column">
                <col class="number-column">
                <col class="enum-column">
                <col class="default-column">
                <col class="icon-column">
                <col class="icon-column">
              </colgroup>
              <thead>
                <tr>
                  <th>Name</th>
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
                  <th>&nbsp;</th>
                  <th>&nbsp;</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="(item, idx) in items"
                  :key="idx"
                >
                  <td>
                    <v-text-field v-model="item.Name"></v-text-field>
                  </td>
                  <td>
                    <v-text-field v-model="item.Unit"></v-text-field>
                  </td>
                  <td class="object-cell">
                    {{ editorItems_ObjectID2Name(item.Object) }}
                  </td>
                  <td class="icon-cell">
                    <v-btn
                      icon="mdi-pencil"
                      size="small"
                      variant="text"
                      @click="editorItems_SelectObj(item)"
                    ></v-btn>
                  </td>
                  <td>
                    <v-select
                      v-model="item.Member"
                      :items="editorItems_ObjectID2Members(item.Object)"
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
                    <v-text-field v-model="item.DefaultValue"></v-text-field>
                  </td>
                  <td class="icon-cell">
                    <v-btn
                      icon="mdi-delete"
                      size="small"
                      variant="text"
                      @click="editorItems_DeleteItem(idx)"
                    ></v-btn>
                  </td>
                  <td class="icon-cell">
                    <v-btn
                      v-if="idx > 0"
                      icon="mdi-chevron-up"
                      size="small"
                      variant="text"
                      @click="editorItems_MoveUpItem(idx)"
                    ></v-btn>
                  </td>
                </tr>
                <tr>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                  <td class="icon-cell">
                    <v-btn
                      icon="mdi-plus"
                      size="small"
                      variant="text"
                      @click="editorItems_AddItem"
                    ></v-btn>
                  </td>
                  <td>&nbsp;</td>
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
            :disabled="!isItemsOK"
            variant="text"
            @click="editorItems_Save"
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
import { ref, computed } from 'vue'
import type { ModuleInfo, Obj, SelectObject } from '../common'
import type { ConfigItem } from './types'
import DlgObjectSelect from '../../../components/DlgObjectSelect.vue'
import EnumValuesColumnHeader from './EnumValuesColumnHeader.vue'
import EnumValuesField from './EnumValuesField.vue'
import { normalizeItemDefaultValue, validateItemDefaultValue } from './util'

interface ObjInfo {
  Name: string
  Members: string[]
}

interface ObjectMap {
  [key: string]: ObjInfo
}

// Props
interface Props {
  configItems?: ConfigItem[]
  backendAsync: (request: string, parameters: object) => Promise<any>
}

const props = withDefaults(defineProps<Props>(), {
  configItems: () => [],
})

// Reactive data
const show = ref(false)
const items = ref<ConfigItem[]>([])
const objectMap = ref<ObjectMap>({})
const selectObject = ref<SelectObject>({
  show: false,
  modules: [],
  selectedModuleID: '',
  selectedObjectID: '',
})
const currentItem = ref<ConfigItem | null>(null)
let resolveDialog: (v: boolean) => void = () => {}

// Computed properties
const isItemsOK = computed(() => {
  const notEmpty = (it: ConfigItem) => it.Name !== '' /* && it.Object !== '' && it.Member !== '' */
  const names = new Set(items.value.map((it) => it.Name))
  const defaultsValid = items.value.every((it) => validateItemDefaultValue(it) === '')
  return items.value.every(notEmpty) && names.size === items.value.length && defaultsValid
})

// Methods
const showDialog = async (): Promise<boolean> => {
  const response: {
    ObjectMap: ObjectMap
    Modules: ModuleInfo[]
  } = await props.backendAsync('GetItemsData', {})

  objectMap.value = response.ObjectMap
  selectObject.value.modules = response.Modules

  const str = JSON.stringify(props.configItems)
  items.value = JSON.parse(str)
  show.value = true

  return new Promise<boolean>((resolve) => {
    resolveDialog = resolve
  })
}

const editorItems_ObjectID2Name = (id: string | null): string => {
  if (id === undefined || id === null) {
    return ''
  }
  const obj: ObjInfo = objectMap.value[id]
  if (obj === undefined) {
    return id
  }
  return obj.Name
}

const editorItems_SelectObj = (item: ConfigItem): void => {
  const currObj: string = item.Object === null ? '' : item.Object
  let objForModuleID: string = currObj
  if (objForModuleID === '') {
    const nonEmptyItems = items.value.filter((it) => it.Object !== null && it.Object !== '')
    if (nonEmptyItems.length > 0) {
      objForModuleID = nonEmptyItems[0].Object!
    }
  }

  const i = objForModuleID.indexOf(':')
  if (i <= 0) {
    selectObject.value.selectedModuleID = selectObject.value.modules[0].ID
  } else {
    selectObject.value.selectedModuleID = objForModuleID.substring(0, i)
  }
  selectObject.value.selectedObjectID = currObj
  selectObject.value.show = true
  currentItem.value = item
}

const editorItems_ObjectID2Members = (id: string | null): string[] => {
  if (id === null) {
    return []
  }
  const obj: ObjInfo = objectMap.value[id]
  if (obj === undefined) {
    return []
  }
  return obj.Members
}

const editorItems_DeleteItem = (idx: number): void => {
  items.value.splice(idx, 1)
}

const editorItems_MoveUpItem = (idx: number): void => {
  const array = items.value
  if (idx > 0) {
    const item = array[idx]
    array.splice(idx, 1)
    array.splice(idx - 1, 0, item)
  }
}

const editorItems_AddItem = (): void => {
  items.value.push(defaultItemConfig())
}

const defaultItemConfig = (): ConfigItem => ({
  Name: '',
  Unit: '',
  Object: null,
  Member: null,
  Type: 'Range',
  MinValue: null,
  MaxValue: null,
  EnumValues: '0=Off; 1=On',
  DefaultValue: null,
})

const parseCsvLineToItemConfig = (parts: string[], header: string[] | undefined): ConfigItem => {
  const it = defaultItemConfig()

  header?.forEach((column, index) => {
    const value = parts[index] ?? ''
    switch (column) {
      case 'Name':
        it.Name = value
        break
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
  const header = 'Name,Unit,Object,Member,Type,MinValue,MaxValue,EnumValues,DefaultValue'
  const csvData = items.value
    .map((item) =>
      [
        item.Name ?? '',
        item.Unit,
        item.Object ?? '',
        item.Member ?? '',
        item.Type,
        item.MinValue ?? '',
        item.MaxValue ?? '',
        item.EnumValues,
        item.DefaultValue ?? '',
      ].join(','),
    )
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

      items.value = lines
        .filter((line) => line.trim() !== '')
        .map((line) => parseCsvLineToItemConfig(line.split(','), header))
    })
    .catch((error) => {
      alert('Failed to paste CSV data from clipboard: ' + error)
    })
}

const editorItems_Save = async (): Promise<void> => {
  for (const item of items.value) {
    normalizeItemDefaultValue(item)
  }
  const para = {
    items: items.value,
  }
  try {
    await props.backendAsync('SaveItems', para)
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

const selectObject_OK = (obj: Obj): void => {
  const members = obj.Members ?? []
  objectMap.value[obj.ID] = {
    Name: obj.Name,
    Members: members,
  }
  if (currentItem.value !== null) {
    currentItem.value.Object = obj.ID
    if (members.length === 1) {
      currentItem.value.Member = members[0]
    }
  }
}

const onDialogKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape' && !selectObject.value.show) {
    closeDialog()
  }
}

// Expose methods for parent component
defineExpose({
  showDialog,
})
</script>

<style scoped>
.items-table-scroll {
  overflow-x: auto;
  width: 100%;
}

.items-table {
  border-collapse: collapse;
  table-layout: fixed;
  min-width: 1120px;
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

.name-column {
  width: 156px;
}

.unit-column {
  width: 70px;
}

.object-column {
  width: 170px;
}

.icon-column {
  width: 44px;
}

.member-column {
  width: 132px;
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
  width: 86px;
}

.object-cell {
  font-size: 16px;
  overflow-wrap: anywhere;
}

.icon-cell {
  text-align: center;
}

.items-table :deep(.v-input) {
  min-width: 0;
}
</style>
