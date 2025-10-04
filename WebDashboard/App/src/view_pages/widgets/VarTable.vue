<template>
  <div>
    <div @contextmenu="onContextMenu">
      <v-table
        density="compact"
        :height="theHeight"
      >
        <thead v-if="config.ShowHeader">
          <tr>
            <th
              class="text-left"
              style="font-size: 14px"
            >
              Variable
            </th>
            <th
              class="text-right"
              style="font-size: 14px; height: 36px; min-width: 67px; padding-right: 0px"
            >
              Value
            </th>
            <th
              class="text-right"
              style="font-size: 14px; height: 36px"
            ></th>
            <th
              class="text-right"
              style="font-size: 14px; height: 36px; min-width: 86px"
            >
              Time
            </th>
            <th
              v-if="config.ShowTrendColumn"
              class="text-center"
              style="font-size: 14px; height: 36px"
            >
              Trend
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="item in items"
            :key="item.Name"
            :style="varItemStyle(item)"
          >
            <td
              class="text-left"
              style="font-size: 14px; height: 36px"
            >
              <v-tooltip
                location="right"
                :open-delay="250"
              >
                <template #activator="{ props }">
                  <span v-bind="props">{{ item.Name }}</span>
                </template>
                <span>{{ varItemInfo(item) }}</span>
              </v-tooltip>
            </td>
            <td
              class="text-right"
              style="font-size: 14px; height: 36px; padding-right: 0px; min-width: 67px"
            >
              <v-tooltip
                location="right"
                :open-delay="250"
              >
                <template #activator="{ props }">
                  <span v-bind="props"
                    ><span :style="{ color: item.ValueColor }">{{ item.Value }}</span></span
                  >
                </template>
                <span>{{ varItemInfo(item) }}</span>
              </v-tooltip>
            </td>
            <td
              class="text-left"
              style="font-size: 14px; height: 36px; padding-left: 8px"
            >
              <v-tooltip
                location="right"
                :open-delay="250"
              >
                <template #activator="{ props }">
                  <span v-bind="props">{{ item.Unit }}</span>
                </template>
                <span>{{ varItemInfo(item) }}</span>
              </v-tooltip>
            </td>
            <td
              class="text-right"
              style="font-size: 14px; height: 36px; min-width: 86px"
            >
              <v-tooltip
                location="right"
                :open-delay="250"
              >
                <template #activator="{ props }">
                  <span v-bind="props">{{ item.Time }}</span>
                </template>
                <span>{{ varItemInfo(item) }}</span>
              </v-tooltip>
            </td>
            <td
              v-if="config.ShowTrendColumn"
              class="text-center"
              style="font-size: 14px; height: 36px"
            >
              <v-icon
                v-if="item.Trend === 'up'"
                :color="varItemColor(item)"
                >mdi-arrow-top-right</v-icon
              >
              <v-icon
                v-if="item.Trend === 'down'"
                :color="varItemColor(item)"
                >mdi-arrow-bottom-right</v-icon
              >
              <v-icon
                v-if="item.Trend === 'flat'"
                :color="varItemColor(item)"
                >mdi-arrow-right</v-icon
              >
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
        <v-list-item @click="onConfigureItems">
          <v-list-item-title>Configure Items...</v-list-item-title>
        </v-list-item>
        <v-list-item @click="onToggleShowHeader">
          <v-list-item-title>{{ config.ShowHeader ? 'Hide Header' : 'Show Header' }}</v-list-item-title>
        </v-list-item>
        <v-list-item @click="onToggleShowTrendColumn">
          <v-list-item-title>{{ config.ShowTrendColumn ? 'Hide Trend Column' : 'Show Trend Column' }}</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <v-dialog
      v-model="editorItems.show"
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
                <th class="text-left">Variable</th>
                <th class="text-left">Trend Frame</th>
                <th class="text-left">Orange Below</th>
                <th class="text-left">Orange Above</th>
                <th class="text-left">Red Below</th>
                <th class="text-left">Red Above</th>
                <th class="text-left">Enum Values</th>
                <th>&nbsp;</th>
                <th>&nbsp;</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(item, idx) in editorItems.items"
                :key="idx"
              >
                <td>
                  <v-text-field v-model="item.Name"></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.Unit"
                    style="max-width: 8ch"
                  ></v-text-field>
                </td>
                <td style="font-size: 16px; max-width: 17ch; word-wrap: break-word">{{ editorItems_ObjectID2Name(item.Variable.Object) }}</td>
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
                    v-model="item.Variable.Name"
                    :items="editorItems_ObjectID2Variables(item.Variable.Object)"
                    style="width: 12ch"
                  ></v-select>
                </td>
                <td>
                  <v-text-field
                    v-model="item.TrendFrame"
                    style="max-width: 8ch"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.WarnBelow"
                    style="max-width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.WarnAbove"
                    style="max-width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.AlarmBelow"
                    style="max-width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.AlarmAbove"
                    style="max-width: 8ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="item.EnumValues"
                    style="max-width: 8ch"
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
                <td colspan="10">&nbsp;</td>
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
            @click="editorItems.show = false"
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
      :module-id="selectObject.selectedModuleID"
      :modules="selectObject.modules"
      :object-id="selectObject.selectedObjectID"
      @on-selected="selectObject_OK"
    ></dlg-object-select>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import type { StyleValue } from 'vue'
