<template>
 <v-app>
  <v-content>

    <v-container fluid>
      <v-row>
        <v-col cols="3" style="min-width: 250px;">

          <v-treeview activatable return-object dense
            :items="treeItems"
            :active="treeActiveItems"
            :open.sync="treeOpenItems"
            @update:active="onActiveTreeItemsChanged">

            <template v-slot:prepend="{ item, open }">
              <v-icon v-if="item.objectType === 'Folder'">
                {{ open ? 'mdi-folder-open' : 'mdi-folder' }}
              </v-icon>
              <v-icon v-if="item.objectType === 'Signal'">
                {{ 'mdi-square-medium' }}
              </v-icon>
              <v-icon v-if="item.objectType === 'Calculation'">
                {{ 'mdi-file-cog-outline' }}

              </v-icon>
            </template>

          </v-treeview>

        </v-col>
        <v-col cols="9">

          <v-toolbar dense v-if="editObject !== null">
            <v-toolbar-title>{{ objectTitle }}</v-toolbar-title>
            <v-spacer></v-spacer>
            <v-btn text @click="saveObject" :disabled="!isObjectDirty">Save</v-btn>
            <v-btn v-if="isObjectDeletable" text @click="deleteObject">Delete</v-btn>

            <template v-if="isFolderObject">
              <v-btn text @click="addFolder">Add Folder</v-btn>
              <v-btn text @click="addSignal">Add Signal</v-btn>
              <v-btn text @click="addCalculation">Add Calculation</v-btn>
            </template>

            <v-btn style="min-width: 18px;" text @click="moveObject(true)"  :disabled="selectedItem === null || selectedItem.first"><v-icon>arrow_upward</v-icon></v-btn>
            <v-btn style="min-width: 18px;" text @click="moveObject(false)" :disabled="selectedItem === null || selectedItem.last"><v-icon>arrow_downward</v-icon></v-btn>

          </v-toolbar>

          <folder-editor      v-if="isFolderObject"      v-model="editObject"></folder-editor>
          <signal-editor      v-if="isSignalObject"      v-model="editObject"></signal-editor>
          <calculation-editor v-if="isCalculationObject" v-model="editObject" :variables="editObjectVariables" :adapterTypesInfo="adapterTypesInfo"></calculation-editor>

        </v-col>
      </v-row>
    </v-container>

    <v-dialog v-model="addDialog.show" persistent max-width="350px" @keydown="(e) => { if (e.keyCode === 27) { this.addDialog.show = false } else if (e.keyCode === 13) { this.onAddNewObject() } }">
      <v-card>
        <v-card-title>
          <span class="headline">Add new {{addDialog.typeName}}</span>
        </v-card-title>
        <v-card-text>
          <v-text-field v-model="addDialog.newID"   label="ID"   ref="txtID" hint="The unique and immutable identifier of the new object"></v-text-field>
          <v-text-field v-model="addDialog.newName" label="Name" ref="txtName"></v-text-field>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn color="grey    darken-1" text @click.native="addDialog.show = false">Cancel</v-btn>
          <v-btn color="primary darken-1" text @click.native="onAddNewObject">Add</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <confirm ref="confirm"></confirm>

  </v-content>
</v-app>
</template>

<script lang="ts">

import { Component, Vue, Watch } from 'vue-property-decorator'
import * as calcmodel from './model'
import * as global from './global'
import * as fast from '../fast_types'
import * as utils from '../utils'
import Confirm from '../components/Confirm.vue'
import { TreeItem, ObjType, model2TreeItems, SignalVariables, CalculationVariables } from './conversion'
import FolderEditor from './FolderEditor.vue'
import SignalEditor from './SignalEditor.vue'
import CalculationEditor from './CalculationEditor.vue'

export interface EventEntry {
  Key: string
  V: fast.DataValue
  T: fast.Timestamp
  Q: fast.Quality
}

@Component({
  components: {
    Confirm,
    FolderEditor,
    SignalEditor,
    CalculationEditor,
  },
})
export default class ViewCalc extends Vue {

