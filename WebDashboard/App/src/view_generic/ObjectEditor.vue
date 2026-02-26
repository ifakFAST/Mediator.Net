<template>
  <div class="d-flex flex-column overflow-hidden h-100">
    <v-toolbar
      class="flex-shrink-0"
      density="compact"
      :elevation="4"
      extended
    >
      <v-toolbar-title
        class="my-toolbar-title"
        :text="title"
      ></v-toolbar-title>
      <v-btn
        :disabled="!isDirty"
        variant="text"
        @click="save"
        >Save</v-btn
      >
      <v-btn
        variant="text"
        @click="deleteObject"
        >Delete</v-btn
      >
      <v-btn
        v-for="t in childTypes"
        :key="t.TypeName"
        variant="text"
        @click="showAddChildDialog(t)"
        >{{ 'Add ' + getShortTypeName(t.TypeName) }}</v-btn
      >
      <v-menu
        location="bottom left"
        offset-y
      >
        <template #activator="{ props: activatorProps }">
          <v-btn
            v-bind="activatorProps"
            icon="mdi-dots-vertical"
          ></v-btn>
        </template>

        <v-list>
          <v-list-item
            :disabled="selection === null || selection.First"
            @click="moveUp"
          >
            <v-list-item-title>Move Up</v-list-item-title>
          </v-list-item>
          <v-list-item
            :disabled="selection === null || selection.Last"
            @click="moveDown"
          >
            <v-list-item-title>Move Down</v-list-item-title>
          </v-list-item>
          <v-list-item
            v-if="isExportable"
            @click="doExport"
          >
            <v-list-item-title>Export</v-list-item-title>
          </v-list-item>
          <v-list-item
            v-if="isImportable"
            @click="doImport"
          >
            <v-list-item-title>Import</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>

      <template #extension>
        <a
          v-for="cat in categoryMembers"
          :key="cat.Category"
          :class="classObject(cat.Category)"
          @click="currentTab = cat.Category"
          >{{ cat.Category }}</a
        >
        <a
          :class="classObject('Variables')"
          @click="currentTab = 'Variables'"
          >Variables</a
        >
      </template>
    </v-toolbar>

    <template
      v-for="cat in categoryMembers"
      :key="cat.Category"
    >
      <div
        v-if="currentTab === cat.Category"
        class="flex-grow-1 overflow-y-auto"
      >
        <form>
          <table
            cellspacing="10"
            style="width: 100%"
          >
            <tbody>
              <template
                v-for="row in cat.Members"
                :key="row.Key"
              >
                <tr v-if="row.IsScalar">
                  <td style="width: 100%">
                    <v-textarea
                      v-if="row.Type === 'String' && row.Name === 'Address'"
                      v-model="row.Value"
                      auto-grow
                      :label="row.Name"
                      rows="1"
                    ></v-textarea>
                    <v-text-field
                      v-if="row.Type === 'String' && row.Name !== 'Address'"
                      v-model="row.Value"
                      :label="row.Name"
                      :readonly="row.Name === 'ID'"
                    ></v-text-field>
                    <v-text-field
                      v-if="row.Type === 'JSON'"
                      v-model="row.Value"
                      :label="row.Name"
                    ></v-text-field>
                    <v-checkbox
                      v-if="row.Type === 'Bool'"
                      v-model="row.Value"
                      :label="row.Name"
                    ></v-checkbox>
                    <v-text-field
                      v-if="row.Type === 'Int32'"
                      v-model="row.Value"
                      :label="row.Name"
                    ></v-text-field>
                    <v-text-field
                      v-if="row.Type === 'Int64'"
                      v-model="row.Value"
                      :label="row.Name"
                    ></v-text-field>
                    <v-text-field
                      v-if="row.Type === 'Duration'"
                      v-model="row.Value"
                      :label="row.Name"
                    ></v-text-field>
                    <v-select
                      v-if="row.Type === 'Enum'"
                      v-model="row.Value"
                      :items="row.EnumValues"
                      :label="row.Name"
                    ></v-select>
                    <location
                      v-if="row.Type === 'LocationRef'"
                      v-model="row.Value"
                      :label="row.Name"
                      :locations="locations"
                    ></location>
                    <template v-if="row.Type === 'Struct' && row.TypeConstraints !== 'Ifak.Fast.Mediator.History'">
                      <p class="font-weight-bold">{{ row.Name }}</p>
                      <struct-editor
                        :members="row.StructMembers"
                        :value="row.Value"
                        @update:value="row.Value = $event"
                      ></struct-editor>
                    </template>
                    <template v-if="row.Type === 'Struct' && row.TypeConstraints === 'Ifak.Fast.Mediator.History'">
                      <p class="font-weight-bold">{{ row.Name }}</p>
                      <history-editor v-model="row.Value" />
                    </template>
                  </td>
                  <td>
                    <v-btn
                      v-if="row.Type === 'String' && row.Browseable"
                      style="min-width: 18px"
                      @click="loadBrowseable(row.Name, row.Value)"
                      >Browse</v-btn
                    >
                  </td>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                </tr>

                <tr v-if="row.IsOption && row.Value !== null">
                  <td style="width: 100%">
                    <v-text-field
                      v-if="row.Type === 'String'"
                      v-model="row.Value"
                      :label="row.Name"
                    ></v-text-field>
                    <v-text-field
                      v-if="row.Type === 'JSON'"
                      v-model="row.Value"
                      :label="row.Name"
                    ></v-text-field>
                    <v-checkbox
                      v-if="row.Type === 'Bool'"
                      v-model="row.Value"
                      :label="row.Name"
                    ></v-checkbox>
                    <v-text-field
                      v-if="row.Type === 'Int32'"
                      v-model="row.Value"
                      :label="row.Name"
                    ></v-text-field>
                    <v-text-field
                      v-if="row.Type === 'Int64'"
                      v-model="row.Value"
                      :label="row.Name"
                    ></v-text-field>
                    <v-text-field
                      v-if="row.Type === 'Duration'"
                      v-model="row.Value"
                      :label="row.Name"
                    ></v-text-field>
                    <v-select
                      v-if="row.Type === 'Enum'"
                      v-model="row.Value"
                      :items="row.EnumValues"
                      :label="row.Name"
                    ></v-select>
                    <location
                      v-if="row.Type === 'LocationRef'"
                      v-model="row.Value"
                      :label="row.Name"
                      :locations="locations"
                    ></location>
                    <template v-if="row.Type === 'Struct' && row.TypeConstraints !== 'Ifak.Fast.Mediator.History'">
                      <p class="font-weight-bold">{{ row.Name }}</p>
                      <struct-editor
                        :members="row.StructMembers"
                        :value="row.Value"
                        @update:value="row.Value = $event"
                      ></struct-editor>
                    </template>
                    <template v-if="row.Type === 'Struct' && row.TypeConstraints === 'Ifak.Fast.Mediator.History'">
                      <p class="font-weight-bold">{{ row.Name }}</p>
                      <history-editor v-model="row.Value" />
                    </template>
                  </td>
                  <td>
                    <v-btn
                      icon="mdi-delete-forever"
                      size="small"
                      @click="removeOptional(row)"
                    ></v-btn>
                  </td>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                </tr>

                <tr v-if="row.IsOption && row.Value === null">
                  <td>
                    <span class="font-weight-bold">{{ row.Name }}</span
                    >: Not set
                  </td>
                  <td>
                    <v-btn
                      size="small"
                      @click="setOptional(row)"
                      >Set</v-btn
                    >
                  </td>
                  <td>&nbsp;</td>
                  <td>&nbsp;</td>
                </tr>

                <template v-if="row.IsArray && row.Type !== 'Struct'">
                  <tr>
                    <td colspan="4">
                      <p class="font-weight-bold">{{ row.Name }}</p>
                    </td>
                  </tr>
                  <tr
                    v-for="(v, idx) in asArray(row.Value)"
                    :key="row.Key + '_' + idx"
                  >
                    <td>
                      <v-text-field
                        v-if="row.Type === 'String'"
                        v-model="row.Value[idx]"
                        :label="'[' + idx + ']'"
                      ></v-text-field>
                      <v-text-field
                        v-if="row.Type === 'JSON'"
                        v-model="row.Value[idx]"
                        :label="'[' + idx + ']'"
                      ></v-text-field>
                      <v-checkbox
                        v-if="row.Type === 'Bool'"
                        v-model="row.Value[idx]"
                        :label="'[' + idx + ']'"
                      ></v-checkbox>
                      <v-text-field
                        v-if="row.Type === 'Int32'"
                        v-model="row.Value[idx]"
                        :label="'[' + idx + ']'"
                      ></v-text-field>
                      <v-text-field
                        v-if="row.Type === 'Int64'"
                        v-model="row.Value[idx]"
                        :label="'[' + idx + ']'"
                      ></v-text-field>
                      <v-text-field
                        v-if="row.Type === 'Duration'"
                        v-model="row.Value[idx]"
                        :label="'[' + idx + ']'"
                      ></v-text-field>
                      <v-select
                        v-if="row.Type === 'Enum'"
                        v-model="row.Value[idx]"
                        :items="row.EnumValues"
                        :label="'[' + idx + ']'"
                      ></v-select>
                      <table
                        v-if="row.Type === 'NamedValue'"
                        style="width: 100%"
                      >
                        <tbody>
                          <tr>
                            <td style="vertical-align: top; width: 40%">
                              <v-text-field
                                v-model="row.Value[idx].Name"
                                label="Name"
                              ></v-text-field>
                            </td>
                            <td style="width: 60%">
                              <v-textarea
                                v-model="row.Value[idx].Value"
                                auto-grow
                                label="Value"
                                rows="1"
                              ></v-textarea>
                            </td>
                          </tr>
                        </tbody>
                      </table>
                    </td>
                    <td>
                      <v-btn
                        icon="mdi-delete-forever"
                        size="small"
                        @click="removeArrayItem(row.Value, idx)"
                      ></v-btn>
                    </td>
                    <td>
                      <v-btn
                        v-if="idx > 0"
                        icon="mdi-chevron-up"
                        size="small"
                        @click="moveUpArrayItem(row.Value, idx)"
                      ></v-btn>
                    </td>
                    <td>&nbsp;</td>
                  </tr>
                  <tr>
                    <td style="text-align: right">
                      <v-btn
                        icon="mdi-plus"
                        size="small"
                        @click="addArrayItem(row)"
                      ></v-btn>
                    </td>
                    <td>&nbsp;</td>
                    <td>&nbsp;</td>
                    <td>&nbsp;</td>
                  </tr>
                </template>

                <template v-if="row.IsArray && row.Type === 'Struct'">
                  <tr>
                    <td colspan="4">
                      <p class="font-weight-bold">{{ row.Name }}</p>
                    </td>
                  </tr>
                  <tr>
                    <td colspan="4">
                      <struct-array-editor
                        :default-value="row.DefaultValue"
                        :members="row.StructMembers"
                        :values="row.Value"
                        @update:values="row.Value = $event"
                      ></struct-array-editor>
                    </td>
                  </tr>
                </template>
              </template>
            </tbody>
          </table>
        </form>
      </div>
    </template>

    <div
      v-if="currentTab === 'Variables'"
      class="flex-grow-1 overflow-y-auto"
    >
      <v-data-table
        class="elevation-1 mx-1 mt-2 mb-1"
        :headers="varHeaders"
        hide-default-footer
        :items="selection?.Variables || []"
        no-data-text="No variables"
      >
        <template #item="{ item }">
          <tr>
            <td style="vertical-align: top">{{ item.Name }}</td>
            <td class="text-right">
              <a @click.stop="editVar(item)">{{ item.V }}</a>
            </td>
            <td
              class="text-right"
              style="vertical-align: top"
              :style="{ color: qualityColor(item.Q) }"
            >
              {{ item.Q }}
            </td>
            <td
              class="text-right"
              style="vertical-align: top"
            >
              {{ item.T }}
            </td>
          </tr>
        </template>
      </v-data-table>

      <v-dialog
        v-model="editVarDialog"
        max-width="290"
        @keydown="editVarKeydown"
      >
        <v-card>
          <v-card-title class="headline">Write Variable Value</v-card-title>
          <v-card-text>
            <v-text-field
              ref="editTextInput"
              v-model="editVarTmp"
              autofocus
              clearable
              label="Edit"
              :rules="[validateVariableValue]"
              single-line
            ></v-text-field>
          </v-card-text>
          <v-card-actions>
            <v-spacer></v-spacer>
            <v-btn
              color="blue-darken-1"
              variant="text"
              @click.stop="editVarDialog = false"
              >Cancel</v-btn
            >
            <v-btn
              color="blue-darken-1"
              variant="text"
              @click.stop="editVarWrite"
              >Write</v-btn
            >
          </v-card-actions>
        </v-card>
      </v-dialog>
    </div>

    <v-dialog
      v-model="addDialog.show"
      max-width="350px"
      persistent
      @keydown="editKeydown"
    >
      <v-card>
        <v-card-title>
          <span class="headline">Add new {{ addDialog.typeNameShort }}</span>
        </v-card-title>
        <v-card-text>
          <v-text-field
            ref="txtID"
            v-model="addDialog.newID"
            autofocus
            hint="The unique and immutable identifier of the new object"
            label="ID"
          ></v-text-field>
          <v-text-field
            ref="txtName"
            v-model="addDialog.newName"
            label="Name"
          ></v-text-field>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn
            color="blue-darken-1"
            variant="text"
            @click="onAddNewObject"
            >Add</v-btn
          >
          <v-btn
            color="red-darken-1"
            variant="text"
            @click="addDialog.show = false"
            >Cancel</v-btn
          >
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog
      v-model="browseDialog.show"
      max-width="900px"
      persistent
      scrollable
      @keydown="browseKeydown"
    >
      <v-card>
        <v-card-title>
          <v-text-field
            v-show="!browseValuesLoading"
            ref="txtSearch"
            v-model="search"
            autofocus
            label="Search Text"
          ></v-text-field>
        </v-card-title>
        <v-divider></v-divider>
        <v-card-text style="height: 520px">
          <v-data-table
            v-model="browseDialog.selection"
            class="elevation-2 mt-2"
            density="compact"
            :footer-props="browseFooter"
            :headers="browseHeaders"
            item-value="it"
            :items="filteredBrowseValuesObj"
            :loading="browseValuesLoading"
            no-data-text="No values"
            show-select
            select-strategy="single"
            return-object
          >
          </v-data-table>
        </v-card-text>
        <v-divider></v-divider>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn
            color="blue-darken-1"
            :disabled="browseDialog.selection.length === 0"
            variant="text"
            @click="browseOK"
            >OK</v-btn
          >
          <v-btn
            color="red-darken-1"
            variant="text"
            @click="browseDialog.show = false"
            >Cancel</v-btn
          >
        </v-card-actions>
      </v-card>
    </v-dialog>

    <confirm ref="confirm"></confirm>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import StructEditor from './StructEditor.vue'
