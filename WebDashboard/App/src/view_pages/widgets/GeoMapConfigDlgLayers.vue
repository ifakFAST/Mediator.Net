<template>
  <div>
    <v-dialog
      v-model="show"
      max-width="1100px"
      persistent
      @keydown="
        (e: KeyboardEvent) => {
          if (e.keyCode === 27 && !selectObject.show) {
            closeDialog()
          }
        }
      "
    >
      <v-card>
        <v-card-title>
          <span class="text-h5">Configure Map Layers</span>
        </v-card-title>

        <v-card-text>
          <!-- Tile Layers -->
          <h4>
            Tile Layers
            <v-btn
              class="ml-2"
              icon="mdi-plus"
              size="small"
              @click="AddTileLayer"
              variant="text"
            ></v-btn>
          </h4>
          <v-table class="no-padding-table">
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
              <tr
                v-for="(layer, idx) in theConfig.TileLayers"
                :key="'tile-' + idx"
              >
                <td>
                  <v-text-field
                    v-model="layer.Name"
                    style="width: 15ch"
                  ></v-text-field>
                </td>
                <td style="width: 100%">
                  <v-text-field
                    v-model="layer.Url"
                    style="width: 100%"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="layer.Attribution"
                    style="width: 25ch"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model.number="layer.MinZoom"
                    style="width: 7ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model.number="layer.MaxZoom"
                    style="width: 7ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <div class="d-flex">
                    <v-btn
                      class="mr-1"
                      :disabled="idx === 0"
                      icon="mdi-arrow-up"
                      size="small"
                      @click="MoveUpItem(theConfig.TileLayers, idx)"
                      variant="text"
                    ></v-btn>
                    <v-btn
                      icon="mdi-delete"
                      size="small"
                      @click="DeleteItemFromArray(theConfig.TileLayers, idx)"
                      variant="text"
                    ></v-btn>
                  </div>
                </td>
              </tr>
            </tbody>
          </v-table>

          <!-- Static Layers -->
          <h4 class="mt-4">
            Static Layers
            <v-btn
              class="ml-2"
              icon="mdi-plus"
              size="small"
              @click="AddStaticLayer"
              variant="text"
            ></v-btn>
          </h4>
          <v-table class="no-padding-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>File Name</th>
                <th>Selected by Default</th>
                <th>Actions</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(layer, idx) in theConfig.StaticLayers"
                :key="'static-' + idx"
              >
                <td>
                  <v-text-field
                    v-model="layer.Name"
                    style="width: 15ch"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model="layer.FileName"
                    style="width: 35ch"
                  ></v-text-field>
                </td>
                <td>
                  <v-checkbox
                    v-model="layer.IsSelected"
                    style="width: 16ch"
                  ></v-checkbox>
                </td>
                <td>
                  <div class="d-flex">
                    <v-btn
                      class="mr-1"
                      :disabled="idx === 0"
                      icon="mdi-arrow-up"
                      size="small"
                      @click="MoveUpItem(theConfig.StaticLayers, idx)"
                      variant="text"
                    ></v-btn>
                    <v-btn
                      icon="mdi-delete"
                      size="small"
                      @click="DeleteItemFromArray(theConfig.StaticLayers, idx)"
                      variant="text"
                    ></v-btn>
                  </div>
                </td>
                <td style="width: 100%"></td>
              </tr>
            </tbody>
          </v-table>

          <!-- Main Layers -->
          <h4 class="mt-4">
            Main Layers (Exclusive)
            <v-btn
              class="ml-2"
              icon="mdi-plus"
              size="small"
              @click="AddMainLayer"
              variant="text"
            ></v-btn>
          </h4>
          <v-table class="no-padding-table">
            <thead>
              <tr>
                <th>Type</th>
                <th>Name</th>
                <th>Object</th>
                <th>Variable</th>
                <th>Frame Count</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(layer, idx) in theConfig.MainLayers"
                :key="'main-' + idx"
              >
                <td>
                  <v-select
                    v-model="layer.Type"
                    :items="['GeoJson', 'GeoTiff']"
                    style="width: 14ch"
                  ></v-select>
                </td>
                <td>
                  <v-text-field
                    v-model="layer.Name"
                    style="width: 18ch"
                  ></v-text-field>
                </td>
                <td style="width: 100%">
                  <div class="d-flex align-center">
                    <span class="mr-2">{{ ObjectID2Name(layer.Variable?.Object) }}</span>
                    <v-btn
                      icon="mdi-pencil"
                      size="small"
                      @click="SelectObj(layer)"
                      variant="text"
                    ></v-btn>
                  </div>
                </td>
                <td>
                  <v-combobox
                    v-model="layer.Variable.Name"
                    :items="ObjectID2Variables(layer.Variable)"
                    style="width: 15ch"
                  ></v-combobox>
                </td>
                <td>
                  <v-text-field
                    v-model.number="layer.FrameCount"
                    min="1"
                    persistent-hint
                    style="width: 10ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <div class="d-flex">
                    <v-btn
                      class="mr-1"
                      :disabled="idx === 0"
                      icon="mdi-arrow-up"
                      size="small"
                      @click="MoveUpItem(theConfig.MainLayers, idx)"
                      variant="text"
                    ></v-btn>
                    <v-btn
                      icon="mdi-delete"
                      size="small"
                      @click="DeleteItemFromArray(theConfig.MainLayers, idx)"
                      variant="text"
                    ></v-btn>
                  </div>
                </td>
              </tr>
            </tbody>
          </v-table>

          <!-- Optional Layers -->
          <h4 class="mt-4">
            Optional Layers
            <v-btn
              class="ml-2"
              icon="mdi-plus"
              size="small"
              @click="AddOptionalLayer"
              variant="text"
            ></v-btn>
          </h4>
          <v-table class="no-padding-table">
            <thead>
              <tr>
                <th>Type</th>
                <th>Name</th>
                <th>Object</th>
                <th>Variable</th>
                <th>Frame Count</th>
                <th>Selected by Default</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(layer, idx) in theConfig.OptionalLayers"
                :key="'opt-' + idx"
              >
                <td>
                  <v-select
                    v-model="layer.Type"
                    :items="['GeoJson', 'GeoTiff']"
                    style="width: 14ch"
                  ></v-select>
                </td>
                <td>
                  <v-text-field
                    v-model="layer.Name"
                    style="width: 18ch"
                  ></v-text-field>
                </td>
                <td style="width: 100%">
                  <div class="d-flex align-center">
                    <span class="mr-2">{{ ObjectID2Name(layer.Variable?.Object) }}</span>
                    <v-btn
                      icon="mdi-pencil"
                      size="small"
                      @click="SelectObj(layer)"
                      variant="text"
                    ></v-btn>
                  </div>
                </td>
                <td>
                  <v-combobox
                    v-model="layer.Variable.Name"
                    :items="ObjectID2Variables(layer.Variable)"
                    style="width: 15ch"
                  ></v-combobox>
                </td>
                <td>
                  <v-text-field
                    v-model.number="layer.FrameCount"
                    min="1"
                    persistent-hint
                    style="width: 10ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-checkbox
                    v-model="layer.IsSelected"
                    style="width: 10ch"
                  ></v-checkbox>
                </td>
                <td>
                  <div class="d-flex">
                    <v-btn
                      class="mr-1"
                      :disabled="idx === 0"
                      icon="mdi-arrow-up"
                      size="small"
                      @click="MoveUpItem(theConfig.OptionalLayers, idx)"
                      variant="text"
                    ></v-btn>
                    <v-btn
                      icon="mdi-delete"
                      size="small"
                      @click="DeleteItemFromArray(theConfig.OptionalLayers, idx)"
                      variant="text"
                    ></v-btn>
                  </div>
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>

        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn
            color="grey-darken-1"
            variant="text"
            @click="closeDialog"
          >
            Cancel
          </v-btn>
          <v-btn
            color="primary-darken-1"
            :disabled="!isItemsOK"
            variant="text"
            @click="SaveConfig"
          >
            Save
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <dlg-object-select
      v-model="selectObject.show"
      :allow-config-variables="true"
      :module-id="selectObject.selectedModuleID"
      :modules="selectObject.modules"
      :object-id="selectObject.selectedObjectID"
      :type="'JSON'"
      @onselected="selectObject_OK"
    ></dlg-object-select>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ModuleInfo, ObjectMap, Obj, Variable, SelectObject, ObjInfo } from './common'
