<template>
  <div>
    <v-dialog
      v-model="showDialog"
      max-width="850px"
      persistent
    >
      <v-card>
        <v-card-title>Configure XY Plot</v-card-title>
        <v-card-text>
          <v-tabs v-model="configTab">
            <v-tab value="series">Data Series</v-tab>
            <v-tab value="plot">Plot Settings</v-tab>
          </v-tabs>

          <v-window v-model="configTab">
            <v-window-item value="series">
              <v-container fluid>
                <v-row style="height: 500px">
                  <!-- Master: Series List -->
                  <v-col
                    cols="5"
                    class="pr-4"
                    style="border-right: 1px solid #e0e0e0; height: 100%; overflow-y: auto"
                  >
                    <div class="d-flex justify-space-between align-center mb-3">
                      <h3>Data Series</h3>
                      <div class="d-flex gap-2">
                        <v-btn
                          size="small"
                          @click="moveSeriesUp"
                          variant="text"
                          icon="mdi-arrow-up"
                          :disabled="selectedSeriesIndex <= 0"
                        ></v-btn>
                        <v-btn
                          color="primary"
                          size="small"
                          @click="addSeries"
                          variant="text"
                          icon="mdi-plus"
                        ></v-btn>
                      </div>
                    </div>

                    <v-list
                      density="compact"
                      class="pa-0"
                    >
                      <v-list-item
                        v-for="(series, idx) in editConfig.DataSeries"
                        :key="idx"
                        :active="selectedSeriesIndex === idx"
                        @click="selectedSeriesIndex = idx"
                        class="mb-1"
                        style="border: 1px solid #e0e0e0; border-radius: 4px"
                      >
                        <v-list-item-title>
                          <div class="d-flex align-center">
                            <div
                              style="width: 24px; height: 24px; border-radius: 4px; margin-right: 8px"
                              :style="{ backgroundColor: series.Color }"
                            ></div>
                            <span>{{ series.Name || '(unnamed)' }}</span>
                          </div>
                        </v-list-item-title>

                        <template #append>
                          <v-btn
                            icon="mdi-delete"
                            size="x-small"
                            variant="text"
                            @click.stop="removeSeries(idx)"
                          ></v-btn>
                        </template>
                      </v-list-item>
                    </v-list>

                    <v-alert
                      v-if="editConfig.DataSeries.length === 0"
                      type="info"
                      variant="tonal"
                      class="mt-4"
                    >
                      Click the + button to add a data series
                    </v-alert>
                  </v-col>

                  <!-- Detail: Selected Series Properties -->
                  <v-col
                    cols="7"
                    style="height: 100%; overflow-y: auto"
                  >
                    <div v-if="selectedSeries">
                      <h3 class="mb-4">Series Properties</h3>

                      <!-- Basic Properties -->
                      <v-card
                        variant="outlined"
                        class="mb-4"
                      >
                        <v-card-title class="text-subtitle-1 bg-grey-lighten-4">Basic Properties</v-card-title>
                        <v-card-text>
                          <v-text-field
                            v-model="selectedSeries.Name"
                            label="Name"
                            class="mb-3"
                          ></v-text-field>

                          <v-row>
                            <v-col cols="7">
                              <div class="d-flex align-center">
                                <v-menu offset-y>
                                  <template #activator="{ props }">
                                    <v-btn
                                      v-bind="props"
                                      class="mr-3"
                                      style="min-width: 40px; width: 40px; height: 40px"
                                      :style="{ backgroundColor: selectedSeries.Color }"
                                    ></v-btn>
                                  </template>
                                  <div>
                                    <div
                                      v-for="(color, index) in colorList"
                                      :key="index"
                                      @click="selectedSeries.Color = color"
                                    >
                                      <div
                                        style="padding: 6px; cursor: pointer"
                                        :style="{ backgroundColor: color }"
                                      >
                                        {{ color }}
                                      </div>
                                    </div>
                                  </div>
                                </v-menu>
                                <v-text-field
                                  v-model="selectedSeries.Color"
                                  label="Color"
                                ></v-text-field>
                              </div>
                            </v-col>
                            <v-col cols="5">
                              <v-text-field
                                v-model.number="selectedSeries.Size"
                                label="Point Size"
                                type="number"
                              ></v-text-field>
                            </v-col>
                          </v-row>

                          <v-checkbox
                            v-model="selectedSeries.Checked"
                            label="Visible"
                            class="mt-2"
                          ></v-checkbox>
                        </v-card-text>
                      </v-card>

                      <!-- Data Sources -->
                      <v-card
                        variant="outlined"
                        class="mb-4"
                      >
                        <v-card-title class="text-subtitle-1 bg-grey-lighten-4">Data Sources</v-card-title>
                        <v-card-text>
                          <div class="mb-4">
                            <label class="text-subtitle-2 mb-2 d-block">X Axis Variable</label>
                            <div class="d-flex align-center">
                              <v-text-field
                                :model-value="getObjectName(selectedSeries.VariableX.Object)"
                                label="Object"
                                readonly
                                class="mr-2"
                              ></v-text-field>
                              <v-btn
                                icon="mdi-pencil"
                                size="small"
                                @click="selectObjectForVariable(selectedSeries.VariableX, 'X', selectedSeriesIndex)"
                              ></v-btn>
                            </div>
                            <v-combobox
                              v-if="shouldShowVariableColumn(selectedSeries.VariableX.Object)"
                              v-model="selectedSeries.VariableX.Name"
                              :items="getVariablesForObject(selectedSeries.VariableX.Object)"
                              label="Variable"
                              class="mt-2"
                            ></v-combobox>
                          </div>

                          <div>
                            <label class="text-subtitle-2 mb-2 d-block">Y Axis Variable</label>
                            <div class="d-flex align-center">
                              <v-text-field
                                :model-value="getObjectName(selectedSeries.VariableY.Object)"
                                label="Object"
                                readonly
                                class="mr-2"
                              ></v-text-field>
                              <v-btn
                                icon="mdi-pencil"
                                size="small"
                                @click="selectObjectForVariable(selectedSeries.VariableY, 'Y', selectedSeriesIndex)"
                              ></v-btn>
                            </div>
                            <v-combobox
                              v-if="shouldShowVariableColumn(selectedSeries.VariableY.Object)"
                              v-model="selectedSeries.VariableY.Name"
                              :items="getVariablesForObject(selectedSeries.VariableY.Object)"
                              label="Variable"
                              class="mt-2"
                            ></v-combobox>
                          </div>
                        </v-card-text>
                      </v-card>

                      <!-- Aggregation -->
                      <v-card
                        variant="outlined"
                        class="mb-4"
                      >
                        <v-card-title class="text-subtitle-1 bg-grey-lighten-4">Aggregation</v-card-title>
                        <v-card-text>
                          <v-row>
                            <v-col cols="6">
                              <v-select
                                v-model="selectedSeries.Aggregation"
                                :items="['None', 'Average', 'First', 'Last']"
                                label="Aggregation Type"
                              ></v-select>
                            </v-col>
                            <v-col cols="6">
                              <v-text-field
                                v-model="selectedSeries.Resolution"
                                label="Resolution"
                                :disabled="selectedSeries.Aggregation === 'None'"
                              ></v-text-field>
                            </v-col>
                          </v-row>
                        </v-card-text>
                      </v-card>

                      <!-- Time Highlighting -->
                      <v-card
                        variant="outlined"
                        class="mb-4"
                      >
                        <v-card-title class="text-subtitle-1 bg-grey-lighten-4">Time Highlighting</v-card-title>
                        <v-card-text>
                          <v-row>
                            <v-col cols="6">
                              <v-select
                                v-model="selectedSeries.TimeHighlighting"
                                :items="timeHighlightingOptions"
                                label="Type"
                              ></v-select>
                            </v-col>
                            <v-col cols="6">
                              <v-text-field
                                v-if="selectedSeries.TimeHighlighting !== 'None'"
                                v-model.number="selectedSeries.TimeHighlightingLastN"
                                label="Last N Points"
                                type="number"
                                min="1"
                              ></v-text-field>
                            </v-col>
                          </v-row>
                          <div v-if="selectedSeries.TimeHighlighting !== 'None'">
                            <label class="text-subtitle-2 mb-2 mt-2 d-block">Highlight Color</label>
                            <div class="d-flex align-center">
                              <v-menu offset-y>
                                <template #activator="{ props }">
                                  <v-btn
                                    v-bind="props"
                                    class="mr-3"
                                    style="min-width: 40px; width: 40px; height: 40px"
                                    :style="{ backgroundColor: selectedSeries.TimeHighlightingColor }"
                                  ></v-btn>
                                </template>
                                <div>
                                  <div
                                    v-for="(color, index) in colorList"
                                    :key="`th-color-${index}`"
                                    @click="selectedSeries.TimeHighlightingColor = color"
                                  >
                                    <div
                                      style="padding: 6px; cursor: pointer"
                                      :style="{ backgroundColor: color }"
                                    >
                                      {{ color }}
                                    </div>
                                  </div>
                                </div>
                              </v-menu>
                              <v-text-field
                                v-model="selectedSeries.TimeHighlightingColor"
                                label="Color"
                              ></v-text-field>
                            </div>
                          </div>
                        </v-card-text>
                      </v-card>

                      <!-- Regression -->
                      <v-card
                        variant="outlined"
                        class="mb-4"
                      >
                        <v-card-title class="text-subtitle-1 bg-grey-lighten-4">Regression Line</v-card-title>
                        <v-card-text>
                          <v-select
                            v-model="selectedSeries.ShowRegression"
                            :items="['None', 'Auto']"
                            label="Show Regression"
                          ></v-select>

                          <div v-if="selectedSeries.ShowRegression !== 'None'">
                            <label class="text-subtitle-2 mb-2 mt-2 d-block">Regression Color</label>
                            <div class="d-flex align-center">
                              <v-menu offset-y>
                                <template #activator="{ props }">
                                  <v-btn
                                    v-bind="props"
                                    class="mr-3"
                                    style="min-width: 40px; width: 40px; height: 40px"
                                    :style="{ backgroundColor: selectedSeries.ColorRegression }"
                                  ></v-btn>
                                </template>
                                <div>
                                  <div
                                    v-for="(color, index) in colorList"
                                    :key="`reg-color-${index}`"
                                    @click="selectedSeries.ColorRegression = color"
                                  >
                                    <div
                                      style="padding: 6px; cursor: pointer"
                                      :style="{ backgroundColor: color }"
                                    >
                                      {{ color }}
                                    </div>
                                  </div>
                                </div>
                              </v-menu>
                              <v-text-field
                                v-model="selectedSeries.ColorRegression"
                                label="Color"
                              ></v-text-field>
                            </div>
                          </div>
                        </v-card-text>
                      </v-card>
                    </div>

                    <v-alert
                      v-else
                      type="info"
                      variant="tonal"
                    >
                      Select a series from the list to edit its properties
                    </v-alert>
                  </v-col>
                </v-row>
              </v-container>
            </v-window-item>

            <v-window-item value="plot">
              <v-container fluid>
                <v-row>
                  <v-col cols="6">
                    <v-text-field
                      v-model="editConfig.PlotConfig.XAxisName"
                      label="X Axis Name"
                    ></v-text-field>
                    <v-checkbox
                      v-model="editConfig.PlotConfig.XAxisStartFromZero"
                      label="X Axis Start From Zero"
                    ></v-checkbox>
                    <v-text-field
                      v-model.number="editConfig.PlotConfig.XAxisLimitMin"
                      label="X Axis Min (optional)"
                      type="number"
                    ></v-text-field>
                    <v-text-field
                      v-model.number="editConfig.PlotConfig.XAxisLimitMax"
                      label="X Axis Max (optional)"
                      type="number"
                    ></v-text-field>
                  </v-col>
                  <v-col cols="6">
                    <v-text-field
                      v-model="editConfig.PlotConfig.YAxisName"
                      label="Y Axis Name"
                    ></v-text-field>
                    <v-checkbox
                      v-model="editConfig.PlotConfig.YAxisStartFromZero"
                      label="Y Axis Start From Zero"
                    ></v-checkbox>
                    <v-text-field
                      v-model.number="editConfig.PlotConfig.YAxisLimitMin"
                      label="Y Axis Min (optional)"
                      type="number"
                    ></v-text-field>
                    <v-text-field
                      v-model.number="editConfig.PlotConfig.YAxisLimitMax"
                      label="Y Axis Max (optional)"
                      type="number"
                    ></v-text-field>
                  </v-col>
                </v-row>
                <v-row>
                  <v-col>
                    <v-text-field
                      v-model.number="editConfig.PlotConfig.MaxDataPoints"
                      label="Max Data Points"
                      type="number"
                    ></v-text-field>
                    <v-select
                      v-model="editConfig.PlotConfig.FilterByQuality"
                      :items="['ExcludeNone', 'ExcludeBad', 'ExcludeNonGood']"
                      label="Quality Filter"
                    ></v-select>
                    <v-checkbox
                      v-model="editConfig.PlotConfig.ShowGrid"
                      label="Show Grid"
                    ></v-checkbox>
                    <v-checkbox
                      v-model="editConfig.PlotConfig.ShowLegend"
                      label="Show Legend"
                    ></v-checkbox>
                    <v-checkbox
                      v-model="editConfig.PlotConfig.Show45DegreeLine"
                      label="Show 45° Line"
                    ></v-checkbox>
                    <v-row v-if="editConfig.PlotConfig.Show45DegreeLine">
                      <v-col cols="2">
                        <v-menu offset-y>
                          <template #activator="{ props }">
                            <v-btn
                              v-bind="props"
                              class="ml-2 mr-2"
                              style="min-width: 32px; width: 32px; height: 32px"
                              :style="{ backgroundColor: editConfig.PlotConfig.Color45DegreeLine }"
                            ></v-btn>
                          </template>
                          <div>
                            <div
                              v-for="(color, index) in colorList"
                              :key="`45deg-color-${index}`"
                              @click="editConfig.PlotConfig.Color45DegreeLine = color"
                            >
                              <div
                                style="padding: 6px; cursor: pointer"
                                :style="{ backgroundColor: color }"
                              >
                                {{ color }}
                              </div>
                            </div>
                          </div>
                        </v-menu>
                      </v-col>
                      <v-col cols="10">
                        <v-text-field
                          v-model="editConfig.PlotConfig.Color45DegreeLine"
                          label="45° Line Color"
                          style="width: 200px"
                        ></v-text-field>
                      </v-col>
                    </v-row>
                  </v-col>
                </v-row>
              </v-container>
            </v-window-item>
          </v-window>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn
            color="grey-darken-1"
            variant="text"
            @click="cancel"
          >
            Cancel
          </v-btn>
          <v-btn
            color="primary-darken-1"
            variant="text"
            :disabled="isSaveDisabled"
            @click="save"
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
      @onselected="onObjectSelected"
    ></dlg-object-select>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import DlgObjectSelect from '../../components/DlgObjectSelect.vue'