  treeItems: TreeItem[] = []
  treeActiveItems: TreeItem[] = []
  treeOpenItems: TreeItem[] = []

  selectedItem: TreeItem | null = null

  editObjectOriginal: string = ''
  editObject: calcmodel.Folder | calcmodel.Signal | calcmodel.Calculation | null = null
  editObjectType: ObjType = 'Folder'
  editObjectVariables: SignalVariables | CalculationVariables | null = null

  addDialog = {
    show: false,
    typeName: '',
    parentMemberName: '',
    newID: '',
    newName: '',
  }

  adapterTypesInfo: global.AdapterInfo[] = []

  mapVariables = new Map<string, fast.VTQ>()

  mounted() {
    const context = this
    window.parent['dashboardApp'].sendViewRequest('GetModel', {}, (strResponse) => {
      context.initModel(strResponse)
      context.treeOpenItems = [context.treeItems[0]]
    })

    window.parent['dashboardApp'].registerViewEventListener( (eventName: string, eventPayload: EventEntry[]) => {
      if (eventName === 'VarChange') {
        this.processEventEntries(eventPayload)
      }
    })
  }

  processEventEntries(entries: EventEntry[]): void {
    const len = entries.length
    const map = this.mapVariables
    for (let i = 0; i < len; i++) {
        const entry: EventEntry = entries[i]
        const vtq: fast.VTQ = map.get(entry.Key)
        if (vtq !== undefined) {
          vtq.V = entry.V
          vtq.T = entry.T
          vtq.Q = entry.Q
        }
    }
  }

  onActiveTreeItemsChanged([activeItem]: TreeItem[]): void {
    if (activeItem === undefined || activeItem === null) {
      console.info('onActiveTreeItemsChanged (null || undefined)')
      this.editObject = null
      this.treeActiveItems = []
      this.selectedItem = null
      return
    }
    console.info('onActiveTreeItemsChanged: ' + activeItem.name)
    const str = JSON.stringify(activeItem.object)
    this.editObjectOriginal = str
    this.editObject = JSON.parse(str)
    this.editObjectType = activeItem.objectType
    this.editObjectVariables = activeItem.objectVariables
    this.treeActiveItems = [activeItem]
    const parent = this.findTreeItem(this.treeItems[0], activeItem.parentID)
    if (parent !== null && this.treeOpenItems.find((it) => it === parent) === undefined) {
      this.treeOpenItems.push(parent)
    }
    this.selectedItem = activeItem
  }

  get isFolderObject(): boolean {
    return this.editObject !== null && this.editObjectType === 'Folder'
  }

  get isSignalObject(): boolean {
    return this.editObject !== null && this.editObjectType === 'Signal'
  }

  get isCalculationObject(): boolean {
    return this.editObject !== null && this.editObjectType === 'Calculation'
  }

  get objectTitle(): string {
    if (this.editObject === null || this.editObject.Name === undefined) { return '' }
    return this.editObjectType + ': ' + this.editObject.Name
  }

  saveObject(): void {
    const context = this
    const id = this.editObject.ID
    const para = {
      ID: id,
      Obj: this.editObject,
    }
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('Save', para, (strResponse) => {
      context.initModel(strResponse, id)
    })
  }

  moveObject(up: boolean): void {
    const context = this
    const id = this.editObject.ID
    const info = {
      ObjID: id,
      Up: up,
    }
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('MoveObject', info, (strResponse) => {
      context.initModel(strResponse, id)
    })
  }

  async deleteObject() {
    const confirm = this.$refs.confirm as any
    const name = this.objectTitle
    if (await confirm.open('Confirm Delete', 'Do you want to delete ' + name + '?', { color: 'red' })) {
      const context = this
      const id = this.editObject.ID
      const tree = this.treeItems[0]
      const treeItemDelete = this.findTreeItem(tree, id)
      const nextSelect = this.findNextSelectObj(tree, treeItemDelete)
      const nextSelectID = nextSelect === null ? '' : nextSelect.id
      const dashboard = window.parent['dashboardApp']
      dashboard.sendViewRequest('Delete', JSON.stringify(id), (strResponse) => {
        context.initModel(strResponse, nextSelectID)
      })
    }
  }