import StructArrayEditor from './StructArrayEditor.vue'
import Confirm from '../components/Confirm.vue'
import HistoryEditor from '../components/HistoryEditor.vue'
import Location from './Location.vue'
import { isValidObjectNameOrID } from '../utils'
import type { TreeNode, TypeMap, ObjectMember, ChildType, AddObjectParams, SaveMember } from './types'
import type { LocationInfo } from '../fast_types'

interface BrowseItem {
  it: string
}

interface CategoryMember {
  Category: string
  Members: ObjectMember[]
}

interface Props {
  selection: TreeNode | null
  members: ObjectMember[]
  childTypes: ChildType[]
  locations: LocationInfo[]
  typeInfo: TypeMap
}

const props = defineProps<Props>()

const emit = defineEmits<{
  browse: [memberName: string, idx: number]
  delete: [obj: TreeNode]
  save: [objID: string, objType: string, members: SaveMember[]]
  add: [info: AddObjectParams]
  move: [objID: string, moveUp: boolean]
  export: [objID: string]
  import: [objID: string]
  updateMember: [memberIdx: number, newValue: any]
}>()

// Refs
const confirm = ref<InstanceType<typeof Confirm>>()
const editTextInput = ref<HTMLElement>()
const txtID = ref<HTMLElement>()
const txtName = ref<HTMLElement>()
const txtSearch = ref<HTMLElement>()