import DlgObjectSelect from '../../components/DlgObjectSelect.vue'
import * as config from './GeoMapConfigTypes'
import type * as fast from '../../fast_types'

interface ItemWithVariable {
  Name: string
  Variable: fast.VariableRef
}

interface ItemWithName {
  Name: string
}

// Props
interface Props {
  configuration?: config.GeoMapConfig
  backendAsync?: (request: string, parameters: object) => Promise<any>
}

const props = withDefaults(defineProps<Props>(), {
  configuration: () => ({}) as config.GeoMapConfig,
})

// Reactive data
const show = ref(false)
const theConfig = ref<config.GeoMapConfig>(config.DefaultGeoMapConfig)
const objectMap = ref<ObjectMap>({})
const selectObject = ref<SelectObject>({
  show: false,
  modules: [],
  selectedModuleID: '',
  selectedObjectID: '',
})
const currentVariable = ref<fast.VariableRef>({ Object: '', Name: '' })
let resolveDialog: (v: boolean) => void = () => {}

// Computed
const isItemsOK = computed(() => {
  const configValue = theConfig.value
  const nameNotEmpty = (it: ItemWithName) => it.Name !== ''
  const fileNameNotEmpty = (it: config.StaticLayer) => it.FileName !== ''
  const names = new Set(configValue.TileLayers.map((it) => it.Name))
  configValue.StaticLayers.forEach((it) => names.add(it.Name))
  configValue.MainLayers.forEach((it) => names.add(it.Name))
  configValue.OptionalLayers.forEach((it) => names.add(it.Name))
  const totalSize =
    configValue.TileLayers.length + configValue.StaticLayers.length + configValue.MainLayers.length + configValue.OptionalLayers.length
  const ok =
    configValue.TileLayers.every(nameNotEmpty) &&
    configValue.StaticLayers.every(nameNotEmpty) &&
    configValue.MainLayers.every(nameNotEmpty) &&
    configValue.OptionalLayers.every(nameNotEmpty) &&
    names.size === totalSize &&
    configValue.StaticLayers.every(fileNameNotEmpty)
  return ok
})

