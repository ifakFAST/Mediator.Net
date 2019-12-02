<template>
  <v-app>
    <v-content>
      <v-container>

        <v-toolbar flat dense color="white" style="margin-top: 0px; margin-bottom: 14px;">
          <a v-bind:class="classObject(tab.Name)"
              v-for="tab in tabs" :key="tab.Name"
              @click="currentTab = tab">{{tab.Name}}</a>
          <v-spacer></v-spacer>
          <v-menu bottom left offset-y>
            <template v-slot:activator="{ on }">
              <v-btn v-on="on" icon><v-icon>more_vert</v-icon></v-btn>
            </template>
            <v-list>
              <v-list-item @click="tabs_Add_Start">
                  <v-list-item-title>Add new Tab</v-list-item-title>
              </v-list-item>
              <v-list-item @click="tabs_Rename_Start" v-if="hasTab">
                  <v-list-item-title>Rename Tab</v-list-item-title>
              </v-list-item>
              <v-list-item @click="tabs_Move_Left" v-if="canTabMoveLeft">
                  <v-list-item-title>Move Left</v-list-item-title>
              </v-list-item>
              <v-list-item @click="tabs_Move_Right" v-if="canTabMoveRight">
                  <v-list-item-title>Move Right</v-list-item-title>
              </v-list-item>
              <v-list-item @click="showConfirmDeleteTab = true" v-if="hasTab">
                  <v-list-item-title>Delete Tab</v-list-item-title>
              </v-list-item>
              <v-list-item @click="editItems" v-if="hasTab">
                  <v-list-item-title>Configure Plot Items</v-list-item-title>
              </v-list-item>
              <v-list-item @click="editPlot" v-if="hasTab">
                  <v-list-item-title>Configure Plot</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-menu>
        </v-toolbar>

        <dy-graph ref="theGraph" v-if="currentTab !== null"
                :graph-data="currentTab.HistoryData"
                :graph-options="currentTab.Options"
                :graph-reset-zoom="zoomResetTime"></dy-graph>

        <v-dialog v-model="editorItems.show" persistent max-width="1100px" @keydown="(e) => { if (e.keyCode === 27 && !selectObject.show) { editorItems.show = false; }}">
          <v-card>
              <v-card-title>
                <span class="headline">Configure Plot Items</span>
              </v-card-title>
              <v-card-text>
                <table>
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th class="text-center">Color</th>
                      <th>&nbsp;</th>
                      <th>Size</th>
                      <th>Type</th>
                      <th>Axis</th>
                      <th>Check</th>
                      <th>Object Name</th>
                      <th>&nbsp;</th>
                      <th>Variable</th>
                      <th>&nbsp;</th>
                      <th>&nbsp;</th>
                    </tr>
                  </thead>
                  <template v-for="(item, idx) in editorItems.items">
                    <tr v-bind:key="idx">
                      <td><v-text-field class="tabcontent" v-model="item.Name"></v-text-field></td>
                      <td>
                        <v-menu offset-y>
                          <template v-slot:activator="{ on }">
                            <v-btn v-on="on" v-bind:style="{ backgroundColor: item.Color }" class="ml-2 mr-2" style="min-width:32px;width:32px;height:32px"></v-btn>
                          </template>
                          <div>
                            <div v-for="(color, index) in editorItems.colorList" :key="index" @click="item.Color = color">
                              <div style="padding:6px; cursor: pointer;" v-bind:style="{ backgroundColor: color }">{{color}}</div>
                            </div>
                          </div>
                        </v-menu>
                      </td>
                      <td><v-text-field class="tabcontent" v-model="item.Color"      style="width:9ch;"></v-text-field></td>
                      <td><v-text-field class="tabcontent" v-model="item.Size"       style="margin-left: 1ex; width:5ch;"></v-text-field></td>
                      <td><v-select     class="tabcontent" v-model="item.SeriesType" style="margin-left: 1ex; width:10ch;" :items="['Scatter', 'Line']"></v-select></td>
                      <td><v-select     class="tabcontent" v-model="item.Axis"       style="margin-left: 1ex; width:8ch;" :items="['Left', 'Right']"></v-select></td>
                      <td><v-checkbox   class="tabcontent" v-model="item.Checked"    style="margin-left: 1ex; margin-right: 1ex;"></v-checkbox></td>
                      <td style="font-size:16px; max-width:17ch; word-wrap:break-word;">{{editorItems_ObjectID2Name(item.Variable.Object)}}</td>
                      <td><v-btn class="ml-2 mr-4" style="min-width:36px;width:36px;" @click="editorItems_SelectObj(item)"><v-icon>edit</v-icon></v-btn></td>
                      <td><v-select     class="tabcontent" :items="editorItems_ObjectID2Variables(item.Variable.Object)" style="width:12ch;" v-model="item.Variable.Name"></v-select></td>
                      <td><v-btn class="ml-2 mr-2" style="min-width:36px;width:36px;" @click="editorItems_DeleteItem(idx)"><v-icon>delete</v-icon></v-btn></td>
                      <td><v-btn class="ml-2 mr-2" style="min-width:36px;width:36px;" v-if="idx > 0" @click="editorItems_MoveUpItem(idx)"><v-icon>keyboard_arrow_up</v-icon></v-btn></td>
                    </tr>
                  </template>
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
                    <td><v-btn style="min-width:36px;width:36px;" @click="editorItems_AddItem"><v-icon>add</v-icon></v-btn></td>
                    <td>&nbsp;</td>
                  </tr>
                </table>
              </v-card-text>
              <v-card-actions>
                <v-spacer></v-spacer>
                <v-btn color="grey darken-1"  text @click.native="editorItems.show = false">Cancel</v-btn>
                <v-btn color="primary darken-1" text :disabled="!isItemsOK" @click.native="editorItems_Save">Save</v-btn>
              </v-card-actions>
          </v-card>
        </v-dialog>

        <v-dialog v-model="showConfirmDeleteTab" persistent max-width="300px" @keydown="(e) => { if (e.keyCode === 27) { showConfirmDeleteTab = false; }}">
          <v-card>
            <v-card-title class="headline">Delete tab?</v-card-title>
            <v-card-text>
              Do you really want to permanently delete tab {{currentTab !== null ? currentTab.Name : ''}}?
            </v-card-text>
            <v-card-actions>
              <v-spacer></v-spacer>
              <v-btn color="grey darken-1" text @click.native="showConfirmDeleteTab = false">Cancel</v-btn>
              <v-btn color="primary darken-1"  text @click.native="tabs_Delete">OK</v-btn>
            </v-card-actions>
          </v-card>
        </v-dialog>

        <v-dialog v-model="showRenameTab" persistent max-width="300px" @keydown="(e) => { if (e.keyCode === 27) { showRenameTab = false; }}">
          <v-card>
            <v-card-title class="headline">Rename tab</v-card-title>
            <v-card-text>
              <v-text-field v-model="tabNameBuffer" ref="editTextRename" label="Tab Name"></v-text-field>
            </v-card-text>
            <v-card-actions>
              <v-spacer></v-spacer>
              <v-btn color="grey darken-1" text @click.native="showRenameTab = false">Cancel</v-btn>
              <v-btn color="primary darken-1" text @click.native="tabs_Rename">OK</v-btn>
            </v-card-actions>
          </v-card>
        </v-dialog>

        <v-dialog v-model="showAddTab" persistent max-width="300px" @keydown="(e) => { if (e.keyCode === 27) { showAddTab = false; }}">
          <v-card>
            <v-card-title class="headline">Add tab</v-card-title>
            <v-card-text>
              <v-text-field v-model="tabNameBuffer" ref="editTextAdd" label="Tab Name"></v-text-field>
            </v-card-text>
            <v-card-actions>
              <v-spacer></v-spacer>
              <v-btn color="grey darken-1" text @click.native="showAddTab = false">Cancel</v-btn>
              <v-btn color="primary darken-1"  text @click.native="tabs_Add">OK</v-btn>
            </v-card-actions>
          </v-card>
        </v-dialog>

        <v-dialog v-model="editorPlot.show" persistent max-width="400px" @keydown="(e) => { if (e.keyCode === 27) { editorPlot.show = false; }}">
          <v-card>
            <v-card-title>
              <span class="headline">Configure Plot</span>
            </v-card-title>
            <v-card-text>
              <table style="width:100%;">
                <tr>
                  <td><v-text-field v-model="editorPlot.plot.LeftAxisName"  label="Left Axis Caption" ></v-text-field></td>
                </tr>
                <tr>
                  <td><v-checkbox v-model="editorPlot.plot.LeftAxisStartFromZero" label="Left Y Axis: Start From Zero"></v-checkbox></td>
                </tr>
                <tr>
                  <td><v-text-field v-model="editorPlot.plot.RightAxisName" label="Right Axis Caption" ></v-text-field></td>
                </tr>
                <tr>
                  <td><v-checkbox v-model="editorPlot.plot.RightAxisStartFromZero" label="Right Y Axis: Start From Zero"></v-checkbox></td>
                </tr>
                <tr>
                  <td><v-text-field v-model="editorPlot.plot.MaxDataPoints" label="Max DataPoints"    ></v-text-field></td>
                </tr>
              </table>
            </v-card-text>
            <v-card-actions>
              <v-spacer></v-spacer>
              <v-btn color="grey darken-1"  text @click.native="editorPlot.show = false">Cancel</v-btn>
              <v-btn color="primary darken-1" text @click.native="editorPlot_Save">Save</v-btn>
            </v-card-actions>
          </v-card>
        </v-dialog>

        <dlg-object-select v-model="selectObject.show"
            :object-id="selectObject.selectedObjectID"
            :module-id="selectObject.selectedModuleID"
            :modules="selectObject.modules"
            @onselected="selectObject_OK"></dlg-object-select>

      </v-container>
    </v-content>
  </v-app>
