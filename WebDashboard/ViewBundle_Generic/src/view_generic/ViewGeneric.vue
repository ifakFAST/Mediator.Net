<template>
 <v-app>
  <v-main>

    <v-container fluid>
      <v-row  >
        <v-col cols="auto" style="min-width: 400px; max-width: 660px;">
          <ul style="padding-left:0px;">

            <object-tree
                :model="objectTree"
                :selection-id="selectedObjectID"
                :initial-open="true"
                :type-info="typeInfo"
                @selectObject="onObjectSelected"
                @dragDrop="onDragDrop"></object-tree>

          </ul>
        </v-col>
        <v-col cols="fill" style="max-width: 850px;">

          <object-editor @save="onSave" @delete="onDelete" @add="onAddObject" 
              @move="moveUpOrDown" @browse="onBrowse" @export="onExport" @import="onImport"
              :selection="selectedObject"
              :members="currObjectValues"
              :child-types="childTypes"
              :type-info="typeInfo"
              :locations="locations"></object-editor>

        </v-col>
      </v-row>
    </v-container>

  </v-main>
</v-app>

</template>

<script lang="ts">

import { Component, Vue, Watch } from 'vue-property-decorator'

import ObjectTree from './ObjectTree.vue'
import ObjectEditor from './ObjectEditor.vue'
import { TreeNode, TypeMap, ObjMemInfo, ObjectMember, ChildType, ObjectMap, AddObjectParams, SaveMember } from './types'
import { LocationInfo } from '../fast_types'

@Component({
  components: {
    ObjectTree,
    ObjectEditor,
  },
})
export default class ViewGeneric extends Vue {

  objectTree: TreeNode = null
  selectedObjectID = ''
  currObjectValues: ObjectMember[] = []
  childTypes: ChildType[] = []
  typeInfo: TypeMap = {}
  objectMap: ObjectMap = {}
  locations: LocationInfo[] = []

  get selectedObject(): TreeNode {
    if (this.selectedObjectID === '') { return null }
    return this.findObjectWithID(this.objectTree, this.selectedObjectID)
  }

  refreshAll() {
    const context = this
    window.parent['dashboardApp'].sendViewRequest('GetModel', {}, (strResponse) => {
      const response = JSON.parse(strResponse)
      context.updateObjects(response.ObjectTree)
      context.typeInfo = response.TypeInfo
      context.locations = response.Locations
      context.onObjectSelected(response.ObjectTree)
    })
  }

  onSave(objID: string, objType: string, changedMembers: SaveMember[]) {
    const context = this
    const para = {
      ID: objID,
      Type: objType,
      Members: changedMembers,
    }
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('Save', para, (strResponse) => {
      const res = JSON.parse(strResponse)
      context.currObjectValues = res.ObjectValues
      context.updateObjects(res.ObjectTree)
    })
  }

  onDelete(obj: TreeNode) {
    const context = this
    const nextSelection = this.findNextSelectObj(this.objectTree, obj)
    window.parent['dashboardApp'].sendViewRequest('Delete', JSON.stringify(obj.ID), (strResponse) => {
      context.updateObjects(JSON.parse(strResponse))
      context.onObjectSelected(nextSelection)
    })
  }

  onAddObject(info: AddObjectParams) {
    const context = this
    window.parent['dashboardApp'].sendViewRequest('AddObject', info, (strResponse) => {
      const res = JSON.parse(strResponse)
      context.updateObjects(res.Tree)
      context.onObjectSelected(context.findObjectWithID(context.objectTree, res.ObjectID))
    })
  }

  onBrowse(memberName: string, idx: number) {
    const context = this
    const info = {
      ObjID: this.selectedObjectID,
      Member: memberName,
    }
    context.currObjectValues[idx].BrowseValuesLoading = true
    context.currObjectValues[idx].BrowseValues = []
    window.parent['dashboardApp'].sendViewRequest('Browse', info, (strResponse) => {
      const res = JSON.parse(strResponse)
      context.currObjectValues[idx].BrowseValues = res
      context.currObjectValues[idx].BrowseValuesLoading = false
    })
  }

  onObjectSelected(selectObject: TreeNode) {
    if (selectObject === null) {
      console.error('onObjectSelected: selectObject == null')
      return
    }
    const context = this
    const parameter = { ID: selectObject.ID, Type: selectObject.Type }
    window.parent['dashboardApp'].sendViewRequest('GetObject', parameter, (strResponse) => {
      const response = JSON.parse(strResponse)
      context.currObjectValues = response.ObjectValues
      context.childTypes = response.ChildTypes
    })
    this.selectedObjectID = selectObject.ID
  }

