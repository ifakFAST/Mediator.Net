<template>
  <v-container
    fluid
    class="pa-4 pl-2"
  >
    <v-row>
      <v-col
        cols="auto"
        style="min-width: 400px; max-width: 660px"
      >
        <ul style="padding-left: 0px">
          <object-tree
            v-if="objectTree"
            :initial-open="true"
            :model="objectTree"
            :selection-id="selectedObjectID"
            :type-info="typeInfo"
            @drag-drop="onDragDrop"
            @select-object="onObjectSelected"
          />
        </ul>
      </v-col>
      <v-col
        cols="fill"
        style="max-width: 850px"
      >
        <object-editor
          :child-types="childTypes"
          :locations="locations"
          :members="currObjectValues"
          :selection="selectedObject"
          :type-info="typeInfo"
          @add="onAddObject"
          @browse="onBrowse"
          @delete="onDelete"
          @export="onExport"
          @import="onImport"
          @move="moveUpOrDown"
          @save="onSave"
          @update-member="onUpdateMember"
        />
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import ObjectTree from './ObjectTree.vue'
import ObjectEditor from './ObjectEditor.vue'
import type { TreeNode, TypeMap, ObjectMember, ChildType, ObjectMap, AddObjectParams, SaveMember } from './types'
import type { LocationInfo } from '../fast_types'

const objectTree = ref<TreeNode | null>(null)
const selectedObjectID = ref('')
const currObjectValues = ref<ObjectMember[]>([])
const childTypes = ref<ChildType[]>([])
const typeInfo = ref<TypeMap>({})
const objectMap = ref<ObjectMap>({})
const locations = ref<LocationInfo[]>([])

const selectedObject = computed((): TreeNode | null => {
  if (selectedObjectID.value === '') {
    return null
  }
  return findObjectWithID(objectTree.value, selectedObjectID.value)
})

const refreshAll = () => {
  ;(window.parent as any)['dashboardApp'].sendViewRequest('GetModel', {}, (strResponse: string) => {
    const response = JSON.parse(strResponse)
    updateObjects(response.ObjectTree)
    typeInfo.value = response.TypeInfo
    locations.value = response.Locations
    onObjectSelected(response.ObjectTree)
  })
}

const onSave = (objID: string, objType: string, changedMembers: SaveMember[]) => {
  const para = {
    ID: objID,
    Type: objType,
    Members: changedMembers,
  }
  const dashboard = (window.parent as any)['dashboardApp']
  dashboard.sendViewRequest('Save', para, (strResponse: string) => {
    const res = JSON.parse(strResponse)
    currObjectValues.value = res.ObjectValues
    updateObjects(res.ObjectTree)
  })
}

const onDelete = (obj: TreeNode) => {
  const nextSelection = findNextSelectObj(objectTree.value, obj)
  ;(window.parent as any)['dashboardApp'].sendViewRequest('Delete', JSON.stringify(obj.ID), (strResponse: string) => {
    updateObjects(JSON.parse(strResponse))
    onObjectSelected(nextSelection)
  })
}

const onAddObject = (info: AddObjectParams) => {
  ;(window.parent as any)['dashboardApp'].sendViewRequest('AddObject', info, (strResponse: string) => {
    const res = JSON.parse(strResponse)
    updateObjects(res.Tree)
    onObjectSelected(findObjectWithID(objectTree.value, res.ObjectID))
  })
}

const onBrowse = (memberName: string, idx: number) => {
  const info = {
    ObjID: selectedObjectID.value,
    Member: memberName,
  }
  currObjectValues.value[idx].BrowseValuesLoading = true
  currObjectValues.value[idx].BrowseValues = []
  ;(window.parent as any)['dashboardApp'].sendViewRequest('Browse', info, (strResponse: string) => {
    const res = JSON.parse(strResponse)
    currObjectValues.value[idx].BrowseValues = res
    currObjectValues.value[idx].BrowseValuesLoading = false
  })
}

const onUpdateMember = (memberIdx: number, newValue: any) => {
  if (memberIdx >= 0 && memberIdx < currObjectValues.value.length) {
    currObjectValues.value[memberIdx].Value = newValue
  }
}

const onObjectSelected = (selectObject: TreeNode | null) => {
  if (selectObject === null) {
    console.error('onObjectSelected: selectObject == null')
    return
  }
  const parameter = { ID: selectObject.ID, Type: selectObject.Type }
  ;(window.parent as any)['dashboardApp'].sendViewRequest('GetObject', parameter, (strResponse: string) => {
    const response = JSON.parse(strResponse)
    currObjectValues.value = response.ObjectValues
    childTypes.value = response.ChildTypes
  })
  selectedObjectID.value = selectObject.ID
}

