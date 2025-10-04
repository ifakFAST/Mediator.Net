<template>
  <v-container
    fluid
    class="pa-4"
  >
    <v-row>
      <v-col>
        <v-toolbar
          :elevation="4"
          border
          density="compact"
        >
          <div
            v-if="mdAndUp"
            class="my-toolbar-title"
          >
            {{ 'Module' }}
          </div>
          <v-toolbar-items>
            <v-btn
              v-for="module in modules"
              :key="module.ID"
              :class="{ 'text-primary': selectedModuleID === module.ID }"
              :size="modules.length > 4 ? 'small' : 'default'"
              variant="text"
              @click="refreshVariables(module.ID)"
            >
              {{ module.Name }}
            </v-btn>
          </v-toolbar-items>
          <v-spacer v-if="mdAndUp" />
          <v-text-field
            v-model="search"
            append-inner-icon="mdi-magnify"
            class="ml-4 mr-1"
            label="Search"
            single-line
          />
        </v-toolbar>

        <Locations
          v-if="locations.length > 0"
          v-model="selectedLocations"
          :locations="locations"
        />

        <v-data-table
          v-model:expanded="expanded"
          class="elevation-4 mt-2"
          :custom-filter="customFilter"
          v-model:items-per-page="perPage"
          :items-per-page-options="[50, 100, 500, 1000, { value: -1, title: 'All' }]"
          :headers="headers"
          item-value="ID"
          :items="locFilteredItems"
          :no-data-text="noDataText"
          :search="search"
          show-expand
          :single-expand="false"
        >
          <template #header.V="{}">
            <span>{{ 'Value' }}</span>
            <v-icon
              class="ml-3"
              small
              :style="{ visibility: 'hidden' }"
            >
              mdi-pencil
            </v-icon>
          </template>

          <template #item.V="{ item }">
            <div style="min-width: 12em; word-break: break-all">
              <a
                v-if="item.Writable"
                style="cursor: pointer"
                @click.stop="edit(item)"
              >
                {{ limitText(item) }}
              </a>
              <span v-else>
                {{ limitText(item) }}
              </span>
              <v-icon
                class="ml-3"
                small
                :style="{ visibility: item.Writable ? 'visible' : 'hidden' }"
                @click.stop="edit(item)"
              >
                mdi-pencil
              </v-icon>
            </div>
          </template>

          <template #item.Q="{ item }">
            <div :style="{ color: qualityColor(item.Q) }">
              {{ item.Q }}
            </div>
          </template>

          <template #item.data-table-expand="{ item, internalItem, isExpanded, toggleExpand }">
            <v-icon
              v-if="!isExpanded(internalItem) && itemNeedsExpand(item)"
              @click="() => toggleExpand(internalItem)"
            >
              mdi-chevron-down
            </v-icon>
            <v-icon
              v-if="isExpanded(internalItem) && itemNeedsExpand(item)"
              @click="() => toggleExpand(internalItem)"
            >
              mdi-chevron-up
            </v-icon>
          </template>

          <template #item.sync-read="{ item }">
            <v-icon
              v-if="item.SyncReadable"
              @click="syncRead(item)"
            >
              mdi-refresh
            </v-icon>
          </template>

          <template #expanded-row="{ columns, item }">
            <td
              class="pa-4"
              :colspan="columns.length"
            >
              <StructView
                v-if="item.Type === 'Struct' || item.Type === 'Timeseries'"
                style="float: right"
                :value="item.V"
                :vertical="item.Dimension !== 1 || item.Type === 'Timeseries'"
              />
              <div
                v-else
                style="word-break: break-all; white-space: pre-wrap"
              >
                {{ unwrapJsonString(item) }}
              </div>
            </td>
          </template>
        </v-data-table>
      </v-col>
    </v-row>
  </v-container>

  <v-dialog
    v-model="editDialog"
    max-width="290"
    @keydown="editKeydown"
  >
    <v-card>
      <v-card-title class="text-h5"> Write Variable Value </v-card-title>
      <v-card-text>
        <v-text-field
          v-model="editTmp"
          autofocus
          clearable
          :rules="[validateVariableValue]"
        />
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn
          color="grey-darken-1"
          variant="text"
          @click="editDialog = false"
        >
          Cancel
        </v-btn>
        <v-btn
          color="primary"
          variant="text"
          @click="editWrite"
        >
          Write
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { ref, computed, onMounted } from 'vue'
import { useDisplay } from 'vuetify'
import * as fast from '../fast_types'
import type { DataTableHeader } from '@/utils'