import type { ModuleInfo, Obj, Variable } from './common'
import type { XyPlotConfig } from './xyPlotTypes'

interface ObjectMap {
  [key: string]: {
    Name: string
    Variables: string[]
  }
}

const showDialog = ref(false)
const configTab = ref('series')
const colorList = ref([
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
  '#000000',
  '#CCCCCC',
])
const timeHighlightingOptions = ['None', 'LastN', 'LastN_Gradient']
const editConfig = ref<XyPlotConfig>({
  PlotConfig: {
    MaxDataPoints: 1000,
    FilterByQuality: 'ExcludeNone',
    XAxisName: '',
    YAxisName: '',
    XAxisStartFromZero: false,
    YAxisStartFromZero: false,
    XAxisLimitMin: null,
    XAxisLimitMax: null,
    YAxisLimitMin: null,
    YAxisLimitMax: null,
    ShowGrid: true,
    ShowLegend: true,
    Show45DegreeLine: true,
    Color45DegreeLine: 'black',
  },
  DataSeries: [],
})
const objectMap = ref<ObjectMap>({})
const selectObject = ref({
  show: false,
  modules: [] as ModuleInfo[],
  selectedModuleID: '',
  selectedObjectID: '',
})
const currentVariable = ref<Variable | null>(null)
const currentVariableType = ref<'X' | 'Y'>('X')
const currentSeriesIndex = ref<number>(0)
const selectedSeriesIndex = ref<number>(-1)

