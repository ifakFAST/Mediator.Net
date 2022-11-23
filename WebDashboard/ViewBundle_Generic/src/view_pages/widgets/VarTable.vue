<template>
  <div>

    <div @contextmenu="onContextMenu">
      <v-simple-table :height="theHeight" dense>
        <template v-slot:default>
          <thead>
            <tr>
              <th class="text-left"  style="font-size:14px;">Variable</th>
              <th class="text-right" style="font-size:14px; height:36px; padding-right:0px;">Value</th>
              <th class="text-right" style="font-size:14px; height:36px;"></th>
              <th class="text-right" style="font-size:14px; height:36px;">Time</th>
              <th v-if="config.ShowTrendColumn" class="text-center" style="font-size:14px; height:36px;">Trend</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in items" :key="item.Name" :style="varItemStyle(item)">
              <td class="text-left"  style="font-size:14px; height:36px;">
                <v-tooltip right open-delay="250">
                  <template v-slot:activator="{ on, attrs }">
                    <span v-bind="attrs" v-on="on">{{ item.Name }}</span>
                  </template>
                  <span>{{ varItemInfo(item) }}</span>
                </v-tooltip>
              </td>
              <td class="text-right" style="font-size:14px; height:36px; padding-right:0px;">
                <v-tooltip right open-delay="250">
                  <template v-slot:activator="{ on, attrs }">
                    <span v-bind="attrs" v-on="on"><span v-bind:style="{ color: item.ValueColor }">{{ item.Value }}</span></span>
                  </template>
                  <span>{{ varItemInfo(item) }}</span>
                </v-tooltip>
              </td>
              <td class="text-left"  style="font-size:14px; height:36px; padding-left:8px;">
                <v-tooltip right open-delay="250">
                  <template v-slot:activator="{ on, attrs }">
                    <span v-bind="attrs" v-on="on">{{ item.Unit }}</span>
                  </template>
                  <span>{{ varItemInfo(item) }}</span>
                </v-tooltip>
              </td>
              <td class="text-right" style="font-size:14px; height:36px;">
                <v-tooltip right open-delay="250">
                  <template v-slot:activator="{ on, attrs }">
                    <span v-bind="attrs" v-on="on">{{ item.Time }}</span>
                  </template>
                  <span>{{ varItemInfo(item) }}</span>
                </v-tooltip>
              </td>
              <td v-if="config.ShowTrendColumn" class="text-center" style="font-size:14px; height:36px;">
                <v-icon :color="varItemColor(item)" v-if="item.Trend === 'up'">mdi-arrow-top-right</v-icon>
                <v-icon :color="varItemColor(item)" v-if="item.Trend === 'down'">mdi-arrow-bottom-right</v-icon>
                <v-icon :color="varItemColor(item)" v-if="item.Trend === 'flat'">mdi-arrow-right</v-icon>
              </td>
            </tr>
          </tbody>
        </template>
      </v-simple-table>
    </div>

    <v-menu v-model="contextMenu.show" :position-x="contextMenu.clientX" :position-y="contextMenu.clientY" absolute offset-y>
      <v-list>
        <v-list-item @click="onConfigureItems" >
          <v-list-item-title>Configure Items...</v-list-item-title>
        </v-list-item>
        <v-list-item @click="onToggleShowTrendColumn" >
          <v-list-item-title> {{ config.ShowTrendColumn ? 'Hide Trend Column' : 'Show Trend Column' }}</v-list-item-title>
        </v-list-item>