interface VarEntry {
  ID: string
  ObjID: string
  Obj: string
  Loc: string
  Var: string
  Type: fast.DataType
  Dimension: number
  Writable: boolean
  SyncReadable: boolean
  V: string
  T: fast.Timestamp
  Q: fast.Quality
}

interface Module {
  ID: string
  Name: string
}
const { mdAndUp } = useDisplay()
const expanded = ref<string[]>([])
const loading = ref(true)
const search = ref('')
const modules = ref<Module[]>([])
const selectedModuleID = ref('')
const items = ref<VarEntry[]>([])
const noDataText = 'No variables in selected module'
const perPage = ref(50)
const editTmp = ref('')
const editDialog = ref(false)
const editItem = ref<VarEntry>({
  ID: '',
  ObjID: '',
  Obj: '',
  Loc: '',
  Var: '',
  Type: 'JSON',
  Dimension: 1,
  V: '',
  T: '',
  Q: 'Bad',
  Writable: false,
  SyncReadable: false,
})

const headers: Array<DataTableHeader> = [
  { title: 'Object Name', align: 'start', sortable: false, key: 'Obj' },
  { title: 'Variable', align: 'start', sortable: false, key: 'Var' },
  { title: 'Value', align: 'end', sortable: false, key: 'V' },
  { title: 'Quality', align: 'end', sortable: false, key: 'Q' },
  { title: 'Timestamp', align: 'end', sortable: false, key: 'T' },
  { title: '', key: 'data-table-expand' },
  { title: '', key: 'sync-read' },
]

const locations = ref<fast.LocationInfo[]>([])
const mapLocations = ref<Map<string, fast.LocationInfo>>(new Map())
const selectedLocations = ref<string[][]>([[], [], [], [], [], []])

const maxValueLen = 60

const limitText = (item: VarEntry): string => {
  const str = item.V
  const MaxLen = maxValueLen
  if (str.length > MaxLen) {
    return str.substring(0, MaxLen) + '\u00A0...'
  } else {
    return str
  }
}

const unwrapJsonString = (item: VarEntry): string => {
  const value = item.V
  if (value === undefined) {
    console.log('unwrapJsonString: value is undefined')
    return ''
  }
  if (value === null) {
    console.log('unwrapJsonString: value is null')
    return ''
  }
  if (value.length === 0) {
    return value
  }
  if (value[0] !== '"') {
    return value
  }
  try {
    const str: string = JSON.parse(value)
    return str
  } catch (error) {
    return value
  }
}

const itemNeedsExpand = (item: VarEntry): boolean => {
  return item.Type === 'Struct' || item.V.length > maxValueLen
}

const setOfSelectedLocationIDs = computed<Set<string>>(() => {
  const res = new Set<string>()
  const addTree = (rootID: string) => {
    const set = new Set<string>()
    set.add(rootID)
    res.add(rootID)
    while (true) {
      const newChildren = locations.value.filter((loc) => set.has(loc.Parent) && !set.has(loc.ID))
      if (newChildren.length === 0) {
        break
      }
      for (const ch of newChildren) {
        set.add(ch.ID)
        res.add(ch.ID)
      }
    }
  }
  const mapLocationsValue = mapLocations.value
  for (let level = 0; level < selectedLocations.value.length; ++level) {
    const potentialChildrenIDs = level < selectedLocations.value.length - 1 ? selectedLocations.value[level + 1] : []
    const potentialChildren = potentialChildrenIDs.filter((id) => mapLocationsValue.has(id)).map((id) => mapLocationsValue.get(id)!)
    for (const locID of selectedLocations.value[level]) {
      if (potentialChildren.every((child) => child.Parent !== locID)) {
        addTree(locID)
      }
    }
  }
  return res
})