</template>

<script lang="ts">

import DyGraph from '../components/DyGraph.vue'
import DlgObjectSelect from '../components/DlgObjectSelect.vue'
import { Component, Vue, Watch } from 'vue-property-decorator'

interface ModuleInfo {
  ID: string
  Name: string
}

interface Variable {
  Object: string
  Name: string
}

interface PlotConfig {
  MaxDataPoints: number
  LeftAxisName: string
  LeftAxisStartFromZero: boolean
  RightAxisName: string
  RightAxisStartFromZero: boolean
}

interface TabConfig {
  Name: string
  PlotConfig: PlotConfig
  Items: ItemConfig[]
}

interface TabRes {
  Name: string
  MaxDataPoints: number
  Variables: Variable[]
  Options: any
  HistoryData: any[][]
  Configuration: TabConfig
}

type SeriesType = 'Line' | 'Scatter'

type Axis = 'Left' | 'Right'

interface ItemConfig {
  Name: string
  Color: string
  Size: number
  SeriesType: SeriesType
  Axis: Axis
  Checked: boolean
  Variable: Variable
}

interface Obj {
  Type: string
  ID: string
  Name: string
  Variables: string[]
}

interface ObjInfo {
  Name: string
  Variables: string[]
}

interface SelectObject {
  show: boolean
  modules: ModuleInfo[]
  selectedModuleID: string
  selectedObjectID: string
  variable: Variable
}