// Reactive data
const currentTab = ref('')
const addDialog = ref({
  show: false,
  memberName: '',
  typeNameShort: '',
  typeNameFull: '',
  newID: '',
  newName: '',
})
const browseDialog = ref({
  show: false,
  memberName: '',
  memberIdx: 0,
  selection: [] as any[],
})
const search = ref('')
const editVarDialog = ref(false)
const editVarName = ref('')
const editVarTmp = ref('')

// Constants
const varHeaders = [
  { title: 'Variable', align: 'start', sortable: false, key: 'Name' },
  { title: 'Value', align: 'end', sortable: false, key: 'V' },
  { title: 'Quality', align: 'end', sortable: false, key: 'Q' },
  { title: 'Timestamp', align: 'end', sortable: false, key: 'T' },
] as const

const browseFooter = {
  showFirstLastPage: true,
  itemsPerPageOptions: [100, 500, 1000, { text: 'All', value: -1 }],
}

const browseHeaders = [{ title: 'Value', align: 'start', sortable: true, key: 'it' }] as const

// Computed properties
const categoryMembers = computed((): CategoryMember[] => {
  const categories = []
  const members = props.members
  for (const member of members) {
    const cat = member.Category
    const idx = categories.findIndex((c) => c === cat)
    if (idx < 0) {
      categories.push(cat)
    }
  }
  return categories.map((c) => {
    return {
      Category: c === '' ? 'Members' : c,
      Members: members.filter((m) => m.Category === c),
    }
  })
})