const locFilteredItems = computed<VarEntry[]>(() => {
  if (locations.value.length === 0) {
    return items.value
  }
  if (selectedLocations.value[1].length === 0) {
    return items.value
  }
  const set = setOfSelectedLocationIDs.value
  return items.value.filter((it) => set.has(it.Loc))
})

const customFilter = (value: string, search: string | null, item?: any): boolean => {
  if (search === null) return true
  if (item === undefined) return true
  const it: VarEntry = item.raw
  search = search.toLowerCase()
  const words = search.split(' ').filter((w) => w !== '')
  const valLower = (it.Obj + ' ' + it.Var + ' ' + it.V).toLowerCase()
  return words.every((word) => valLower.indexOf(word) !== -1)
}

const validateVariableValue = (v: any): string | boolean => {
  if (v === '') {
    return 'No empty value allowed.'
  }
  try {
    JSON.parse(v)
    return true
  } catch (error) {
    return 'Not valid JSON'
  }
}

const edit = (item: VarEntry): void => {
  editItem.value = item
  editTmp.value = item.V
  editDialog.value = true
}

const editWrite = (): void => {
  editDialog.value = false
  writeVariable(editItem.value.ObjID, editItem.value.Var, editTmp.value)
}

const syncRead = (item: VarEntry): void => {
  const params = {
    ObjID: item.ObjID,
    Var: item.Var,
  }
  // @ts-ignore
  window.parent['dashboardApp'].sendViewRequest('SyncRead', params, (strResponse: string) => {
    const response = JSON.parse(strResponse)
    const variable = items.value[response.N]
    variable.V = response.V
    variable.T = response.T
    variable.Q = response.Q
  })
}

const editKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    editDialog.value = false
  } else if (e.key === 'Enter') {
    editWrite()
  }
}

const refreshVariables = (moduleID: string): void => {
  selectedModuleID.value = moduleID
  // @ts-ignore
  window.parent['dashboardApp'].sendViewRequest('ReadModuleVariables', { ModuleID: moduleID }, (strResponse: string) => {
    const response = JSON.parse(strResponse)
    modules.value = response.Modules
    selectedModuleID.value = response.ModuleID
    items.value = response.Variables
    locations.value = response.Locations
    loading.value = false
    if (selectedLocations.value[0].length === 0) {
      const locationRootID = locations.value.find((loc) => loc.Parent === '' || loc.Parent === null)?.ID || ''
      selectedLocations.value[0].push(locationRootID)
    }
    mapLocations.value.clear()
    locations.value.forEach((loc) => {
      mapLocations.value.set(loc.ID, loc)
    })
  })
}

const qualityColor = (q: string): string => {
  if (q === 'Good') {
    return 'green'
  }
  if (q === 'Uncertain') {
    return 'orange'
  }
  return 'red'
}

const writeVariable = (objID: any, varName: string, newVal: any): void => {
  try {
    JSON.parse(newVal)
    const params = { ObjID: objID, Var: varName, V: newVal }
    // @ts-ignore
    window.parent['dashboardApp'].sendViewRequest('WriteVariable', params, (strResponse: string) => {})
  } catch (error) {
    console.log('Bad JSON: ' + error)
  }
}

onMounted(() => {
  refreshVariables('')
  // @ts-ignore
  window.parent['dashboardApp'].registerViewEventListener((eventName: string, eventPayload: any) => {
    if (eventName === 'Change') {
      const len = eventPayload.length
      const itemsValue = items.value
      for (let i = 0; i < len; i++) {
        const entry = eventPayload[i]
        const variable = itemsValue[entry.N]
        variable.V = entry.V
        variable.T = entry.T
        variable.Q = entry.Q
      }
    }
  })
})
</script>

<style>
.v-table > .v-table__wrapper > table > thead > tr > th {
  font-size: 16px;
  font-weight: bold;
}

.v-table > .v-table__wrapper > table > tbody > tr > td {
  font-size: 16px;
  height: auto;
  padding-top: 9px !important;
  padding-bottom: 9px !important;
}

.my-toolbar-title {
  font-size: 1.25rem;
  font-weight: 400;
  letter-spacing: 0;
  line-height: 1.75rem;
  text-transform: none;
  margin-left: 15px;
  margin-right: 15px;
}
</style>
