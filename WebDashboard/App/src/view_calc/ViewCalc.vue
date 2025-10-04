<template>
  <v-container
    fluid
    class="pa-4"
  >
    <v-row>
      <v-col
        cols="auto"
        style="min-width: 260px"
      >
        <TreeView
          v-model:selected="selectedNode"
          :expanded="true"
          :icon-function="iconFunction"
          :root="treeRoot"
        />
      </v-col>
      <v-col cols="fill">
        <v-toolbar
          v-if="editObject !== null"
          :elevation="4"
          density="compact"
        >
          <v-toolbar-title>{{ objectTitle }}</v-toolbar-title>
          <v-spacer />
          <v-btn
            :disabled="!isObjectDirty"
            variant="text"
            @click="saveObject"
          >
            Save
          </v-btn>
          <v-btn
            v-if="isObjectDeletable"
            variant="text"
            @click="deleteObject"
          >
            Delete
          </v-btn>

          <template v-if="isFolderObject">
            <v-btn
              variant="text"
              @click="addFolder"
            >
              Add Folder
            </v-btn>
            <v-btn
              variant="text"
              @click="addSignal"
            >
              Add Signal
            </v-btn>
            <v-btn
              variant="text"
              @click="addCalculation"
            >
              Add Calculation
            </v-btn>
          </template>

          <v-btn
            :disabled="selectedItem === null || selectedItem.first"
            icon
            @click="moveObject(true)"
          >
            <v-icon>mdi-arrow-up</v-icon>
          </v-btn>
          <v-btn
            :disabled="selectedItem === null || selectedItem.last"
            icon
            @click="moveObject(false)"
          >
            <v-icon>mdi-arrow-down</v-icon>
          </v-btn>

          <v-menu
            location="bottom end"
            offset-y
          >
            <template #activator="{ props }">
              <v-btn
                icon
                v-bind="props"
              >
                <v-icon>mdi-dots-vertical</v-icon>
              </v-btn>
            </template>
            <v-list>
              <v-list-item @click="resetVariables">
                <v-list-item-title>Reset Variables</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-menu>
        </v-toolbar>

        <FolderEditor
          v-if="isFolderObject && editObject !== null"
          v-model="editObject as calcmodel.Folder"
        />
        <SignalEditor
          v-if="isSignalObject && editObject !== null"
          v-model="editObject as calcmodel.Signal"
        />
        <CalculationEditor
          v-if="isCalculationObject && editObject !== null && editObjectVariables !== null"
          v-model="editObject as calcmodel.Calculation"
          :adapter-types-info="adapterTypesInfo"
          :variables="editObjectVariables as CalculationVariables"
        />
      </v-col>
    </v-row>
  </v-container>

  <v-dialog
    v-model="addDialog.show"
    max-width="350px"
    persistent
    @keydown="onAddDialogKeydown"
  >
    <v-card>
      <v-card-title>
        <span class="text-h5">Add new {{ addDialog.typeName }}</span>
      </v-card-title>
      <v-card-text>
        <v-text-field
          v-model="addDialog.newID"
          hint="The unique and immutable identifier of the new object"
          label="ID"
        />
        <v-text-field
          v-model="addDialog.newName"
          autofocus
          label="Name"
        />
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn
          color="grey-darken-1"
          variant="text"
          @click="addDialog.show = false"
        >
          Cancel
        </v-btn>
        <v-btn
          color="primary-darken-1"
          variant="text"
          @click="onAddNewObject"
        >
          Add
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <Confirm ref="dlgConfirm" />
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import * as calcmodel from './model'
import * as global from './global'
import * as fast from '../fast_types'
import * as utils from '../utils'
import type { TreeItem, ObjType, SignalVariables, CalculationVariables } from './conversion'
import { model2TreeItems } from './conversion'
import FolderEditor from './FolderEditor.vue'
import SignalEditor from './SignalEditor.vue'
import CalculationEditor from './CalculationEditor.vue'
import type { Node } from '../components/TreeView.vue'

interface EventEntry {
  Key: string
  V: fast.DataValue
  T: fast.Timestamp
  Q: fast.Quality
}

const selectedNode = ref<Node | null>(null)
const treeRoot = ref<TreeItem | null>(null)

const selectedItem = ref<TreeItem | null>(null)

const editObjectOriginal = ref('')
const editObject = ref<calcmodel.Folder | calcmodel.Signal | calcmodel.Calculation | null>(null)
const editObjectType = ref<ObjType>('Folder')
const editObjectVariables = ref<SignalVariables | CalculationVariables | null>(null)