const isExportable = computed((): boolean => {
  return (
    props.selection !== null &&
    props.selection.Type !== undefined &&
    props.selection.Type !== null &&
    props.typeInfo[props.selection.Type].IsExportable
  )
})

const isImportable = computed((): boolean => {
  return (
    props.selection !== null &&
    props.selection.Type !== undefined &&
    props.selection.Type !== null &&
    props.typeInfo[props.selection.Type].IsImportable
  )
})

const isDirty = computed(() => {
  return -1 !== props.members.findIndex((m) => JSON.stringify(m.ValueOriginal) !== JSON.stringify(m.Value))
})

const title = computed(() => {
  if (props.selection === null || props.selection.Name === undefined) {
    return ''
  }
  return getShortTypeName(props.selection.Type) + ': ' + props.selection.Name
})

const browseValues = computed(() => {
  if (browseDialog.value.memberIdx < props.members.length) {
    return props.members[browseDialog.value.memberIdx].BrowseValues
  }
  return []
})

const browseValuesLoading = computed((): boolean => {
  if (browseDialog.value.memberIdx < props.members.length) {
    return props.members[browseDialog.value.memberIdx].BrowseValuesLoading
  }
  return false
})

const filteredBrowseValuesObj = computed((): BrowseItem[] => {
  const items: string[] = filteredBrowseValues.value
  return items.map((it) => ({ it }))
})

