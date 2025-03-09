<template>
  <div>
    <v-dialog v-model="show" persistent max-width="1100px" @keydown="(e) => { if (e.keyCode === 27 && !selectObject.show) { closeDialog(); }}">
      <v-card>
        <v-card-title>
          <span class="headline">Configure Map</span>
        </v-card-title>

        <v-card-text>
          <!-- Map Configuration -->
          <v-subheader>Map Settings</v-subheader>
          <v-row>
            <v-col cols="4">
              <v-text-field
                v-model="theConfig.MapConfig.Center"
                label="Center"
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.MapConfig.ZoomDefault"
                label="Default Zoom Level"
                type="number"
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.MapConfig.MainGroupLabel"
                label="Main Group Label"
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.MapConfig.OptionalGroupLabel"
                label="Optional Group Label"
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.MapConfig.MouseOverOpacityDelta"
                label="MouseOver Opacity Delta"
              ></v-text-field>
            </v-col>
          </v-row>

          <!-- Tile Layers -->
          <v-subheader class="mt-4">
            Tile Layers
            <v-btn icon class="ml-2" @click="AddTileLayer">
              <v-icon>add</v-icon>
            </v-btn>
          </v-subheader>
          <v-simple-table>
            <template v-slot:default>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>URL</th>
                  <th>Attribution</th>
                  <th>Min Zoom</th>
                  <th>Max Zoom</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(layer, idx) in theConfig.TileLayers" :key="'tile-' + idx">
                  <td><v-text-field v-model="layer.Name" dense></v-text-field></td>
                  <td><v-text-field v-model="layer.Url" dense></v-text-field></td>
                  <td><v-text-field v-model="layer.Attribution" dense></v-text-field></td>
                  <td><v-text-field v-model.number="layer.MinZoom" type="number" dense style="max-width: 100px"></v-text-field></td>
                  <td><v-text-field v-model.number="layer.MaxZoom" type="number" dense style="max-width: 100px"></v-text-field></td>
                  <td>
                    <v-btn icon small @click="DeleteItemFromArray(theConfig.TileLayers, idx)">
                      <v-icon small>delete</v-icon>
                    </v-btn>
                  </td>
                </tr>
              </tbody>
            </template>
          </v-simple-table>

          <!-- Main Layers -->
          <v-subheader class="mt-4">
            Main Layers (Exclusive)
            <v-btn icon class="ml-2" @click="AddMainLayer">
              <v-icon>add</v-icon>
            </v-btn>
          </v-subheader>
          <v-simple-table>
            <template v-slot:default>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Object</th>
                  <th>Variable</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(layer, idx) in theConfig.MainLayers" :key="'main-' + idx">
                  <td><v-text-field v-model="layer.Name" dense></v-text-field></td>
                  <td>
                    <div class="d-flex align-center">
                      <span class="mr-2">{{ ObjectID2Name(layer.Variable?.Object) }}</span>
                      <v-btn icon small @click="SelectObj(layer)">
                        <v-icon small>edit</v-icon>
                      </v-btn>
                    </div>
                  </td>
                  <td>
                    <v-select
                      v-model="layer.Variable.Name"
                      :items="ObjectID2Variables(layer.Variable?.Object)"
                      dense
                    ></v-select>
                  </td>
                  <td>
                    <v-btn icon small @click="DeleteItemFromArray(theConfig.MainLayers, idx)">
                      <v-icon small>delete</v-icon>
                    </v-btn>
                  </td>
                </tr>
              </tbody>
            </template>
          </v-simple-table>

          <!-- Optional Layers -->
          <v-subheader class="mt-4">
            Optional Layers
            <v-btn icon class="ml-2" @click="AddOptionalLayer">
              <v-icon>add</v-icon>
            </v-btn>
          </v-subheader>
          <v-simple-table>
            <template v-slot:default>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Object</th>
                  <th>Variable</th>
                  <th>Selected by Default</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(layer, idx) in theConfig.OptionalLayers" :key="'opt-' + idx">
                  <td><v-text-field v-model="layer.Name" dense></v-text-field></td>
                  <td>
                    <div class="d-flex align-center">
                      <span class="mr-2">{{ ObjectID2Name(layer.Variable?.Object) }}</span>
                      <v-btn icon small @click="SelectObj(layer)">
                        <v-icon small>edit</v-icon>
                      </v-btn>
                    </div>
                  </td>
                  <td>
                    <v-select
                      v-model="layer.Variable.Name"
                      :items="ObjectID2Variables(layer.Variable?.Object)"
                      dense
                    ></v-select>
                  </td>
                  <td>
                    <v-switch v-model="layer.IsSelected" dense></v-switch>
                  </td>
                  <td>
                    <v-btn icon small @click="DeleteItemFromArray(theConfig.OptionalLayers, idx)">
                      <v-icon small>delete</v-icon>
                    </v-btn>
                  </td>
                </tr>
              </tbody>
            </template>
          </v-simple-table>
        </v-card-text>

        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn color="grey darken-1" text @click.native="closeDialog">Cancel</v-btn>
          <v-btn color="primary darken-1" text :disabled="!isItemsOK" @click.native="SaveConfig">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <dlg-object-select v-model="selectObject.show"
      :object-id="selectObject.selectedObjectID"
      :module-id="selectObject.selectedModuleID"
      :allow-config-variables="true"
      :modules="selectObject.modules"
      :type="'JSON'"
      @onselected="selectObject_OK">
    </dlg-object-select>
  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue } from 'vue-property-decorator'