let resolveDialog: (config: XyPlotConfig | null) => void = () => {}

const selectedSeries = computed(() => {
  if (selectedSeriesIndex.value >= 0 && selectedSeriesIndex.value < editConfig.value.DataSeries.length) {
    return editConfig.value.DataSeries[selectedSeriesIndex.value]
  }
  return null
})

const getObjectName = (objectId: string): string => {
  if (!objectId) return '(none)'
  if (objectMap.value[objectId]) {
    return objectMap.value[objectId].Name
  }
  const i = objectId.indexOf(':')
  if (i > 0) {
    return objectId.substring(i + 1)
  }
  return objectId
}

const getVariablesForObject = (objectId: string): string[] => {
  if (!objectId || !objectMap.value[objectId]) {
    return []
  }
  return objectMap.value[objectId].Variables
}

const shouldShowVariableColumn = (objectId: string): boolean => {
  if (!objectId) return false
  const variables = getVariablesForObject(objectId)
  if (variables.length === 0) return false
  if (variables.length === 1 && variables[0] === 'Value') return false
  return true
}

const isSaveDisabled = computed((): boolean => {
  const series = editConfig.value.DataSeries

  // Check if there are any series at all
  if (series.length === 0) {
    return false
  }

  // Check that every series has both X and Y objects selected
  const hasAllObjectsSelected = series.every(
    (s) => s.VariableX.Object && s.VariableX.Object !== '' && s.VariableY.Object && s.VariableY.Object !== '',
  )

  if (!hasAllObjectsSelected) {
    return true
  }

  // Check that every series has a unique non-empty name
  const names = series.map((s) => s.Name.trim()).filter((name) => name !== '')

  // If any series has an empty name, disable save
  if (names.length !== series.length) {
    return true
  }

  // Check for duplicate names
  const uniqueNames = new Set(names)
  if (uniqueNames.size !== names.length) {
    return true
  }

  return false
})