interface EditorItems {
  show: boolean
  items: ItemConfig[]
  colorList: string[]
}

interface ObjectMap {
  [key: string]: ObjInfo
}

interface EditorPlot {
  show: boolean
  plot: PlotConfig
}

@Component({
  components: {
    DyGraph,
    DlgObjectSelect,
  },
})
export default class ViewHistory extends Vue {

  currentTab: TabRes = null
  tabs: TabRes[] = []
  timeRange: any = {}
  zoomResetTime = 0
  objectMap: ObjectMap = {}
  editorItems: EditorItems = {
    show: false,
    items: [],
    colorList: [
      '#1BA1E2',
      '#A05000',
      '#339933',
      '#A2C139',
      '#D80073',
      '#F09609',
      '#E671B8',
      '#A200FF',
      '#E51400',
      '#00ABA9',
    ],
  }
  editorPlot: EditorPlot = {
    show: false,
    plot: {
      MaxDataPoints: 100,
      LeftAxisName: '',
      LeftAxisStartFromZero: true,
      RightAxisName: '',
      RightAxisStartFromZero: true,
    },
  }
  showConfirmDeleteTab = false
  selectedModuleID = ''
  showRenameTab = false
  showAddTab = false
  tabNameBuffer = ''
  selectObject: SelectObject = {
    show: false,
    modules: [],
    selectedModuleID: '',
    selectedObjectID: '',
    variable: { Object: '', Name: '' },
  }

  get hasTab(): boolean {
    return this.tabs.length > 0
  }

  get canTabMoveLeft(): boolean {
    if (this.tabs.length === 0) { return false }
    const i = this.tabs.findIndex((t) => t === this.currentTab)
    return i > 0
  }

  get canTabMoveRight(): boolean {
    if (this.tabs.length === 0) { return false }
    const i = this.tabs.findIndex((t) => t === this.currentTab)
    return i < this.tabs.length - 1
  }