import * as fast from '../../fast_types'
import type { TimeRange } from '../../utils'
import DlgObjectSelect from '../../components/DlgObjectSelect.vue'
import type { ModuleInfo, ObjectMap, Obj, SelectObject, ObjInfo } from './common'

interface Config {
  ShowTrendColumn: boolean
  ShowHeader: boolean
  Items: ItemConfig[]
}

interface ItemConfig {
  Name: string
  Unit: string
  TrendFrame: fast.Duration
  Variable: fast.VariableRef
  WarnBelow: number | null
  WarnAbove: number | null
  AlarmBelow: number | null
  AlarmAbove: number | null
  EnumValues: string
}

interface VarItem {
  Name: string
  Value: string
  ValueColor: string
  Unit: string
  Time: string
  Trend: TrendType
  Warning?: string
  Alarm?: string
}

type TrendType = 'up' | 'down' | 'flat' | '?'

interface EditorItems {
  show: boolean
  items: ItemConfig[]
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
const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})
const objectMap = ref<ObjectMap>({})
const selectObject = ref<SelectObject>({
  show: false,
  modules: [],
  selectedModuleID: '',
  selectedObjectID: '',
})
const currentVariable = ref<fast.VariableRef>({ Object: '', Name: '' })
const editorItems = ref<EditorItems>({
  show: false,
  items: [],
})
const canUpdateConfig = ref(false)

// Computed properties
const theHeight = computed(() => {
  if (props.height.trim() === '') return 'auto'
  return props.height
})

const configItems = computed(() => {
  return props.config.Items ?? []
})

const isItemsOK = computed(() => {
  const notEmpty = (it: ItemConfig) => it.Name !== '' && it.Variable.Object !== '' && it.Variable.Name !== ''
  const names = new Set(editorItems.value.items.map((it) => it.Name))
  return editorItems.value.items.every(notEmpty) && names.size === editorItems.value.items.length
})

// Methods
const varItemStyle = (item: VarItem): StyleValue => {
  const style: StyleValue = {}
  if (!!item.Alarm || !!item.Warning) {
    style.fontWeight = 'bold'
    style.color = varItemColor(item)
  }
  return style
}

const varItemInfo = (item: VarItem): string => {
  if (item.Alarm) return item.Alarm
  if (item.Warning) return item.Warning
  return 'Quality of variable is Good'
}

const varItemColor = (item: VarItem): string => {
  if (item.Alarm) return 'red'
  if (item.Warning) return 'orange'
  return ''
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

const onDialogKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape' && !selectObject.value.show) {
    editorItems.value.show = false
  }
}

const onToggleShowTrendColumn = async (): Promise<void> => {
  try {
    await props.backendAsync('ToggleShowTrendColumn', {})
  } catch (err: any) {
    alert(err.message)
  }
}