  addFolder(): void {
    this.prepareAddObject('Folder', 'Folders')
  }

  addSignal(): void {
    this.prepareAddObject('Signal', 'Signals')
  }

  addCalculation(): void {
    this.prepareAddObject('Calculation', 'Calculations')
  }

  prepareAddObject(typeName: string, member: string): void {
    this.addDialog.typeName = typeName
    this.addDialog.parentMemberName = member
    this.addDialog.newID = utils.findUniqueID(typeName, 6, this.getAllIDs())
    this.addDialog.newName = ''
    this.addDialog.show = true
    const context = this
    const doFocus = () => {
      const txtName = context.$refs.txtName as HTMLElement
      txtName.focus()
    }
    setTimeout(doFocus, 100)
  }

  onAddNewObject() {
    this.addDialog.show = false
    const info = {
      ParentObjID:  this.editObject.ID,
      ParentMember: this.addDialog.parentMemberName,
      NewObjID:     this.addDialog.newID,
      NewObjType:   this.addDialog.typeName,
      NewObjName:   this.addDialog.newName,
    }
    const context = this
    const newID = this.addDialog.newID
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('AddObject', info, (strResponse) => {
      context.initModel(strResponse, newID)
    })
  }

  initModel(strResponse: string, activeItemID?: string): void {
    const res = JSON.parse(strResponse)
    const model: calcmodel.CalcModel = res.model
    const objectInfos: global.ObjInfo[] = res.objectInfos
    const moduleInfos: global.ModuleInfo[] = res.moduleInfos
    const varValues: EventEntry[] = res.variableValues
    this.adapterTypesInfo = res.adapterTypesInfo

    global.mapObjects.clear()
    objectInfos.forEach((obj) => {
      global.mapObjects.set(obj.ID, obj)
    })

    global.modules.length = 0
    moduleInfos.forEach((module) => {
      global.modules.push(module)
    })

    this.mapVariables.clear()
    const treeRoot = model2TreeItems(model, this.mapVariables)
    this.treeItems = [treeRoot]

    this.processEventEntries(varValues)

    if (activeItemID === undefined) {
      this.onActiveTreeItemsChanged([ treeRoot ])
    }
    else {
      this.onActiveTreeItemsChanged([ this.findTreeItem(treeRoot, activeItemID) ])
    }
  }

  findNextSelectObj(tree: TreeItem, objDelete: TreeItem | null): TreeItem | null {
    if (objDelete === null || objDelete.parentID === null) { return null }
    const parent = this.findTreeItem(tree, objDelete.parentID)
    if (parent === null) { return null }
    const n = parent.children.length
    const i = parent.children.findIndex((ch) => ch.id === objDelete.id)
    if (i < 0) { return parent }
    if (i + 1 < n)  { return parent.children[i + 1] }
    if (i - 1 >= 0) { return parent.children[i - 1] }
    return parent
  }

  get isObjectDirty(): boolean {
    const obj = this.editObject
    if (obj === null) { return false }
    return this.editObjectOriginal !== JSON.stringify(obj)
  }

  get isObjectDeletable(): boolean {
    if (this.editObject === null || this.treeActiveItems.length !== 1) { return false }
    const treeItem = this.treeActiveItems[0]
    return treeItem.parentID !== null
  }

  findTreeItem(root: TreeItem, id: string): TreeItem | null {
    if (root.id === id) { return root }
    for (const child of root.children) {
      const hit: TreeItem | null = this.findTreeItem(child, id)
      if (hit !== null) { return hit }
    }
    return null
  }

  getAllIDs(): Set<string> {
    const set = new Set<string>()
    this.getAllIDsFromTree(this.treeItems[0], set)
    return set
  }

  getAllIDsFromTree(root: TreeItem, resSet: Set<string>): void {
    resSet.add(root.id)
    for (const child of root.children) {
      this.getAllIDsFromTree(child, resSet)
    }
  }

}

</script>

<style>

</style>