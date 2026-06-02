<template>
  <div>
    <v-dialog
      v-model="show"
      max-width="1300px"
      persistent
      @keydown="
        (e: KeyboardEvent) => {
          if (e.keyCode === 27 && !selectObject.show && !colorMapEditor.show) {
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
                <th>Opacity</th>
                <th>Color Map</th>
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
                  <v-text-field
                    v-if="layer.Type === 'GeoTiff'"
                    :model-value="layer.Opacity ?? ''"
                    max="1"
                    min="0"
                    step="0.01"
                    style="width: 10ch"
                    type="number"
                    @update:model-value="SetLayerOpacity(layer, $event)"
                  ></v-text-field>
                  <span
                    v-else
                    class="text-medium-emphasis"
                  >
                    -
                  </span>
                </td>
                <td>
                  <div class="d-flex align-center">
                    <v-tooltip
                      v-if="layer.Type === 'GeoTiff'"
                      text="Edit color map"
                    >
                      <template #activator="{ props: tooltipProps }">
                        <v-btn
                          v-bind="tooltipProps"
                          icon="mdi-palette"
                          size="small"
                          @click="OpenColorMapEditor(layer)"
                          variant="text"
                        ></v-btn>
                      </template>
                    </v-tooltip>
                    <span
                      v-if="layer.Type === 'GeoTiff'"
                      class="ml-1 text-medium-emphasis"
                      style="min-width: 2ch"
                    >
                      {{ colorMapCount(layer) }}
                    </span>
                    <span
                      v-else
                      class="text-medium-emphasis"
                    >
                      -
                    </span>
                  </div>
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
                <th>Opacity</th>
                <th>Color Map</th>
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
                  <v-text-field
                    v-if="layer.Type === 'GeoTiff'"
                    :model-value="layer.Opacity ?? ''"
                    max="1"
                    min="0"
                    step="0.01"
                    style="width: 10ch"
                    type="number"
                    @update:model-value="SetLayerOpacity(layer, $event)"
                  ></v-text-field>
                  <span
                    v-else
                    class="text-medium-emphasis"
                  >
                    -
                  </span>
                </td>
                <td>
                  <div class="d-flex align-center">
                    <v-tooltip
                      v-if="layer.Type === 'GeoTiff'"
                      text="Edit color map"
                    >
                      <template #activator="{ props: tooltipProps }">
                        <v-btn
                          v-bind="tooltipProps"
                          icon="mdi-palette"
                          size="small"
                          @click="OpenColorMapEditor(layer)"
                          variant="text"
                        ></v-btn>
                      </template>
                    </v-tooltip>
                    <span
                      v-if="layer.Type === 'GeoTiff'"
                      class="ml-1 text-medium-emphasis"
                      style="min-width: 2ch"
                    >
                      {{ colorMapCount(layer) }}
                    </span>
                    <span
                      v-else
                      class="text-medium-emphasis"
                    >
                      -
                    </span>
                  </div>
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

    <v-dialog
      v-model="colorMapEditor.show"
      max-width="760px"
      persistent
      @keydown.stop="
        (e: KeyboardEvent) => {
          if (e.keyCode === 27) {
            CloseColorMapEditor()
          }
        }
      "
    >
      <v-card>
        <v-card-title>
          <span class="text-h5">Color Map</span>
          <span class="ml-2 text-subtitle-1 text-medium-emphasis">{{ colorMapEditor.layerName }}</span>
        </v-card-title>

        <v-card-text>
          <div class="d-flex mb-2">
            <v-tooltip text="Add range">
              <template #activator="{ props: tooltipProps }">
                <v-btn
                  v-bind="tooltipProps"
                  class="mr-1"
                  icon="mdi-plus"
                  size="small"
                  @click="AddColorMapRange"
                  variant="text"
                ></v-btn>
              </template>
            </v-tooltip>
            <v-tooltip text="Copy CSV">
              <template #activator="{ props: tooltipProps }">
                <v-btn
                  v-bind="tooltipProps"
                  class="mr-1"
                  icon="mdi-content-copy"
                  size="small"
                  @click="CopyColorMapAsCsvToClipboard"
                  variant="text"
                ></v-btn>
              </template>
            </v-tooltip>
            <v-tooltip text="Paste CSV">
              <template #activator="{ props: tooltipProps }">
                <v-btn
                  v-bind="tooltipProps"
                  class="mr-1"
                  icon="mdi-clipboard-text"
                  size="small"
                  @click="ReplaceColorMapFromCsvFromClipboard"
                  variant="text"
                ></v-btn>
              </template>
            </v-tooltip>
            <v-tooltip text="Clear ranges">
              <template #activator="{ props: tooltipProps }">
                <v-btn
                  v-bind="tooltipProps"
                  icon="mdi-delete-sweep"
                  size="small"
                  @click="ClearColorMapRanges"
                  variant="text"
                ></v-btn>
              </template>
            </v-tooltip>
          </div>

          <v-table class="no-padding-table">
            <thead>
              <tr>
                <th>Start</th>
                <th>End</th>
                <th>Color</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(range, idx) in colorMapEditor.ranges"
                :key="'color-map-' + idx"
              >
                <td>
                  <v-text-field
                    v-model.number="range.Start"
                    style="width: 12ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td>
                  <v-text-field
                    v-model.number="range.End"
                    style="width: 12ch"
                    type="number"
                  ></v-text-field>
                </td>
                <td style="width: 100%">
                  <div class="d-flex align-center">
                    <input
                      class="geomap-color-map-swatch mr-2"
                      type="color"
                      :value="colorPickerValue(range.Color)"
                      @input="SetColorMapRangeColor(range, $event)"
                    />
                    <v-text-field
                      v-model="range.Color"
                      style="width: 100%"
                    ></v-text-field>
                  </div>
                </td>
                <td>
                  <div class="d-flex">
                    <v-btn
                      class="mr-1"
                      :disabled="idx === 0"
                      icon="mdi-arrow-up"
                      size="small"
                      @click="MoveUpItem(colorMapEditor.ranges, idx)"
                      variant="text"
                    ></v-btn>
                    <v-btn
                      icon="mdi-delete"
                      size="small"
                      @click="DeleteItemFromArray(colorMapEditor.ranges, idx)"
                      variant="text"
                    ></v-btn>
                  </div>
                </td>
              </tr>
              <tr v-if="colorMapEditor.ranges.length === 0">
                <td
                  class="text-medium-emphasis"
                  colspan="4"
                >
                  No ranges configured
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
            @click="CloseColorMapEditor"
          >
            Cancel
          </v-btn>
          <v-btn
            color="primary-darken-1"
            variant="text"
            @click="SaveColorMapEditor"
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

interface ColorMapEditor {
  show: boolean
  layer: config.NamedLayerType | null
  layerName: string
  ranges: config.ColorMapRange[]
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
const colorMapEditor = ref<ColorMapEditor>({
  show: false,
  layer: null,
  layerName: '',
  ranges: [],
})
let resolveDialog: (v: boolean) => void = () => {}

const normalizeColorMapRange = (range: any): config.ColorMapRange => ({
  Start: Number(range.Start ?? range.start ?? 0),
  End: Number(range.End ?? range.end ?? 0),
  Color: String(range.Color ?? range.color ?? ''),
})

const normalizeLayerColorMap = (layer: config.NamedLayerType): void => {
  const rawColorMap = (layer as any).ColorMap ?? (layer as any).colorMap
  if (Array.isArray(rawColorMap) && rawColorMap.length > 0) {
    layer.ColorMap = rawColorMap.map(normalizeColorMapRange)
  } else {
    delete layer.ColorMap
  }
  delete (layer as any).colorMap
}

const normalizeLayerGeoTiffConfig = (layer: config.NamedLayerType): void => {
  normalizeLayerColorMap(layer)

  const rawOpacity = (layer as any).Opacity ?? (layer as any).opacity
  if (rawOpacity === undefined || rawOpacity === null || rawOpacity === '') {
    delete layer.Opacity
  } else {
    layer.Opacity = Number(rawOpacity)
  }
  delete (layer as any).opacity
}

const normalizeConfiguredGeoTiffConfig = (configValue: config.GeoMapConfig): void => {
  configValue.MainLayers?.forEach(normalizeLayerGeoTiffConfig)
  configValue.OptionalLayers?.forEach(normalizeLayerGeoTiffConfig)
}

const isFiniteNumber = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value)

const isColorMapRangeOK = (range: config.ColorMapRange): boolean => {
  return isFiniteNumber(range.Start) && isFiniteNumber(range.End) && range.Start < range.End && range.Color.trim() !== ''
}

const isLayerColorMapOK = (layer: config.NamedLayerType): boolean => {
  return layer.Type !== 'GeoTiff' || !layer.ColorMap || layer.ColorMap.every(isColorMapRangeOK)
}

const isLayerOpacityOK = (layer: config.NamedLayerType): boolean => {
  return layer.Type !== 'GeoTiff' || layer.Opacity === undefined || (isFiniteNumber(layer.Opacity) && layer.Opacity >= 0 && layer.Opacity <= 1)
}

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
    configValue.MainLayers.every(isLayerColorMapOK) &&
    configValue.OptionalLayers.every(isLayerColorMapOK) &&
    configValue.MainLayers.every(isLayerOpacityOK) &&
    configValue.OptionalLayers.every(isLayerOpacityOK) &&
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
  normalizeConfiguredGeoTiffConfig(theConfig.value)
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

const colorMapCount = (layer: config.NamedLayerType): number => {
  return layer.ColorMap?.length ?? 0
}

const SetLayerOpacity = (layer: config.NamedLayerType, value: unknown): void => {
  if (value === undefined || value === null || value === '') {
    delete layer.Opacity
    return
  }

  layer.Opacity = Number(value)
}

const OpenColorMapEditor = (layer: config.NamedLayerType): void => {
  if (layer.Type !== 'GeoTiff') {
    return
  }

  normalizeLayerColorMap(layer)
  colorMapEditor.value = {
    show: true,
    layer,
    layerName: layer.Name,
    ranges: (layer.ColorMap ?? []).map((range) => ({ ...range })),
  }
}

const CloseColorMapEditor = (): void => {
  colorMapEditor.value.show = false
}

const AddColorMapRange = (): void => {
  const ranges = colorMapEditor.value.ranges
  const previousEnd = ranges.length > 0 ? Number(ranges[ranges.length - 1].End) : 0
  const start = Number.isFinite(previousEnd) ? previousEnd : 0
  ranges.push({
    Start: start,
    End: start + 1,
    Color: '#000000',
  })
}

const ClearColorMapRanges = (): void => {
  colorMapEditor.value.ranges = []
}

const colorPickerValue = (color: string): string => {
  const normalized = color.trim()
  if (/^#[0-9a-fA-F]{6}$/.test(normalized)) {
    return normalized
  }
  const shortHex = normalized.match(/^#([0-9a-fA-F])([0-9a-fA-F])([0-9a-fA-F])$/)
  if (shortHex) {
    return `#${shortHex[1]}${shortHex[1]}${shortHex[2]}${shortHex[2]}${shortHex[3]}${shortHex[3]}`
  }
  return '#000000'
}

const SetColorMapRangeColor = (range: config.ColorMapRange, event: Event): void => {
  const input = event.target as HTMLInputElement | null
  if (input) {
    range.Color = input.value
  }
}

const escapeCsvValue = (value: string | number): string => {
  const text = String(value)
  if (/[",\r\n]/.test(text)) {
    return `"${text.replace(/"/g, '""')}"`
  }
  return text
}

const parseCsvRows = (csvData: string): string[][] => {
  const rows: string[][] = []
  let row: string[] = []
  let value = ''
  let inQuotes = false

  for (let i = 0; i < csvData.length; i++) {
    const ch = csvData[i]
    if (inQuotes) {
      if (ch === '"' && csvData[i + 1] === '"') {
        value += '"'
        i++
      } else if (ch === '"') {
        inQuotes = false
      } else {
        value += ch
      }
    } else if (ch === '"') {
      inQuotes = true
    } else if (ch === ',') {
      row.push(value)
      value = ''
    } else if (ch === '\r' || ch === '\n') {
      row.push(value)
      rows.push(row)
      row = []
      value = ''
      if (ch === '\r' && csvData[i + 1] === '\n') {
        i++
      }
    } else {
      value += ch
    }
  }

  if (value !== '' || row.length > 0) {
    row.push(value)
    rows.push(row)
  }

  return rows.filter((it) => it.some((value) => value.trim() !== ''))
}

const parseColorMapCsv = (csvData: string): config.ColorMapRange[] => {
  const rows = parseCsvRows(csvData)
  if (rows.length === 0) {
    return []
  }

  const header = rows[0].map((value) => value.trim().toLowerCase())
  const hasHeader = header.includes('start') && header.includes('end') && header.includes('color')
  const startIdx = hasHeader ? header.indexOf('start') : 0
  const endIdx = hasHeader ? header.indexOf('end') : 1
  const colorIdx = hasHeader ? header.indexOf('color') : 2
  const dataRows = hasHeader ? rows.slice(1) : rows

  return dataRows.map((parts, idx) => {
    const startText = parts[startIdx]?.trim() ?? ''
    const endText = parts[endIdx]?.trim() ?? ''
    const colorText = parts[colorIdx]?.trim() ?? ''
    const range: config.ColorMapRange = {
      Start: Number(startText),
      End: Number(endText),
      Color: colorText,
    }
    if (startText === '' || endText === '' || colorText === '') {
      throw new Error(`Invalid color map CSV row ${idx + (hasHeader ? 2 : 1)}`)
    }
    if (!isColorMapRangeOK(range)) {
      throw new Error(`Invalid color map CSV row ${idx + (hasHeader ? 2 : 1)}`)
    }
    return range
  })
}

const CopyColorMapAsCsvToClipboard = (): void => {
  const header = 'Start,End,Color'
  const csvData = colorMapEditor.value.ranges.map((range) => [range.Start, range.End, range.Color].map(escapeCsvValue).join(',')).join('\r\n')

  navigator.clipboard
    .writeText(csvData === '' ? header : header + '\r\n' + csvData)
    .then(() => {
      alert('CSV data copied to clipboard')
    })
    .catch((error) => {
      alert('Failed to copy CSV data to clipboard: ' + error)
    })
}

const ReplaceColorMapFromCsvFromClipboard = (): void => {
  navigator.clipboard
    .readText()
    .then((csvData) => {
      colorMapEditor.value.ranges = parseColorMapCsv(csvData)
    })
    .catch((error) => {
      alert('Failed to paste CSV data from clipboard: ' + error)
    })
}

const SaveColorMapEditor = (): void => {
  const layer = colorMapEditor.value.layer
  if (!layer) {
    CloseColorMapEditor()
    return
  }

  const ranges = colorMapEditor.value.ranges.map((range) => ({ ...range, Color: range.Color.trim() }))
  const invalidIdx = ranges.findIndex((range) => !isColorMapRangeOK(range))
  if (invalidIdx >= 0) {
    alert(`Invalid color map range at row ${invalidIdx + 1}`)
    return
  }

  if (ranges.length > 0) {
    layer.ColorMap = ranges
  } else {
    delete layer.ColorMap
  }
  delete (layer as any).colorMap
  CloseColorMapEditor()
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

<style scoped>
.geomap-color-map-swatch {
  width: 32px;
  height: 32px;
  min-width: 32px;
  padding: 0;
  border: 0;
  background: transparent;
}
</style>