const findNextSelectObj = (tree: TreeNode | null, objDelete: TreeNode): TreeNode | null => {
  const parent = findObjectWithID(tree, objDelete.ParentID)
  if (parent === null) {
    return null
  }
  const n = parent.Children.length
  const i = parent.Children.findIndex((ch) => ch.ID === objDelete.ID)
  if (i < 0) {
    return parent
  }
  if (i + 1 < n) {
    return parent.Children[i + 1]
  }
  if (i - 1 >= 0) {
    return parent.Children[i - 1]
  }
  return parent
}

const findObjectWithID = (tree: TreeNode | null, id: string): TreeNode | null => {
  if (tree === null || tree.ID === id) {
    return tree
  }
  for (const child of tree.Children) {
    const res = findObjectWithID(child, id)
    if (res !== null) {
      return res
    }
  }
  return null
}

const onDragDrop = (fromID: string, toID: string, toArrayName: string) => {
  const info = {
    FromID: fromID,
    ToID: toID,
    ToArray: toArrayName,
  }
  ;(window.parent as any)['dashboardApp'].sendViewRequest('DragDrop', info, (strResponse: string) => {
    const res = JSON.parse(strResponse)
    updateObjects(res)
    onObjectSelected(findObjectWithID(objectTree.value, fromID))
  })
}

const updateObjects = (objTree: TreeNode) => {
  objectTree.value = objTree
  const map = {}
  addTreeNodeToMap(objTree, map)
  objectMap.value = map
}

const addTreeNodeToMap = (tree: TreeNode | null, map: any) => {
  if (tree === null) {
    return
  }
  map[tree.ID] = tree
  for (const child of tree.Children) {
    addTreeNodeToMap(child, map)
  }
}

const moveUpOrDown = (id: string, up: boolean) => {
  const info = {
    ObjID: id,
    Up: up,
  }
  ;(window.parent as any)['dashboardApp'].sendViewRequest('MoveObject', info, (strResponse: string) => {
    const res = JSON.parse(strResponse)
    updateObjects(res)
    onObjectSelected(findObjectWithID(objectTree.value, id))
  })
}

const onExport = async (id: string): Promise<void> => {
  const info = {
    ObjID: id,
  }
  const blobResponse: Blob = await (window.parent as any)['dashboardApp'].sendViewRequestAsync('Export', info, 'blob')
  downloadBlob(blobResponse, 'Export.xlsx')
}

const downloadBlob = (blob: Blob, filename: string): void => {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename || 'download'
  a.click()
  setTimeout(() => {
    URL.revokeObjectURL(url)
  }, 500)
}

const onImport = (id: string): void => {
  const inputElement = document.createElement('input')
  inputElement.type = 'file'
  inputElement.accept = '.xlsx'
  inputElement.onchange = () => {
    const curFiles = inputElement.files
    if (curFiles?.length === 0) {
      return
    }
    const file = curFiles![0]
    const reader = new FileReader()
    reader.onload = () => {
      const arrayBuffer = reader.result as ArrayBuffer
      const byteArray = new Uint8Array(arrayBuffer)
      const info = {
        ObjID: id,
        Data: Array.from(byteArray),
      }
      ;(window.parent as any)['dashboardApp'].sendViewRequest('Import', info, () => {
        refreshAll()
      })
    }
    reader.readAsArrayBuffer(file)
  }
  inputElement.click()
}

const onEvent_VarChange = (entries: any[]) => {
  const objMap = objectMap.value
  for (const entry of entries) {
    const obj = objMap[entry.ObjectID]
    if (obj === undefined) {
      return
    }
    for (const variable of obj.Variables) {
      if (variable.Name === entry.VarName) {
        variable.V = entry.V
        variable.T = entry.T
        variable.Q = entry.Q
      }
    }
  }
}

onMounted(() => {
  refreshAll()
  ;(window.parent as any)['dashboardApp'].registerViewEventListener((eventName: string, eventPayload: any) => {
    if (eventName === 'VarChange') {
      onEvent_VarChange(eventPayload)
    }
  })
})
</script>

<style>
.item {
  cursor: pointer;
}

.bold {
  font-size: 18px;
  font-weight: bold;
  margin-bottom: 0px;
}

.selectedtab {
  font-weight: bold;
  color: black !important;
  margin: 12px !important;
  text-transform: uppercase;
}

.nonselectedtab {
  font-weight: normal;
  color: grey !important;
  margin: 12px !important;
  text-transform: uppercase;
}

.struct {
  border: 1px solid grey;
  margin: 8px;
  background-color: rgb(228, 230, 231);
}

.array {
  border: 1px solid grey;
  background-color: rgb(228, 230, 231);
}

.small {
  min-width: 42px;
  width: 42px;
}

.input-group.input-group--selection-controls label {
  max-width: 100%;
}

table.v-table thead th {
  font-size: 16px;
  font-weight: bold;
}

table.v-table tbody td {
  font-size: 16px;
}

ul {
  padding-left: 1em;
  line-height: 1.5em;
  list-style-type: none;
  font-size: 18px;
}
</style>