<!--         <v-list-item @click="onConfigurePlot" >
          <v-list-item-title>Configure Plot...</v-list-item-title>
        </v-list-item> -->
      </v-list>
    </v-menu>

    <v-dialog v-model="editorItems.show" persistent max-width="1100px" @keydown="(e) => { if (e.keyCode === 27 && !selectObject.show) { editorItems.show = false; }}">
      <v-card>
          <v-card-title>
            <span class="headline">Configure Items</span>
          </v-card-title>
          <v-card-text>
            <table>
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
              
              <tr v-for="(item, idx) in editorItems.items" v-bind:key="idx">
                <td><v-text-field class="tabcontent" v-model="item.Name"></v-text-field></td>
                <td><v-text-field class="tabcontent" v-model="item.Unit" style="max-width:8ch;"></v-text-field></td>
                <td style="font-size:16px; max-width:17ch; word-wrap:break-word;">{{editorItems_ObjectID2Name(item.Variable.Object)}}</td>
                <td><v-btn class="ml-2 mr-4" style="min-width:36px;width:36px;" @click="editorItems_SelectObj(item)"><v-icon>edit</v-icon></v-btn></td>
                <td><v-select     class="tabcontent" :items="editorItems_ObjectID2Variables(item.Variable.Object)" style="width:12ch;" v-model="item.Variable.Name"></v-select></td>
                <td><v-text-field class="tabcontent" style="max-width:8ch;" v-model="item.TrendFrame"></v-text-field></td>
                <td><text-field-nullable-number style="max-width:8ch;" v-model="item.WarnBelow"></text-field-nullable-number></td>
                <td><text-field-nullable-number style="max-width:8ch;" v-model="item.WarnAbove"></text-field-nullable-number></td>
                <td><text-field-nullable-number style="max-width:8ch;" v-model="item.AlarmBelow"></text-field-nullable-number></td>
                <td><text-field-nullable-number style="max-width:8ch;" v-model="item.AlarmAbove"></text-field-nullable-number></td>

                <td><v-text-field class="tabcontent" style="max-width:8ch;" v-model="item.EnumValues"></v-text-field></td>

                <td><v-btn class="ml-2 mr-2" style="min-width:36px;width:36px;" @click="editorItems_DeleteItem(idx)"><v-icon>delete</v-icon></v-btn></td>
                <td><v-btn class="ml-2 mr-2" style="min-width:36px;width:36px;" v-if="idx > 0" @click="editorItems_MoveUpItem(idx)"><v-icon>keyboard_arrow_up</v-icon></v-btn></td>
              </tr>
              
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
                <td><v-btn class="ml-2 mr-2" style="min-width:36px;width:36px;" @click="editorItems_AddItem"><v-icon>add</v-icon></v-btn></td>
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

    <dlg-object-select v-model="selectObject.show"
      :object-id="selectObject.selectedObjectID"
      :module-id="selectObject.selectedModuleID"
      :modules="selectObject.modules"
      @onselected="selectObject_OK"></dlg-object-select>

  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import * as fast from '../../fast_types'
import { TimeRange } from '../../utils'
import DlgObjectSelect from '../../components/DlgObjectSelect.vue'
import TextFieldNullableNumber from '../../components/TextFieldNullableNumber.vue'
import { ModuleInfo, ObjectMap, Obj, SelectObject, ObjInfo } from './common'

interface Config {
  ShowTrendColumn: boolean
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

@Component({
  components: {
    DlgObjectSelect,
    TextFieldNullableNumber,
  },
})
export default class VarTable extends Vue {

  @Prop({ default() { return '' } }) id: string
  @Prop({ default() { return '' } }) width: string
  @Prop({ default() { return '' } }) height: string
  @Prop({ default() { return {} } }) config: Config
  @Prop() backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  @Prop({ default() { return '' } }) eventName: string
  @Prop({ default() { return {} } }) eventPayload: object
  @Prop({ default() { return {} } }) timeRange: TimeRange
  @Prop({ default() { return 0 } }) resize: number
  @Prop({ default() { return null } }) dateWindow: number[]

  items: VarItem[] = []

  contextMenu = {
    show: false,
    clientX: 0,
    clientY: 0,
  }

  objectMap: ObjectMap = {}

  selectObject: SelectObject = {
    show: false,
    modules: [],
    selectedModuleID: '',
    selectedObjectID: '',
  }

  currentVariable: fast.VariableRef = { Object: '', Name: '' }

  editorItems: EditorItems = {
    show: false,
    items: [],
  }