const addSeries = (): void => {
  const newIndex = editConfig.value.DataSeries.length
  editConfig.value.DataSeries.push({
    Name: 'New Series',
    Color: '#' + Math.floor(Math.random() * 16777215).toString(16),
    Size: 3,
    Checked: true,
    VariableX: { Object: '', Name: '' },
    VariableY: { Object: '', Name: '' },
    Aggregation: 'First',
    Resolution: '15 min',
    TimeHighlighting: 'None',
    TimeHighlightingLastN: 1,
    TimeHighlightingColor: 'black',
    ShowRegression: 'None',
    ColorRegression: 'black',
  })
  // Auto-select the newly added series
  selectedSeriesIndex.value = newIndex
}

const removeSeries = (idx: number): void => {
  editConfig.value.DataSeries.splice(idx, 1)

  // Adjust selection after removal
  if (selectedSeriesIndex.value === idx) {
    // If we removed the selected series, select the previous one or the first one
    if (editConfig.value.DataSeries.length > 0) {
      selectedSeriesIndex.value = Math.max(0, idx - 1)
    } else {
      selectedSeriesIndex.value = -1
    }
  } else if (selectedSeriesIndex.value > idx) {
    // If we removed a series before the selected one, adjust the index
    selectedSeriesIndex.value--
  }
}