  findNextSelectObj(tree: TreeNode, objDelete: TreeNode): TreeNode {
    const parent = this.findObjectWithID(tree, objDelete.ParentID)
    if (parent === null) { return null }
    const n = parent.Children.length
    const i = parent.Children.findIndex((ch) => ch.ID === objDelete.ID)
    if (i < 0) { return parent }
    if (i + 1 < n)  { return parent.Children[i + 1] }
    if (i - 1 >= 0) { return parent.Children[i - 1] }
    return parent
  }

  findObjectWithID(tree: TreeNode, id: string): TreeNode {
    if (tree === null || tree.ID === id) {
      return tree
    }
    for (const child of tree.Children) {
      const res = this.findObjectWithID(child, id)
      if (res !== null) { return res }
    }
    return null
  }

  onDragDrop(fromID, toID, toArrayName) {
    const info = {
      FromID: fromID,
      ToID: toID,
      ToArray: toArrayName,
    }
    const context = this
    window.parent['dashboardApp'].sendViewRequest('DragDrop', info, (strResponse) => {
      const res = JSON.parse(strResponse)
      context.updateObjects(res)
      context.onObjectSelected(context.findObjectWithID(context.objectTree, fromID))
    })
  }

  updateObjects(objTree: TreeNode) {
    this.objectTree = objTree
    const map = {}
    this.addTreeNodeToMap(objTree, map)
    this.objectMap = map
  }

  addTreeNodeToMap(tree: TreeNode, map: object) {
    if (tree === null) { return }
    map[tree.ID] = tree
    for (const child of tree.Children) {
      this.addTreeNodeToMap(child, map)
    }
  }

  moveUpOrDown(id, up) {
    const context = this
    const info = {
      ObjID: id,
      Up: up,
    }
    window.parent['dashboardApp'].sendViewRequest('MoveObject', info, (strResponse) => {
      const res = JSON.parse(strResponse)
      context.updateObjects(res)
      context.onObjectSelected(context.findObjectWithID(context.objectTree, id))
    })
  }

  async onExport(id: string): Promise<void> {
    const info = {
      ObjID: id,
    }
    const blobResponse: Blob = await window.parent['dashboardApp'].sendViewRequestAsync('Export', info, 'blob')
    this.downloadBlob(blobResponse, 'Export.xlsx')
  }

  downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename || 'download'
    a.click()
    setTimeout(() => {
      URL.revokeObjectURL(url)
    }, 500)
  }

  onImport(id: string): void {
    const inputElement = document.createElement('input')
    inputElement.type = 'file'
    inputElement.accept = '.xlsx'
    inputElement.onchange = () => {
      const curFiles = inputElement.files
      if (curFiles.length === 0) {
        return;
      }
      const file = curFiles[0]
      const reader = new FileReader()
      reader.onload = () => {
        const arrayBuffer = reader.result as ArrayBuffer
        const byteArray = new Uint8Array(arrayBuffer)
        const info = {
          ObjID: id,
          Data: Array.from(byteArray),
        }
        window.parent['dashboardApp'].sendViewRequest('Import', info, (strResponse) => {
          this.refreshAll()
        })
      }
      reader.readAsArrayBuffer(file)
    }
    inputElement.click()
  }

  mounted() {
    this.refreshAll()
    const context = this
    window.parent['dashboardApp'].registerViewEventListener((eventName, eventPayload) => {
      if (eventName === 'VarChange') {
        context.onEvent_VarChange(eventPayload)
      }
    })
  }

  onEvent_VarChange(entries) {
    const objectMap = this.objectMap
    for (const entry of entries) {
      const obj = objectMap[entry.ObjectID]
      if (obj === undefined) { return }
      for (const variable of obj.Variables) {
        if (variable.Name === entry.VarName) {
          variable.V = entry.V
          variable.T = entry.T
          variable.Q = entry.Q
        }
      }
    }
  }

}

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
    color: black!important;
    margin: 12px!important;
    text-transform: uppercase;
  }

  .nonselectedtab {
    font-weight: normal;
    color: grey!important;
    margin: 12px!important;
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

  .input-group.input-group--selection-controls label { /* used for v-checkbox */
    max-width: 100%;
  }

  table.v-table thead th {
    font-size: 16px;
    font-weight: bold;
  }

  table.v-table tbody td {
    font-size: 16px;
  }

  /*td {
      border: 1px solid rgb(20, 18, 18);
  }*/

  ul {
    padding-left: 1em;
    line-height: 1.5em;
    list-style-type: none;
    font-size: 18px;
  }

</style>