const dlgConfirm = ref(null)

const addDialog = ref({
  show: false,
  typeName: '',
  parentMemberName: '',
  newID: '',
  newName: '',
})

const adapterTypesInfo = ref<global.AdapterInfo[]>([])

const mapVariables = ref(new Map<string, fast.VTQ>())

const isFolderObject = computed((): boolean => {
  return editObject.value !== null && editObjectType.value === 'Folder'
})

const isSignalObject = computed((): boolean => {
  return editObject.value !== null && editObjectType.value === 'Signal'
})

const isCalculationObject = computed((): boolean => {
  return editObject.value !== null && editObjectType.value === 'Calculation'
})

const objectTitle = computed((): string => {
  if (editObject.value === null || editObject.value.Name === undefined) {
    return ''
  }
  const type = editObjectType.value
  const name = editObject.value.Name
  const id = editObject.value.ID
  const idAppend = id === name ? '' : ` (ID: ${id})`
  return `${type}: ${name}${idAppend}`
})

const isObjectDirty = computed((): boolean => {
  const obj = editObject.value
  if (obj === null) {
    return false
  }
  return editObjectOriginal.value !== JSON.stringify(obj)
})

const isObjectDeletable = computed((): boolean => {
  if (editObject.value === null || selectedItem.value === null) {
    return false
  }
  return selectedItem.value.parentID !== null
})

watch(selectedNode, (newNode: Node | null) => {
  if (newNode) {
    console.info(`selectedNode is ${newNode.title}`)
    const activeItem: TreeItem = newNode as unknown as TreeItem
    const str = JSON.stringify(activeItem.object)
    editObjectOriginal.value = str
    editObject.value = JSON.parse(str)
    editObjectType.value = activeItem.objectType
    editObjectVariables.value = activeItem.objectVariables
    //const parent = findTreeItem(treeItems.value[0], activeItem.parentID)
    //if (parent !== null && treeOpenItems.value.find((it) => it === parent) === undefined) {
    //  treeOpenItems.value.push(parent)
    //}
    selectedItem.value = activeItem
  } else {
    console.info('selectedNode is (null || undefined)')
    editObjectOriginal.value = ''
    editObject.value = null
    editObjectType.value = 'Folder'
    editObjectVariables.value = null
    selectedItem.value = null
  }
})

const saveObject = (): void => {
  const id = editObject.value?.ID
  if (!id) return
  const para = {
    ID: id,
    Obj: editObject.value,
  }
  // @ts-ignore
  const dashboard = window.parent['dashboardApp']
  dashboard.sendViewRequest('Save', para, (strResponse: string) => {
    initModel(strResponse, id)
  })
}

const moveObject = (up: boolean): void => {
  const id = editObject.value?.ID
  if (!id) return
  const info = {
    ObjID: id,
    Up: up,
  }
  // @ts-ignore
  const dashboard = window.parent['dashboardApp']
  dashboard.sendViewRequest('MoveObject', info, (strResponse: string) => {
    initModel(strResponse, id)
  })
}

const resetVariables = async (): Promise<void> => {
  const confirm = (window as any).$refs.confirm
  const name = objectTitle.value
  if (await confirm.open('Confirm Reset', `Do you want to reset all variables (including history) of ${name}?`, { color: 'red' })) {
    const id = editObject.value?.ID
    if (!id) return
    // @ts-ignore
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('ResetVariables', JSON.stringify(id), () => {})
  }
}

const deleteObject = async (): Promise<void> => {
  const confirm = dlgConfirm.value as any
  const name = objectTitle.value
  if (await confirm.open('Confirm Delete', `Do you want to delete ${name}?`, { color: 'red' })) {
    const id = editObject.value?.ID
    const tree = treeRoot.value
    if (!id || !tree) return
    const treeItemDelete = findTreeItem(tree, id)
    const nextSelect = findNextSelectObj(tree, treeItemDelete)
    const nextSelectID = nextSelect === null ? '' : nextSelect.id
    // @ts-ignore
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('Delete', JSON.stringify(id), (strResponse: string) => {
      initModel(strResponse, nextSelectID)
    })
  }
}

const addFolder = (): void => {
  prepareAddObject('Folder', 'Folders')
}

const addSignal = (): void => {
  prepareAddObject('Signal', 'Signals')
}

const addCalculation = (): void => {
  prepareAddObject('Calculation', 'Calculations')
}