  get isItemsOK() {
    return this.editorItems.items.every((it) => {
      return it.Name !== '' &&
        it.Variable.Object !== '' &&
        it.Variable.Name !== ''
    })
  }

  init() {
    const context = this
    const para = {
      TimeRange: this.timeRange,
    }
    window.parent['dashboardApp'].sendViewRequest('Init', para, (strResponse) => {
      const response = JSON.parse(strResponse)
      response.Tabs.forEach((tab) => {
        tab.Options = context.augmentOptions(tab.Options)
        context.sliceDataToDateWindow(tab.HistoryData, tab.Variables.length, response.WindowLeft, response.WindowRight)
      })
      context.objectMap = response.ObjectMap
      context.selectObject.modules = response.Modules
      context.tabs = response.Tabs
      if (response.Tabs.length > 0) {
        context.currentTab = response.Tabs[0]
        context.onLoadDataForNextTab(0, false, true)
      }
    })
  }

  onLoadDataForNextTab(idxTabToLoad: number, resetZoom: boolean, loadNext: boolean) {
    if (idxTabToLoad >= this.tabs.length) { return }
    const context = this
    const tab = this.tabs[idxTabToLoad]
    const para = {
      TabName: tab.Name,
      TimeRange: this.timeRange,
      Variables: tab.Variables,
      MaxDataPoints: tab.MaxDataPoints,
    }
    window.parent['dashboardApp'].sendViewRequest('LoadTabData', para, (strResponse) => {
      const response = JSON.parse(strResponse)
      const data: any[][] = response.Data

      context.convertTimestamps(data)
      context.sliceDataToDateWindow(data, tab.Variables.length, response.WindowLeft, response.WindowRight)
      tab.HistoryData = data

      if (resetZoom) {
        context.zoomResetTime = new Date().getTime()
      }
      if (loadNext) {
        context.onLoadDataForNextTab(idxTabToLoad + 1, false, true)
      }
    })
  }

  convertTimestamps(data: any[][]) {
    const len = data.length
    for (let i = 0; i < len; ++i) {
      const entry = data[i]
      entry[0] = new Date(entry[0])
    }
  }

  sliceDataToDateWindow(data: any[][], seriesCount: number, windowLeft: number, windowRight: number) {

    const leftData  = [new Date(windowLeft)]
    const rightData = [new Date(windowRight)]
    for (let i = 0; i < seriesCount; ++i) {
      leftData.push(null)
      rightData.push(null)
    }

    if (data.length === 0) {
      data.push(leftData)
      data.push(rightData)
    }
    else {

      while (data.length > 0 && data[0][0].getTime() < windowLeft) {
        data.shift()
      }

      if (windowLeft < data[0][0].getTime()) {
        data.unshift(leftData)
      }

      if (data[data.length - 1][0].getTime() < windowRight) {
        data.push(rightData)
      }
    }
  }

  onTimeRangeChange() {
    this.onLoadDataForNextTab(0, true, true)
  }

  classObject(tabName: string) {
    const sel = this.currentTab !== null && this.currentTab.Name === tabName
    return {
      selectedtab: sel,
      nonselectedtab: !sel,
    }
  }

  tabs_Add_Start() {
    this.tabNameBuffer = ''
    this.showAddTab = true
    const context = this
    setTimeout(() => {
      const txtAdd = context.$refs.editTextAdd as HTMLElement
      txtAdd.focus()
    }, 100)
  }

  tabs_Add() {
    this.showAddTab = false
    const newName = this.tabNameBuffer
    const context = this
    const para = {
      TimeRange: this.timeRange,
      NewName: newName,
    }
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('AddTab', para, (strResponse) => {
      const res = JSON.parse(strResponse)
      const tab = res.NewTab
      context.sliceDataToDateWindow(tab.HistoryData, tab.Variables.length, res.WindowLeft, res.WindowRight)
      context.tabs.push(tab)
      context.currentTab = tab
    })
  }

  tabs_Rename_Start() {
    this.tabNameBuffer = this.currentTab.Name
    this.showRenameTab = true
    const context = this
    setTimeout(() => {
      const txtRename = context.$refs.editTextRename as HTMLElement
      txtRename.focus()
    }, 100)
  }

  tabs_Rename() {
    this.showRenameTab = false
    const newName = this.tabNameBuffer
    const context = this
    const para = {
      TabName: this.currentTab.Name,
      NewName: newName,
    }
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('RenameTab', para, (strResponse) => {
      const res = JSON.parse(strResponse)
      context.currentTab.Configuration = res.Configuration
      context.currentTab.Name = newName
    })
  }