  get theHeight(): string {
    if (this.height.trim() === '') { return 'auto' }
    return this.height
  }

  varItemStyle(item: VarItem): object {
    const style = {}
    if (!!item.Alarm || !!item.Warning) {
      style['font-weight'] = 'bold'
      style['color'] = this.varItemColor(item)
    }
    return style
  }

  varItemInfo(item: VarItem): string {
    if (item.Alarm)   { return item.Alarm }
    if (item.Warning) { return item.Warning }
    return 'Quality of variable is Good'
  }

  varItemColor(item: VarItem): string {
    if (item.Alarm)   { return 'red' }
    if (item.Warning) { return 'orange' }
    return ''
  }

  mounted(): void {
    this.onLoadData()
  }

  onContextMenu(e: any): void {
    e.preventDefault()
    e.stopPropagation()
    this.contextMenu.show = false
    this.contextMenu.clientX = e.clientX
    this.contextMenu.clientY = e.clientY
    const context = this
    this.$nextTick(() => {
      context.contextMenu.show = true
    })
  }

  get configItems(): ItemConfig[] {
    return this.config.Items ?? []
  }

  get isItemsOK(): boolean {
    const notEmpty = (it: ItemConfig) => it.Name !== '' && it.Variable.Object !== '' && it.Variable.Name !== ''
    const names = new Set(this.editorItems.items.map((it) => it.Name))
    return this.editorItems.items.every(notEmpty) && names.size === this.editorItems.items.length
  }

  async onToggleShowTrendColumn(): Promise<void> {
    await this.backendAsync('ToggleShowTrendColumn', {})
  }

  async onConfigureItems(): Promise<void> {

    const response: {
        ObjectMap: ObjectMap,
        Modules: ModuleInfo[],
      } = await this.backendAsync('GetItemsData', { })

    this.objectMap = response.ObjectMap
    this.selectObject.modules = response.Modules

    const str = JSON.stringify(this.configItems)
    this.editorItems.items = JSON.parse(str)
    this.editorItems.show = true
  }

  async onLoadData(): Promise<void> {
    const items: VarItem[] = await this.backendAsync('LoadData', { })
    this.items = items
  }

  @Watch('eventPayload')
  watch_event(newVal: object, old: object): void {
    if (this.eventName === 'OnVarChanged') {
      const updatedItems: VarItem[] = this.eventPayload as any
      for (const it of updatedItems) {
        for (const at of this.items) {
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
  }

  editorItems_AddItem(): void {
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
    this.editorItems.items.push(item)
  }

  editorItems_DeleteItem(idx: number): void {
    this.editorItems.items.splice(idx, 1)
  }

  editorItems_MoveUpItem(idx: number): void {
    const array = this.editorItems.items
    if (idx > 0) {
      const item = array[idx]
      array.splice(idx, 1)
      array.splice(idx - 1, 0, item)
    }
  }

  async editorItems_Save(): Promise<void> {

    this.editorItems.show = false

    const para = {
      items: this.editorItems.items,
    }
    const items: VarItem[] = await this.backendAsync('SaveItems', para)
    this.items = items
  }

  editorItems_ObjectID2Name(id: string): string {
    const obj: ObjInfo = this.objectMap[id]
    if (obj === undefined) { return id }
    return obj.Name
  }

  editorItems_ObjectID2Variables(id: string): string[] {
    const obj: ObjInfo = this.objectMap[id]
    if (obj === undefined) { return [] }
    return obj.Variables
  }

  editorItems_SelectObj(item: ItemConfig): void {
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
    this.selectObject.show = true
    this.currentVariable = item.Variable
  }

  selectObject_OK(obj: Obj): void {
    this.objectMap[obj.ID] = {
      Name: obj.Name,
      Variables: obj.Variables,
    }
    this.currentVariable.Object = obj.ID
    if (obj.Variables.length === 1) {
      this.currentVariable.Name = obj.Variables[0]
    }
  }
}

</script>