const prepareAddObject = (typeName: string, member: string): void => {
  addDialog.value.typeName = typeName
  addDialog.value.parentMemberName = member
  addDialog.value.newID = utils.findUniqueID(typeName, 6, getAllIDs())
  addDialog.value.newName = ''
  addDialog.value.show = true
}

const onAddNewObject = (): void => {
  addDialog.value.show = false
  const info = {
    ParentObjID: editObject.value?.ID,
    ParentMember: addDialog.value.parentMemberName,
    NewObjID: addDialog.value.newID,
    NewObjType: addDialog.value.typeName,
    NewObjName: addDialog.value.newName,
  }
  const newID = addDialog.value.newID
  // @ts-ignore
  const dashboard = window.parent['dashboardApp']
  dashboard.sendViewRequest('AddObject', info, (strResponse: string) => {
    initModel(strResponse, newID)
  })
}

const onAddDialogKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    addDialog.value.show = false
  } else if (e.key === 'Enter') {
    onAddNewObject()
  }
}

const initModel = (strResponse: string, activeItemID?: string): void => {
  const res = JSON.parse(strResponse)
  const model: calcmodel.CalcModel = res.model
  const objectInfos: global.ObjInfo[] = res.objectInfos
  const moduleInfos: global.ModuleInfo[] = res.moduleInfos
  const varValues: EventEntry[] = res.variableValues
  adapterTypesInfo.value = res.adapterTypesInfo

  global.mapObjects.clear()
  objectInfos.forEach((obj) => {
    global.mapObjects.set(obj.ID, obj)
  })

  global.modules.length = 0
  moduleInfos.forEach((module) => {
    global.modules.push(module)
  })

  mapVariables.value.clear()
  const theTreeRoot: TreeItem = model2TreeItems(model, mapVariables.value)
  treeRoot.value = theTreeRoot

  processEventEntries(varValues)

  if (activeItemID) {
    selectedNode.value = findTreeItem(theTreeRoot, activeItemID)
  } else {
    selectedNode.value = theTreeRoot
  }
}

const processEventEntries = (entries: EventEntry[]): void => {
  const len = entries.length
  const map = mapVariables.value
  for (let i = 0; i < len; i++) {
    const entry: EventEntry = entries[i]
    const vtq: fast.VTQ | undefined = map.get(entry.Key)
    if (vtq !== undefined) {
      vtq.V = entry.V
      vtq.T = entry.T
      vtq.Q = entry.Q
    }
  }
}

const findTreeItem = (root: TreeItem, id: string | null): TreeItem | null => {
  if (root.id === id) {
    return root
  }
  for (const child of root.children) {
    const hit: TreeItem | null = findTreeItem(child, id)
    if (hit !== null) {
      return hit
    }
  }
  return null
}

const findNextSelectObj = (tree: TreeItem, objDelete: TreeItem | null): TreeItem | null => {
  if (objDelete === null || objDelete.parentID === null) {
    return null
  }
  const parent = findTreeItem(tree, objDelete.parentID)
  if (parent === null) {
    return null
  }
  const n = parent.children.length
  const i = parent.children.findIndex((ch) => ch.id === objDelete.id)
  if (i < 0) {
    return parent
  }
  if (i + 1 < n) {
    return parent.children[i + 1]
  }
  if (i - 1 >= 0) {
    return parent.children[i - 1]
  }
  return parent
}

const getAllIDs = (): Set<string> => {
  const set = new Set<string>()
  if (treeRoot.value) {
    getAllIDsFromTree(treeRoot.value, set)
  }
  return set
}

const getAllIDsFromTree = (root: TreeItem, resSet: Set<string>): void => {
  resSet.add(root.id)
  for (const child of root.children) {
    getAllIDsFromTree(child, resSet)
  }
}

onMounted(() => {
  // @ts-ignore
  const dashboard = window.parent['dashboardApp']
  dashboard.sendViewRequest('GetModel', {}, (strResponse: string) => {
    initModel(strResponse)
  })

  dashboard.registerViewEventListener((eventName: string, eventPayload: EventEntry[]) => {
    if (eventName === 'VarChange') {
      processEventEntries(eventPayload)
    }
  })
})

const iconFunction = (node: Node, isExpanded: boolean): string => {
  const item = node as unknown as TreeItem

  if (item.objectType === 'Folder') {
    return isExpanded ? 'mdi-folder-open' : 'mdi-folder'
  }

  if (item.objectType === 'Signal') {
    return 'mdi-square-medium'
  }

  if (item.objectType === 'Calculation') {
    return 'mdi-file-cog-outline'
  }

  return ''
}
</script>

<style></style>