const moveSeriesUp = (): void => {
  const idx = selectedSeriesIndex.value
  if (idx <= 0 || idx >= editConfig.value.DataSeries.length) {
    return
  }

  // Swap with the previous series
  const temp = editConfig.value.DataSeries[idx]
  editConfig.value.DataSeries[idx] = editConfig.value.DataSeries[idx - 1]
  editConfig.value.DataSeries[idx - 1] = temp

  // Update selection to follow the moved series
  selectedSeriesIndex.value = idx - 1
}

const selectObjectForVariable = (variable: Variable, type: 'X' | 'Y', seriesIdx: number): void => {
  currentVariable.value = variable
  currentVariableType.value = type
  currentSeriesIndex.value = seriesIdx

  const currObj = variable.Object || ''
  let objForModuleID = currObj
  if (objForModuleID === '') {
    const nonEmptySeries = editConfig.value.DataSeries.filter(
      (s) => (s.VariableX.Object && s.VariableX.Object !== '') || (s.VariableY.Object && s.VariableY.Object !== ''),
    )
    if (nonEmptySeries.length > 0) {
      objForModuleID = nonEmptySeries[0].VariableX.Object || nonEmptySeries[0].VariableY.Object || ''
    }
  }

  const i = objForModuleID.indexOf(':')
  if (i <= 0) {
    if (selectObject.value.modules.length > 0) {
      selectObject.value.selectedModuleID = selectObject.value.modules[0].ID
    }
  } else {
    selectObject.value.selectedModuleID = objForModuleID.substring(0, i)
  }
  selectObject.value.selectedObjectID = currObj
  selectObject.value.show = true
}

const onObjectSelected = (obj: Obj): void => {
  const variables = obj.Variables || []

  objectMap.value[obj.ID] = {
    Name: obj.Name,
    Variables: variables,
  }

  if (currentVariable.value) {
    currentVariable.value.Object = obj.ID
    if (variables.length === 1) {
      currentVariable.value.Name = variables[0]
    } else {
      currentVariable.value.Name = ''
    }
  }
}

const save = (): void => {
  resolveDialog(editConfig.value)
  showDialog.value = false
}

const cancel = (): void => {
  resolveDialog(null)
  showDialog.value = false
}

const open = (config: XyPlotConfig, objMap: ObjectMap, modules: ModuleInfo[]): Promise<XyPlotConfig | null> => {
  editConfig.value = JSON.parse(JSON.stringify(config))
  objectMap.value = objMap
  selectObject.value.modules = modules
  configTab.value = 'series'

  // Select the first series by default if available
  selectedSeriesIndex.value = editConfig.value.DataSeries.length > 0 ? 0 : -1

  showDialog.value = true

  return new Promise<XyPlotConfig | null>((resolve) => {
    resolveDialog = resolve
  })
}

defineExpose({
  open,
})
</script>