// Methods
const showDialog = async (): Promise<boolean> => {
  const response: {
    ObjectMap: ObjectMap
    Modules: ModuleInfo[]
  } = await props.backendAsync!('GetItemsData', {})

  objectMap.value = response.ObjectMap
  selectObject.value.modules = response.Modules

  const str = JSON.stringify(props.configuration)
  theConfig.value = JSON.parse(str)
  show.value = true

  return new Promise<boolean>((resolve) => {
    resolveDialog = resolve
  })
}

const ObjectID2Name = (id: string): string => {
  if (id === undefined) {
    return ''
  }
  const obj: ObjInfo = objectMap.value[id]
  if (obj === undefined) {
    return id
  }
  return obj.Name
}

const SelectObj = (item: ItemWithVariable): void => {
  const currObj: string = item.Variable === null || item.Variable.Object === null ? '' : item.Variable.Object
  selectObject.value.selectedModuleID = 'CALC'
  selectObject.value.selectedObjectID = currObj
  selectObject.value.show = true
  currentVariable.value = item.Variable
}

const ObjectID2Variables = (id?: Variable): string[] => {
  if (id === undefined) {
    return []
  }
  const obj: ObjInfo = objectMap.value[id.Object]
  if (obj === undefined) {
    if (id.Name === '') {
      return []
    }
    return [id.Name]
  }
  return obj.Variables
}

const DeleteItemFromArray = (array: any[], idx: number): void => {
  array.splice(idx, 1)
}

const MoveUpItem = (array: object[], idx: number): void => {
  if (idx > 0) {
    const item = array[idx]
    array.splice(idx, 1)
    array.splice(idx - 1, 0, item)
  }
}

const AddTileLayer = (): void => {
  const item: config.TileLayer = {
    Name: 'OpenStreetMap',
    Url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
    Attribution: 'Map data © <a href="https://openstreetmap.org">OpenStreetMap</a> contributors',
    MinZoom: 10,
    MaxZoom: 19,
  }
  theConfig.value.TileLayers.push(item)
}

const AddMainLayer = (): void => {
  const item: config.MainLayer = {
    Name: '',
    Type: 'GeoJson',
    Variable: { Object: '', Name: '' },
    FrameCount: 1,
  }
  theConfig.value.MainLayers.push(item)
}

const AddStaticLayer = (): void => {
  const item: config.StaticLayer = {
    Name: '',
    FileName: '',
    IsSelected: false,
  }
  theConfig.value.StaticLayers.push(item)
}

const AddOptionalLayer = (): void => {
  const item: config.OptionalLayer = {
    Name: '',
    Type: 'GeoJson',
    Variable: { Object: '', Name: '' },
    IsSelected: true,
    FrameCount: 1,
  }
  theConfig.value.OptionalLayers.push(item)
}

const SaveConfig = async (): Promise<void> => {
  const para = {
    config: theConfig.value,
  }
  try {
    await props.backendAsync!('SaveConfig', para)
  } catch (exp) {
    alert(exp)
    return
  }
  show.value = false
  resolveDialog(true)
}

const closeDialog = (): void => {
  show.value = false
  resolveDialog(false)
}

const selectObject_OK = (obj: any): void => {
  objectMap.value[obj.ID] = {
    Name: obj.Name,
    Variables: obj.Variables,
  }
  currentVariable.value.Object = obj.ID
  if (obj.Variables.length === 1) {
    currentVariable.value.Name = obj.Variables[0]
  }
}

// Expose methods for parent component
defineExpose({
  showDialog,
})
</script>