const onToggleShowHeader = async (): Promise<void> => {
  try {
    await props.backendAsync('ToggleShowHeader', {})
  } catch (err: any) {
    alert(err.message)
  }
}

const onConfigureItems = async (): Promise<void> => {
  const response: {
    ObjectMap: ObjectMap
    Modules: ModuleInfo[]
  } = await props.backendAsync('GetItemsData', {})

  objectMap.value = response.ObjectMap
  selectObject.value.modules = response.Modules

  const str = JSON.stringify(configItems.value)
  editorItems.value.items = JSON.parse(str)
  editorItems.value.show = true
}

const onLoadData = async (): Promise<void> => {
  const loadedItems: VarItem[] = await props.backendAsync('LoadData', {})
  items.value = loadedItems
}

const editorItems_AddItem = (): void => {
  const item: ItemConfig = {
    Name: '',
    Unit: '',
    TrendFrame: '15 min',
    WarnBelow: null,
    WarnAbove: null,
    AlarmBelow: null,
    AlarmAbove: null,
    EnumValues: '',
    Variable: {
      Object: '',
      Name: '',
    },
  }
  editorItems.value.items.push(item)
}

const editorItems_DeleteItem = (idx: number): void => {
  editorItems.value.items.splice(idx, 1)
}

const editorItems_MoveUpItem = (idx: number): void => {
  const array = editorItems.value.items
  if (idx > 0) {
    const item = array[idx]
    array.splice(idx, 1)
    array.splice(idx - 1, 0, item)
  }
}

const editorItems_Save = async (): Promise<void> => {
  editorItems.value.show = false

  const para = {
    items: editorItems.value.items,
  }
  try {
    const savedItems: VarItem[] = await props.backendAsync('SaveItems', para)
    items.value = savedItems
  } catch (err: any) {
    alert(err.message)
  }
}

const editorItems_ObjectID2Name = (id: string): string => {
  const obj: ObjInfo = objectMap.value[id]
  if (obj === undefined) return id
  return obj.Name
}

const editorItems_ObjectID2Variables = (id: string): string[] => {
  const obj: ObjInfo = objectMap.value[id]
  if (obj === undefined) return []
  return obj.Variables
}

const editorItems_SelectObj = (item: ItemConfig): void => {
  const currObj: string = item.Variable.Object
  let objForModuleID: string = currObj
  if (objForModuleID === '') {
    const nonEmptyItems = editorItems.value.items.filter((it) => it.Variable.Object !== '')
    if (nonEmptyItems.length > 0) {
      objForModuleID = nonEmptyItems[0].Variable.Object
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
  currentVariable.value = item.Variable
}

const selectObject_OK = (obj: Obj): void => {
  objectMap.value[obj.ID] = {
    Name: obj.Name,
    Variables: obj.Variables,
  }
  currentVariable.value.Object = obj.ID
  if (obj.Variables.length === 1) {
    currentVariable.value.Name = obj.Variables[0]
  }
}

// Watchers
watch(
  () => props.eventPayload,
  (newVal: object) => {
    if (props.eventName === 'OnVarChanged') {
      const updatedItems: VarItem[] = props.eventPayload as any
      for (const it of updatedItems) {
        for (const at of items.value) {
          if (it.Name === at.Name) {
            at.Value = it.Value
            at.ValueColor = it.ValueColor
            at.Time = it.Time
            at.Warning = it.Warning
            at.Alarm = it.Alarm
            at.Trend = it.Trend
            break
          }
        }
      }
    }
  },
)

// Lifecycle
onMounted(() => {
  items.value = props.config.Items.map((it) => {
    const ret: VarItem = {
      Name: it.Name,
      Value: '',
      ValueColor: '',
      Unit: it.Unit,
      Time: '',
      Trend: '?',
    }
    return ret
  })
  onLoadData()
  canUpdateConfig.value = (window.parent as any)['dashboardApp'].canUpdateViewConfig()
})
</script>
