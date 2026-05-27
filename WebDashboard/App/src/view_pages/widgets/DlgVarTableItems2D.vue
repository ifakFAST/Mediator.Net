<template>
  <div>
    <v-dialog
      v-model="show"
      max-width="1100px"
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
                <th style="text-align: left">Object Name</th>
                <th>&nbsp;</th>
                <th style="text-align: left">Variable</th>
                <th style="text-align: left">Orange Below</th>
                <th style="text-align: left">Orange Above</th>
                <th style="text-align: left">Red Below</th>
                <th style="text-align: left">Red Above</th>
                <th style="text-align: left">Enum Values</th>
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
                <td style="font-size: 16px; max-width: 20ch; word-wrap: break-word">
                  {{ objectID2Name(item.Variable.Object) }}
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
                    v-model="item.Variable.Name"
                    :items="objectID2Variables(item.Variable.Object)"
                    style="margin-left: 1ex; width: 14ch"
                  ></v-select>
                </td>
                <td>
                  <v-text-field
                    v-model="item.WarnBelow"
                    style="margin-left: 1ex; width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.WarnAbove"
                    style="margin-left: 1ex; width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.AlarmBelow"
                    style="margin-left: 1ex; width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.AlarmAbove"
                    style="margin-left: 1ex; width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.EnumValues"
                    style="width: 16ch"
                  ></v-text-field>
                </td>
              </tr>
            </tbody>
          </table>
        </v-card-text>
        <v-card-actions>
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
      :module-id="selectObject.selectedModuleID"
      :modules="selectObject.modules"
      :object-id="selectObject.selectedObjectID"
      @onselected="selectObject_OK"
    ></dlg-object-select>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import * as fast from '../../fast_types'
import type { ModuleInfo, Obj, ObjInfo, ObjectMap, SelectObject } from './common'
import DlgObjectSelect from '../../components/DlgObjectSelect.vue'

interface ItemConfig {
  Variable: fast.VariableRef | null
  WarnBelow: number | null
  WarnAbove: number | null
  AlarmBelow: number | null
  AlarmAbove: number | null
  EnumValues: string
}

interface EditableItemConfig extends Omit<ItemConfig, 'Variable'> {
  Variable: fast.VariableRef
}

const emptyVariable = (): fast.VariableRef => ({ Object: '', Name: '' })

const normalizeItemForEdit = (item: ItemConfig): EditableItemConfig => ({
  ...item,
  Variable: item.Variable ?? emptyVariable(),
})

const sanitizeItemForSave = (item: EditableItemConfig): ItemConfig => {
  const v = item.Variable
  const hasVariable = v.Object !== '' && v.Name !== ''
  return { ...item, Variable: hasVariable ? v : null }
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
const items = ref<EditableItemConfig[]>([])
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
const currentVariable = ref<fast.VariableRef>({ Object: '', Name: '' })
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

const objectID2Name = (id: string): string => {
  if (id === '') return ''
  const obj = objectMap.value[id]
  return obj !== undefined ? obj.Name : id
}

const objectID2Variables = (id: string): string[] => {
  if (id === '') return []
  const obj = objectMap.value[id]
  return obj !== undefined ? obj.Variables : []
}

const selectObj = (item: EditableItemConfig): void => {
  const currObj = item.Variable.Object
  let objForModuleID = currObj
  if (objForModuleID === '') {
    const nonEmpty = items.value.find((it) => it.Variable.Object !== '')
    if (nonEmpty) objForModuleID = nonEmpty.Variable.Object
  }
  const i = objForModuleID.indexOf(':')
  if (i <= 0) {
    selectObject.value.selectedModuleID = selectObject.value.modules[0]?.ID ?? ''
  } else {
    selectObject.value.selectedModuleID = objForModuleID.substring(0, i)
  }
  selectObject.value.selectedObjectID = currObj
  selectObject.value.show = true
  currentVariable.value = item.Variable
}

const selectObject_OK = (obj: Obj): void => {
  const variables = obj.Variables ?? []
  objectMap.value[obj.ID] = { Name: obj.Name, Variables: variables }
  currentVariable.value.Object = obj.ID
  if (variables.length === 1) {
    currentVariable.value.Name = variables[0]
  }
}

const showDialog = async (): Promise<boolean> => {
  const response: { ObjectMap: ObjectMap; Modules: ModuleInfo[] } =
    await props.backendAsync('GetItemsData', {})

  objectMap.value = response.ObjectMap
  selectObject.value.modules = response.Modules

  items.value = JSON.parse(JSON.stringify(props.config.Items)).map(normalizeItemForEdit)
  selectedFilter.value = filterOptions.value[0] ?? null
  show.value = true

  return new Promise<boolean>((resolve) => {
    resolveDialog = resolve
  })
}

const save = async (): Promise<void> => {
  try {
    const theItems = items.value.map(sanitizeItemForSave)
    await props.backendAsync('SaveItems', { items: theItems })
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

defineExpose({ showDialog })
</script>
