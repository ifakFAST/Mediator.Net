<template>
    <div>
        <v-dialog v-model="show" persistent max-width="1100px" 
                @keydown="(e) => { if (e.keyCode === 27 && !selectObject.show) { closeDialog(); }}">
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

                          <th class="text-left">Member</th>

                          <th class="text-left">Type</th>

                          <th class="text-left">Min</th>
                          <th class="text-left">Max</th>
                          <th class="text-left">Enum Values</th>

                          <th>&nbsp;</th>
                          <th>&nbsp;</th>
                        </tr>
                    </thead>
                    
                    <tr v-for="(item, idx) in items" v-bind:key="idx">
                        <td><v-text-field class="tabcontent" style="max-width:15ch;" v-model="item.Name"></v-text-field></td>
                        <td><v-text-field class="tabcontent ml-2 mr-2" v-model="item.Unit" style="max-width:8ch;"></v-text-field></td>
                        <td style="font-size:16px; max-width:20ch; word-wrap:break-word;">{{editorItems_ObjectID2Name(item.Object)}}</td>
                        <td><v-btn class="ml-2 mr-4" style="min-width:36px;width:36px;" @click="editorItems_SelectObj(item)"><v-icon>edit</v-icon></v-btn></td>
                        <td><v-select     class="tabcontent mr-2" :items="editorItems_ObjectID2Members(item.Object)" style="width:20ch;" v-model="item.Member"></v-select></td>
                        
                        <td><v-select     class="tabcontent mr-2" :items="['Range', 'Enum']" style="width:9ch;" v-model="item.Type"></v-select></td>

                        <td><text-field-nullable-number v-if="item.Type === 'Range'" style="max-width:8ch;" v-model="item.MinValue"></text-field-nullable-number></td>
                        <td><text-field-nullable-number v-if="item.Type === 'Range'" class="ml-2 mr-2" style="max-width:8ch;" v-model="item.MaxValue"></text-field-nullable-number></td>
                        
                        <td><v-text-field class="tabcontent" v-if="item.Type === 'Enum'" style="max-width:14ch;" v-model="item.EnumValues"></v-text-field></td>

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
                        <td><v-btn class="ml-2 mr-2" style="min-width:36px;width:36px;" @click="editorItems_AddItem"><v-icon>add</v-icon></v-btn></td>
                        <td>&nbsp;</td>
                    </tr>
                </table>
                </v-card-text>
                <v-card-actions>
                    <v-spacer></v-spacer>
                    <v-btn color="grey darken-1"    text @click.native="closeDialog">Cancel</v-btn>
                    <v-btn color="primary darken-1" text :disabled="!isItemsOK" @click.native="editorItems_Save">Save</v-btn>
                </v-card-actions>
            </v-card>
        </v-dialog>

        <dlg-object-select v-model="selectObject.show"
            :object-id="selectObject.selectedObjectID"
            :module-id="selectObject.selectedModuleID"
            :modules="selectObject.modules"
            :filter="'WithMembers'"
            @onselected="selectObject_OK"></dlg-object-select>

      </div>

  </template>
  
  <script lang="ts">
  
  import { Component, Prop, Vue } from 'vue-property-decorator'
  import { ModuleInfo, Obj, SelectObject } from '../common'
  import { ConfigItem } from './types'
  import DlgObjectSelect from '../../../components/DlgObjectSelect.vue'
  import TextFieldNullableNumber from '../../../components/TextFieldNullableNumber.vue'

  interface ObjInfo {
    Name: string
    Members: string[]
  }

  interface ObjectMap {
    [key: string]: ObjInfo
  }

  @Component({
    components: {
      DlgObjectSelect,
      TextFieldNullableNumber,
    },
  })
  export default class DlgConfigItems extends Vue {

    @Prop({ default() { return [] } }) configItems: ConfigItem[]
    @Prop() backendAsync: (request: string, parameters: object) => Promise<any>
  
    show: boolean = false
    items: ConfigItem[] = []

    objectMap: ObjectMap = {}

    selectObject: SelectObject = {
      show: false,
      modules: [],
      selectedModuleID: '',
      selectedObjectID: '',
    }

    currentItem: ConfigItem | null = null

    resolveDialog: ((v: boolean) => void) = (v) => {}

    async showDialog(): Promise<boolean> {

      const response: {
          ObjectMap: ObjectMap,
          Modules: ModuleInfo[],
        } = await this.backendAsync('GetItemsData', { })

      this.objectMap = response.ObjectMap
      this.selectObject.modules = response.Modules

      const str = JSON.stringify(this.configItems)
      this.items = JSON.parse(str)
      this.show = true

      return new Promise<boolean>((resolve) => {
        this.resolveDialog = resolve
      })
    }

    editorItems_ObjectID2Name(id: string): string {
      if (id === undefined) { return '' }
      const obj: ObjInfo = this.objectMap[id]
      if (obj === undefined) { return id }
      return obj.Name
    }

    editorItems_SelectObj(item: ConfigItem): void {
      const currObj: string = item.Object === null ? '' : item.Object
      let objForModuleID: string = currObj
      if (objForModuleID === '') {
        const nonEmptyItems = this.items.filter((it) => it.Object !== null && it.Object !== '')
        if (nonEmptyItems.length > 0) {
          objForModuleID = nonEmptyItems[0].Object
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
      this.currentItem = item
    }

    editorItems_ObjectID2Members(id: string): string[] {
      const obj: ObjInfo = this.objectMap[id]
      if (obj === undefined) { return [] }
      return obj.Members
    }

    editorItems_DeleteItem(idx: number): void {
      this.items.splice(idx, 1)
    }

    editorItems_MoveUpItem(idx: number): void {
      const array = this.items
      if (idx > 0) {
        const item = array[idx]
        array.splice(idx, 1)
        array.splice(idx - 1, 0, item)
      }
    }

    editorItems_AddItem(): void {
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
      this.items.push(item)
    }

    get isItemsOK(): boolean {
      const notEmpty = (it: ConfigItem) => it.Name !== '' /* && it.Object !== '' && it.Member !== '' */
      const names = new Set(this.items.map((it) => it.Name))
      return this.items.every(notEmpty) && names.size === this.items.length
    }

    async editorItems_Save(): Promise<void> {
      const para = {
        items: this.items,
      }
      try {
        await this.backendAsync('SaveItems', para)
      }
      catch (exp) {
        alert(exp)
        return
      }
      this.show = false
      this.resolveDialog(true)
    }

    closeDialog(): void {
      this.show = false
      this.resolveDialog(false)
    }

    selectObject_OK(obj: Obj): void {
      this.objectMap[obj.ID] = {
        Name: obj.Name,
        Members: obj.Members,
      }
      if (this.currentItem !== null) {
        this.currentItem.Object = obj.ID
        if (obj.Members.length === 1) {
          this.currentItem.Member = obj.Members[0]
        }
      }
    }
  }
  
  </script>
  