const filteredBrowseValues = computed(() => {
  const items = browseValues.value
  const s = search.value.toString().toLowerCase()
  if (s.trim() === '') {
    return items
  }
  const words = s.split(' ').filter((w) => w !== '')
  const isFilterMatch = (val: string) => {
    const valLower = val.toLowerCase()
    return words.every((word) => valLower.indexOf(word) !== -1)
  }
  return items.filter((item) => isFilterMatch(item))
})

// Watchers
watch(categoryMembers, (v, oldV) => {
  const tab = currentTab.value
  if (tab === 'Variables') {
    return
  }
  const categories = categoryMembers.value
  if (categories.findIndex((cat) => cat.Category === tab) >= 0) {
    return
  }
  currentTab.value = categories.length === 0 ? '' : categories[0].Category
})

// Methods
const classObject = (tab: string) => {
  const sel = currentTab.value === tab
  return {
    selectedtab: sel,
    nonselectedtab: !sel,
  }
}

const loadBrowseable = (memberName: string, memberValue: any) => {
  const idx = props.members.findIndex((m) => m.Name === memberName)
  emit('browse', memberName, idx)
  browseDialog.value.memberName = memberName
  browseDialog.value.memberIdx = idx
  browseDialog.value.selection = []
  if (browseDialog.value.memberIdx < props.members.length) {
    const value = props.members[browseDialog.value.memberIdx].Value
    if (value !== undefined && value !== null && value !== '') {
      browseDialog.value.selection = [{ it: value }]
    }
  }
  search.value = ''
  browseDialog.value.show = true
  nextTick(() => {
    txtSearch.value?.focus()
  })
}

const browseOK = () => {
  if (browseDialog.value.selection.length > 0) {
    emit('updateMember', browseDialog.value.memberIdx, browseDialog.value.selection[0].it)
  }
  browseDialog.value.show = false
}

const browseKeydown = (e: KeyboardEvent) => {
  if (e.keyCode === 27) {
    browseDialog.value.show = false
  } else if (e.keyCode === 13) {
    browseOK()
  }
}

const editVar = (item: any) => {
  editVarName.value = item.Name
  editVarTmp.value = item.V
  editVarDialog.value = true
  nextTick(() => {
    editTextInput.value?.focus()
  })
}

const editVarWrite = () => {
  editVarDialog.value = false
  writeVariable(props.selection!.ID, editVarName.value, editVarTmp.value)
}

const editVarKeydown = (e: KeyboardEvent) => {
  if (e.keyCode === 27) {
    editVarDialog.value = false
  } else if (e.keyCode === 13) {
    editVarWrite()
  }
}

