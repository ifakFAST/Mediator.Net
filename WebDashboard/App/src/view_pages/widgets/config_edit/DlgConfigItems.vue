<template>
  <div>
    <v-dialog
      v-model="show"
      max-width="1100px"
      persistent
      @keydown="onDialogKeydown"
    >
      <v-card>
        <v-card-title>
          <span class="text-h5">Configure Items</span>
        </v-card-title>
        <v-card-text>
          <v-table>
            <thead>
              <tr>
                <th class="text-left">Name</th>
                <th class="text-left">Unit</th>
                <th class="text-left">Object Name</th>
                <th>&nbsp;</th>
                <th class="text-left">Member</th>
                <th class="text-left">Type</th>
                <th class="text-left">Min</th>
                <th class="text-left">Max</th>
                <th class="text-left">Enum Values</th>
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
                  <v-text-field
                    v-model="item.Name"
                    style="max-width: 15ch"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.Unit"
                    class="ml-2 mr-2"
                    style="max-width: 8ch"
                  ></v-text-field>
                </td>
                <td style="font-size: 16px; max-width: 20ch; word-wrap: break-word">
                  {{ editorItems_ObjectID2Name(item.Object) }}
                </td>
                <td>
                  <v-btn
                    class="ml-2 mr-4"
                    icon="mdi-pencil"
                    style="min-width: 36px; width: 36px"
                    @click="editorItems_SelectObj(item)"
                  ></v-btn>
                </td>
                <td>
                  <v-select
                    v-model="item.Member"
                    class="mr-2"
                    :items="editorItems_ObjectID2Members(item.Object)"
                    style="width: 20ch"
                  ></v-select>
                </td>
                <td>
                  <v-select
                    v-model="item.Type"
                    class="mr-2"
                    :items="['Range', 'Enum']"
                    style="width: 9ch"
                  ></v-select>
                </td>
                <td>
                  <v-text-field
                    v-if="item.Type === 'Range'"
                    v-model="item.MinValue"
                    style="max-width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-if="item.Type === 'Range'"
                    v-model="item.MaxValue"
                    class="ml-2 mr-2"
                    style="max-width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-if="item.Type === 'Enum'"
                    v-model="item.EnumValues"
                    style="max-width: 14ch"
                  ></v-text-field>
                </td>
                <td>
                  <v-btn
                    class="ml-2 mr-2"
                    icon="mdi-delete"
                    style="min-width: 36px; width: 36px"
                    @click="editorItems_DeleteItem(idx)"
                  ></v-btn>
                </td>
                <td>
                  <v-btn
                    v-if="idx > 0"
                    class="ml-2 mr-2"
                    icon="mdi-chevron-up"
                    style="min-width: 36px; width: 36px"
                    @click="editorItems_MoveUpItem(idx)"
                  ></v-btn>
                </td>
              </tr>
              <tr>
                <td colspan="9">&nbsp;</td>
                <td>
                  <v-btn
                    class="ml-2 mr-2"
                    icon="mdi-plus"
                    style="min-width: 36px; width: 36px"
                    @click="editorItems_AddItem"
                  ></v-btn>
                </td>
                <td>&nbsp;</td>
              </tr>
            </tbody>
          </v-table>
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
  return items.value.every(notEmpty) && names.size === items.value.length
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
  const item: ConfigItem = {
    Name: '',
    Unit: '',
    Object: null,
    Member: null,
    Type: 'Range',
    MinValue: null,
    MaxValue: null,
    EnumValues: '0=Off; 1=On',
  }
  items.value.push(item)
}

const editorItems_Save = async (): Promise<void> => {
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