import { ModuleInfo, ObjectMap, Obj, SelectObject, ObjInfo } from './common'
import DlgObjectSelect from '../../components/DlgObjectSelect.vue'
//import TextFieldNullableNumber from '../../../components/TextFieldNullableNumber.vue'
import * as config from './GeoMapConfigTypes'
import * as fast from '../../fast_types'

interface ItemWithVariable {
  Name: string
  Variable: fast.VariableRef
}

interface ItemWithName {
  Name: string  
}

@Component({
  components: {
    DlgObjectSelect,
    //TextFieldNullableNumber,
  },
})
export default class GeoMapConfigDlg extends Vue {

  @Prop({ default() { return [] } }) configuration: config.GeoMapConfig
  @Prop() backendAsync: (request: string, parameters: object) => Promise<any>

  show: boolean = false
  theConfig: config.GeoMapConfig = config.DefaultGeoMapConfig

  objectMap: ObjectMap = {}

  selectObject: SelectObject = {
    show: false,
    modules: [],
    selectedModuleID: '',
    selectedObjectID: '',
  }

  currentVariable: fast.VariableRef = { Object: '', Name: '' }

  resolveDialog: ((v: boolean) => void) = (v) => {}

  async showDialog(): Promise<boolean> {

    const response: {
        ObjectMap: ObjectMap,
        Modules: ModuleInfo[],
      } = await this.backendAsync('GetItemsData', { })

    this.objectMap = response.ObjectMap
    this.selectObject.modules = response.Modules

    const str = JSON.stringify(this.configuration)
    this.theConfig = JSON.parse(str)
    this.show = true

    return new Promise<boolean>((resolve) => {
      this.resolveDialog = resolve
    })
  }

  ObjectID2Name(id: string): string {
    if (id === undefined) { return '' }
    const obj: ObjInfo = this.objectMap[id]
    if (obj === undefined) { return id }
    return obj.Name
  }

  SelectObj(item: ItemWithVariable): void {
    const currObj: string = item.Variable === null || item.Variable.Object === null ? '' : item.Variable.Object
    this.selectObject.selectedModuleID = "CALC"
    this.selectObject.selectedObjectID = currObj
    this.selectObject.show = true
    this.currentVariable = item.Variable
  }

  ObjectID2Variables(id: string): string[] {
    const obj: ObjInfo = this.objectMap[id]
    if (obj === undefined) { return [] }
    return obj.Variables
  }

  DeleteItemFromArray(array: any[], idx: number): void {
    array.splice(idx, 1)
  }

  MoveUpItem(array: ItemWithVariable[], idx: number): void {
    if (idx > 0) {
      const item = array[idx]
      array.splice(idx, 1)
      array.splice(idx - 1, 0, item)
    }
  }

  AddTileLayer(): void {
    const item: config.TileLayer = {
      Name: 'OpenStreetMap',
      Url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      Attribution: 'Map data © <a href="https://openstreetmap.org">OpenStreetMap</a> contributors',
      MinZoom: 10,
      MaxZoom: 19
    }
    this.theConfig.TileLayers.push(item)
  }

  AddMainLayer(): void {
    const item: config.MainLayer = {
      Name: '',
      Type: 'GeoJson',
      Variable: { Object: '', Name: '' }
    }
    this.theConfig.MainLayers.push(item)
  }

  AddOptionalLayer(): void {
    const item: config.OptionalLayer = {
      Name: '',
      Type: 'GeoJson',
      Variable: { Object: '', Name: '' },
      IsSelected: true
    }
    this.theConfig.OptionalLayers.push(item)
  }

  get isItemsOK(): boolean {
    const config = this.theConfig
    const notEmpty = (it: ItemWithName) => it.Name !== ''
    const names = new Set(config.TileLayers.map((it) => it.Name))
    config.MainLayers.forEach((it) => names.add(it.Name))
    config.OptionalLayers.forEach((it) => names.add(it.Name))
    const totalSize = config.TileLayers.length + config.MainLayers.length + config.OptionalLayers.length
    const ok = 
      config.TileLayers.every(notEmpty) && 
      config.MainLayers.every(notEmpty) && 
      config.OptionalLayers.every(notEmpty) && 
      names.size === totalSize
    return ok
  }

  async SaveConfig(): Promise<void> {
    const para = {
      config: this.theConfig,
    }
    try {
      await this.backendAsync('SaveConfig', para)
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
      Variables: obj.Variables,
    }
    this.currentVariable.Object = obj.ID
    if (obj.Variables.length === 1) {
      this.currentVariable.Name = obj.Variables[0]
    }
  }
}

</script>