  tabs_Move_Left() {
    const context = this
    const tabIdx = this.tabs.findIndex((t) => t === this.currentTab)
    const dashboard = window.parent['dashboardApp']
    const para = {
      TabName: this.currentTab.Name,
    }
    dashboard.sendViewRequest('MoveLeft', para, (strResponse) => {
      const arr = context.tabs
      const tmp = arr[tabIdx]
      Vue.set(arr, tabIdx, arr[tabIdx - 1])
      Vue.set(arr, tabIdx - 1, tmp)
    })
  }

  tabs_Move_Right() {
    const context = this
    const tabIdx = this.tabs.findIndex((t) => t === this.currentTab)
    const dashboard = window.parent['dashboardApp']
    const para = {
      TabName: this.currentTab.Name,
    }
    dashboard.sendViewRequest('MoveRight', para, (strResponse) => {
      const arr = context.tabs
      const tmp = arr[tabIdx]
      Vue.set(arr, tabIdx, arr[tabIdx + 1])
      Vue.set(arr, tabIdx + 1, tmp)
    })
  }

  tabs_Delete() {
    this.showConfirmDeleteTab = false
    const context = this
    const tabIdx = this.tabs.findIndex((t) => t === this.currentTab)
    const tabCount = this.tabs.length
    const para = {
      TabName: this.currentTab.Name,
    }
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('DeleteTab', para, (strResponse) => {
      context.tabs.splice(tabIdx, 1)
      if (tabCount === 1) {
        context.currentTab = null
      }
      else if (tabIdx === tabCount - 1) {
        context.currentTab = context.tabs[tabIdx - 1]
      }
      else {
        context.currentTab = context.tabs[tabIdx]
      }
    })
  }

  editItems() {
    const str = JSON.stringify(this.currentTab.Configuration.Items)
    this.editorItems.items = JSON.parse(str)
    this.editorItems.show = true
  }

  editPlot() {
    const str = JSON.stringify(this.currentTab.Configuration.PlotConfig)
    this.editorPlot.plot = JSON.parse(str)
    this.editorPlot.show = true
  }

  editorItems_AddItem() {
    const item: ItemConfig = {
      Name: '',
      Color: this.editorItems.colorList[this.editorItems.items.length % this.editorItems.colorList.length],
      Size: 3,
      SeriesType: 'Scatter',
      Axis: 'Left',
      Checked: true,
      Variable: {
        Object: '',
        Name: '',
      },
    }
    this.editorItems.items.push(item)
  }

  editorItems_DeleteItem(idx: number) {
    this.editorItems.items.splice(idx, 1)
  }

  editorItems_MoveUpItem(idx: number) {
    const array = this.editorItems.items
    if (idx > 0) {
      const item = array[idx]
      array.splice(idx, 1)
      array.splice(idx - 1, 0, item)
    }
  }

  editorItems_Save() {
    this.editorItems.show = false
    const tabIdx = this.tabs.findIndex((t) => t === this.currentTab)
    const context = this
    const para = {
      TabName: this.currentTab.Configuration.Name,
      Items: this.editorItems.items,
    }
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('SaveItems', para, (strResponse) => {
      const res = JSON.parse(strResponse)
      context.currentTab.Variables = res.Variables
      context.currentTab.Options = context.augmentOptions(res.Options)
      context.currentTab.Configuration = res.Configuration
      if (res.ReloadData) {
        context.onLoadDataForNextTab(tabIdx, true, false)
      }
    })
  }

  editorItems_ObjectID2Name(id: string) {
    const obj: ObjInfo = this.objectMap[id]
    if (obj === undefined) { return id }
    return obj.Name
  }

  editorItems_ObjectID2Variables(id: string) {
    const obj: ObjInfo = this.objectMap[id]
    if (obj === undefined) { return [] }
    return obj.Variables
  }

  editorPlot_Save() {
    this.editorPlot.show = false
    const tabIdx = this.tabs.findIndex((t) => t === this.currentTab)
    const context = this
    const para = {
      TabName: this.currentTab.Configuration.Name,
      Plot: this.editorPlot.plot,
    }
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('SavePlot', para, (strResponse) => {
      const res = JSON.parse(strResponse)
      context.currentTab.Options = context.augmentOptions(res.Options)
      context.currentTab.Configuration = res.Configuration
      if (res.ReloadData) {
        context.onLoadDataForNextTab(tabIdx, true, false)
      }
    })
  }