const validateVariableValue = (v: string) => {
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

const writeVariable = (objID: string, varName: string, newVal: string) => {
  try {
    JSON.parse(newVal)
    const params = { ObjID: objID, Var: varName, V: newVal }
    ;(window.parent as any)['dashboardApp'].sendViewRequest('WriteVariable', params, (strResponse: string) => {})
  } catch (error) {
    console.log('Bad JSON: ' + error)
  }
}

const getShortTypeName = (fullTypeName: string) => {
  if (fullTypeName === undefined) {
    return ''
  }
  const i = fullTypeName.lastIndexOf('.')
  if (i > 0) {
    return fullTypeName.substring(i + 1)
  }
  return fullTypeName
}

const deleteObject = async () => {
  const obj: TreeNode = props.selection!
  const name = getShortTypeName(obj.Type) + ' ' + obj.Name
  if (await confirm.value!.open('Confirm Delete', 'Do you want to delete ' + name + '?', { color: 'red' })) {
    emit('delete', obj)
  }
}

const qualityColor = (q: string) => {
  if (q === 'Good') {
    return 'green'
  }
  if (q === 'Uncertain') {
    return 'orange'
  }
  return 'red'
}

const save = () => {
  const msChanged = props.members.filter((m) => JSON.stringify(m.ValueOriginal) !== JSON.stringify(m.Value))
  const mem: SaveMember[] = msChanged.map((m) => {
    console.log('Orig: ' + JSON.stringify(m.ValueOriginal))
    console.log('Upda: ' + JSON.stringify(m.Value))
    return {
      Name: m.Name,
      Value: JSON.stringify(m.Value),
    }
  })
  emit('save', props.selection!.ID, props.selection!.Type, mem)
}

const showAddChildDialog = (t: ChildType) => {
  if (t.Members.length > 1) {
    alert('Multiple members for type ' + t.TypeName)
    return
  }
  ;(window.parent as any)['dashboardApp'].sendViewRequest('GetNewID', JSON.stringify(t.TypeName), (strResponse: string) => {
    const theID = JSON.parse(strResponse)
    addDialog.value.newID = theID
  })
  addDialog.value.memberName = t.Members[0]
  addDialog.value.typeNameShort = getShortTypeName(t.TypeName)
  addDialog.value.typeNameFull = t.TypeName
  addDialog.value.newID = ''
  addDialog.value.newName = ''
  addDialog.value.show = true
  nextTick(() => {
    txtName.value?.focus()
  })
}

const editKeydown = (e: KeyboardEvent) => {
  if (e.keyCode === 27) {
    addDialog.value.show = false
  }
}

const onAddNewObject = () => {
  if (!isValidObjectNameOrID(addDialog.value.newID)) {
    alert('ID must not be empty and must not start or end with whitespace.')
    return
  }
  if (!isValidObjectNameOrID(addDialog.value.newName)) {
    alert('Name must not be empty and must not start or end with whitespace.')
    return
  }
  addDialog.value.show = false
  const info: AddObjectParams = {
    ParentObjID: props.selection!.ID,
    ParentMember: addDialog.value.memberName,
    NewObjID: addDialog.value.newID,
    NewObjType: addDialog.value.typeNameFull,
    NewObjName: addDialog.value.newName,
  }
  emit('add', info)
}

const removeOptional = (member: ObjectMember) => {
  member.Value = null
}

const setOptional = (member: ObjectMember) => {
  member.Value = JSON.parse(member.DefaultValue)
}

const asArray = (value: unknown): any[] => {
  return Array.isArray(value) ? value : []
}

const removeArrayItem = (array: any[], idx: number) => {
  array.splice(idx, 1)
}

const moveUpArrayItem = (array: any[], idx: number) => {
  if (idx > 0) {
    const item = array[idx]
    array.splice(idx, 1)
    array.splice(idx - 1, 0, item)
  }
}

const addArrayItem = (member: ObjectMember) => {
  member.Value.push(JSON.parse(member.DefaultValue))
}

const moveUp = () => {
  emit('move', props.selection!.ID, true)
}

const moveDown = () => {
  emit('move', props.selection!.ID, false)
}

const doExport = () => {
  emit('export', props.selection!.ID)
}

const doImport = () => {
  emit('import', props.selection!.ID)
}
</script>

<style scoped>
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
  font-weight: 500;
  line-height: 1.6;
  letter-spacing: 0.0125em;
}

.v-toolbar__content > .v-toolbar-title {
  margin-inline-start: 12px;
}
</style>