  editorItems_SelectObj(item: ItemConfig) {
    const currObj: string = item.Variable.Object
    let objForModuleID: string = currObj
    if (objForModuleID === '') {
      const nonEmptyItems = this.editorItems.items.filter((it) => it.Variable.Object !== '')
      if (nonEmptyItems.length > 0) {
        objForModuleID = nonEmptyItems[0].Variable.Object
      }
    }

    const i = objForModuleID.indexOf(':')
    if (i <= 0) {
      this.selectObject.selectedModuleID = this.selectObject.modules[0].ID
    }
    else {
      this.selectObject.selectedModuleID = objForModuleID.substring(0, i)
    }
    this.selectObject.selectedObjectID = currObj
    this.selectObject.variable = item.Variable
    this.selectObject.show = true
  }

  selectObject_OK(obj: Obj) {
    this.objectMap[obj.ID] = {
      Name: obj.Name,
      Variables: obj.Variables,
    }
    this.selectObject.variable.Object = obj.ID
    if (obj.Variables.length === 1) {
      this.selectObject.variable.Name = obj.Variables[0]
    }
  }

  augmentOptions(options) {
    options.legendFormatter = (data) => {
      const HasData = data.x != null
      const theSeries = data.series.map((series, i) => {
        const id = 'varCheck_' + i
        const checked = series.isVisible ? 'checked' : ''
        const label = series.labelHTML
        return `<div style="background-color: white; display: inline;">` +
          `<input type=checkbox id="${id}" ${checked} onClick="window.changeVarVisibility('${id}', ${i})">&nbsp;&nbsp;` +
          `<span style='font-weight: bold;'>${series.dashHTML} ${label}</span>` +
          (HasData && series.isVisible ? (': ' + series.yHTML) : '') +
          '&nbsp;</div>'
      })
      return (HasData ? data.xHTML : '') + '<br>' + theSeries.join('<br>')
    }
    return options
  }

  mounted() {

    const context = this

    window['changeVarVisibility'] = (elementID: string, index: number) => {
      const checkBox: any = document.getElementById(elementID)
      const gr: any = context.$refs.theGraph
      gr._data.graph.setVisibility(index, checkBox.checked)
    }

    const dashboard = window.parent['dashboardApp']
    dashboard.showTimeRangeSelector(true)
    this.timeRange = dashboard.getCurrentTimeRange()
    this.init()

    dashboard.registerTimeRangeListener((timeRange) => {
      context.timeRange = timeRange
      context.onTimeRangeChange()
    })

    dashboard.registerViewEventListener((eventName, eventPayload) => {

        if (eventName === 'TabDataAppend') {
          const tabName = eventPayload.TabName
          const newData = JSON.parse(eventPayload.Data)
          context.convertTimestamps(newData)

          const tab = context.tabs.find((ta) => ta.Name === tabName)
          const data = tab.HistoryData

          const hasData = (entry) => {
            for (let k = 1; k < entry.length; ++k) {
              if (entry[k] !== null) { return true }
            }
            return false
          }

          const t = newData[0][0]
          const len = data.length
          let countRemove = 0
          for (let i = len - 1; i >= 0; --i) {
            const entry = data[i]
            if (entry[0] < t && hasData(entry)) {
              break
            }
            countRemove += 1
          }
          if (countRemove > 0) {
            data.splice(-countRemove, countRemove)
          }

          newData.forEach((element) => {
            data.push(element)
          })

          context.sliceDataToDateWindow(data, tab.Variables.length, eventPayload.WindowLeft, eventPayload.WindowRight)
        }
    })
  }
}

</script>

<style>

  .container {
    padding: 16px 0px !important;
  }

  @media only screen and (min-width: 1100px) {
    .container {
      max-width: 1080px;
    }
  }

  @media only screen and (min-width: 1400px) {
    .container {
      max-width: 1380px;
    }
  }

  @media only screen and (min-width: 1700px) {
    .container {
      max-width: 1680px;
    }
  }

  .selectedtab {
    font-weight: bold;
    color: black !important;
    margin: 12px !important;
  }

  .nonselectedtab {
    font-weight: normal;
    color: grey !important;
    margin: 12px !important;
  }

  th {
    text-align: left;
  }

  .tabcontent {
    padding-top: 2px !important;
  }

  .input-group__details {
    min-height: 0px;
  }

  .v-data-table th {
    font-size: 14px;
  }

</style>
