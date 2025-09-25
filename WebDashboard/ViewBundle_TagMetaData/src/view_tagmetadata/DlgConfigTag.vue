<template>
  <v-dialog
    v-model="isOpen"
    max-width="700px"
    scrollable
    @keydown="(e: any) => e.key === 'Escape' && close()"
  >
    <v-card>
      <v-card-text>
        <div class="mb-4">
          <span class="text-h5">{{ dialogTitle + ' ' + identifier }}</span>
        </div>

        <div
          class="d-flex align-center mb-4"
          style="gap: 8px"
        >
          <v-text-field
            v-model="sourceTagID"
            label="Source tag identifier (readonly)"
            class="flex-grow-1"
            readonly
          />

          <v-text-field
            v-model="sourceTagName"
            label="Source tag name (readonly)"
            class="flex-grow-1"
            readonly
          />
        </div>
        <div
          class="d-flex align-center mb-2"
          style="gap: 8px; flex-wrap: wrap"
        >
          <div class="text-subtitle-2 mr-0">Unit Group:</div>
          <div class="d-flex flex-wrap overflow-auto">
            <v-btn
              v-for="ug in visibleUnitGroups"
              :key="ug.ID"
              :color="selectedUnitGroup === ug.ID ? 'primary' : undefined"
              :variant="selectedUnitGroup === ug.ID ? 'elevated' : 'text'"
              @click="() => (selectedUnitGroup === ug.ID ? (selectedUnitGroup = '') : (selectedUnitGroup = ug.ID))"
            >
              {{ ug.ID }}
            </v-btn>
          </div>
        </div>

        <div
          class="d-flex align-center mb-2"
          style="gap: 8px; flex-wrap: wrap"
        >
          <div class="text-subtitle-2 mr-3">Category:</div>
          <div class="d-flex flex-wrap overflow-auto">
            <v-btn
              v-for="cat in visibleCategories"
              :key="cat.ID"
              :color="selectedCategory === cat.ID ? 'primary' : undefined"
              :variant="selectedCategory === cat.ID ? 'elevated' : 'text'"
              @click="() => (selectedCategory === cat.ID ? (selectedCategory = '') : (selectedCategory = cat.ID))"
            >
              {{ cat.ID }}
            </v-btn>
          </div>
        </div>

        <div class="d-flex align-center mb-2">
          <v-text-field
            v-model="search"
            append-inner-icon="mdi-magnify"
            label="Search"
            class="ml-auto"
            autofocus
          />
        </div>

        <v-data-table
          v-model="selected"
          class="elevation-1 mt-2 mb-1"
          :headers="headers"
          :items="filteredWhatsSearch"
          :items-per-page="-1"
          return-object
          hide-default-footer
          show-select
          select-strategy="single"
          height="200"
        >
        </v-data-table>

        <!-- Units for selected What -->
        <div
          class="d-flex align-center mt-3"
          style="gap: 8px; flex-wrap: wrap"
        >
          <div class="text-subtitle-2 mr-0">Source Unit:</div>
          <div class="d-flex flex-wrap overflow-auto">
            <v-btn
              v-for="u in selectableUnits"
              :key="u.ID"
              class="text-none"
              :color="selectedUnit === u.ID ? 'primary' : undefined"
              :variant="selectedUnit === u.ID ? 'elevated' : 'text'"
              @click="() => (selectedUnit === u.ID ? (selectedUnit = '') : (selectedUnit = u.ID))"
            >
              {{ u.ID }}
            </v-btn>
            <div
              v-if="selectableUnits.length === 0"
              class="text-caption text-grey ml-1"
            >
              <v-btn
                variant="text"
                class="text-none"
                >Select a row above to choose unit</v-btn
              >
            </div>
          </div>
        </div>

        <div
          v-if="showLocationSelection"
          class="d-flex align-center mt-3"
          style="gap: 8px; flex-wrap: wrap"
        >
          <div class="text-subtitle-2 mr-4">Location:</div>
          <div class="d-flex flex-wrap overflow-auto">
            <v-btn
              v-for="loc in locationOptions"
              :key="loc"
              class="text-none"
              :color="location === loc ? 'primary' : undefined"
              :variant="location === loc ? 'elevated' : 'text'"
              @click="() => (location === loc ? (location = '') : (location = loc))"
            >
              {{ loc }}
            </v-btn>
          </div>
        </div>

        <div
          class="d-flex align-center mt-3"
          style="gap: 8px"
        >
          <v-text-field
            v-model="depthStr"
            label="Depth"
            type="number"
            style="max-width: 130px"
            suffix="m"
            clearable
          />
          <v-text-field
            v-model="notes"
            label="Notes"
            class="flex-grow-1"
          />
        </div>

        <!-- Sampling Configuration -->
        <div class="mt-4">
          <div
            class="d-flex align-center mb-3"
            style="gap: 8px; flex-wrap: wrap"
          >
            <div class="text-subtitle-2 mr-3">Sampling:</div>
            <div class="d-flex flex-wrap overflow-auto">
              <v-btn
                v-for="option in samplingOptions"
                :key="option.value"
                class="text-none mr-1"
                :color="sampling === option.value ? 'primary' : undefined"
                :variant="sampling === option.value ? 'elevated' : 'text'"
                @click="() => (sampling = option.value)"
              >
                {{ option.title }}
              </v-btn>
            </div>
          </div>

          <!-- Details Container - Fixed height to prevent dialog resizing -->
          <div
            class="ml-12 mb-2"
            style="min-height: 56px; position: relative"
          >
            <!-- Sensor Details -->
            <div
              v-if="showSensorDetails"
              class="d-flex align-center"
              style="gap: 8px"
            >
              <v-select
                v-model="sensorType"
                label="Type"
                :items="sensorTypeOptions"
                style="max-width: 150px"
              />

              <v-select
                v-model="sensorPrinciple"
                label="Principle"
                :items="measurementPrincipleOptions"
                class="flex-grow-1"
              />

              <v-text-field
                v-model="sensorT90Str"
                label="T90"
                type="number"
                style="max-width: 100px"
                suffix="min"
              />
            </div>

            <!-- AutoSampler Details -->
            <div
              v-if="showAutoSamplerDetails"
              class="d-flex align-center"
              style="gap: 8px"
            >
              <v-select
                v-model="autoSamplerProportional"
                label="Proportional"
                :items="proportionalTypeOptions"
                style="max-width: 150px"
              />
              <v-text-field
                v-model="autoSamplerIntervalStr"
                label="Interval"
                type="number"
                style="max-width: 100px"
                suffix="h"
              />
              <v-text-field
                v-model="autoSamplerOffsetStr"
                label="Offset"
                type="number"
                style="max-width: 100px"
                suffix="h"
              />
              <v-select
                v-model="autoSamplerTimestampPos"
                label="Timestamp Pos"
                :items="timestampPosOptions"
                style="max-width: 130px"
              />
            </div>
          </div>
        </div>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn
          color="grey-darken-1"
          variant="text"
          @click="close"
        >
          Cancel
        </v-btn>
        <v-btn
          color="primary-darken-1"
          :disabled="selected.length !== 1 || selectedUnit === '' || identifier.trim() === ''"
          variant="text"
          @click="onOK"
        >
          OK
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { ref, computed, watch } from 'vue'
import type { DataTableHeader } from '@/utils'
import * as meta from './metamodel'
import * as model from './model_tags'

// Internal visibility
const isOpen = ref(false)

// Internal inputs configured by open()
const dialogTitle = ref<string>('')
const sourceTagID = ref<string>('')
const sourceTagName = ref<string>('')
const metaModelRef = ref<meta.MetaModel | null>(null)

// Result resolver for async API
export type DialogResult = {
  identifier: string
  what: meta.What
  unit: string
  notes: string
  depth?: number | null
  location?: string
  sampling: model.Sampling
  sensorDetails?: model.SensorDetails | null
  autoSamplerDetails?: model.AutoSamplerDetails | null
}
let resolver: ((res: DialogResult | null) => void) | null = null

// State
const search = ref('')
const selectedUnitGroup = ref<string>('')
const selectedCategory = ref<string>('')
const selected = ref<meta.What[]>([])
const selectedUnit = ref<string>('')
const notes = ref('')
const identifier = ref('')
// Numeric input fields (as strings for controlled input)
const depthStr = ref<string>('')
const location = ref<string>('')
const locationOptions = ref<string[]>([])
const showLocationSelection = computed(() => locationOptions.value.length > 0)

// Sampling fields
const sampling = ref<model.Sampling>(model.Sampling.Sensor)
// Sensor details
const sensorType = ref<model.SensorType>(model.SensorType.InSitu)
const sensorPrinciple = ref<model.MeasurementPrinciple>(model.MeasurementPrinciple.ISE)
const sensorT90Str = ref<string>('0')
// AutoSampler details
const autoSamplerProportional = ref<model.ProportionalType>(model.ProportionalType.Volume)
const autoSamplerIntervalStr = ref<string>('1')
const autoSamplerOffsetStr = ref<string>('0')
const autoSamplerTimestampPos = ref<model.TimestampPos>(model.TimestampPos.Start)

// Data accessors
const unitGroups = computed(() => metaModelRef.value?.UnitGroups ?? [])
const categories = computed(() => metaModelRef.value?.Categories ?? [])
const whats = computed(() => metaModelRef.value?.Whats ?? [])

// Only show buttons for unit groups / categories that have at least one matching "what"
const visibleUnitGroups = computed(() => {
  const cat = selectedCategory.value.trim()
  const idsWithWhats = new Set(whats.value.filter((w) => cat === '' || (w.Category || '') === cat).map((w) => w.UnitGroup || ''))
  return unitGroups.value.filter((ug) => idsWithWhats.has(ug.ID))
})

const visibleCategories = computed(() => {
  const ug = selectedUnitGroup.value.trim()
  const idsWithWhats = new Set(whats.value.filter((w) => ug === '' || (w.UnitGroup || '') === ug).map((w) => w.Category || ''))
  return categories.value.filter((cat) => idsWithWhats.has(cat.ID))
})

// Headers for table
const headers: DataTableHeader[] = [
  { title: 'Short', key: 'ShortName', sortable: true },
  { title: 'Unit Group', key: 'UnitGroup', sortable: true },
  { title: 'Name', key: 'Name', sortable: true },
  { title: 'Category', key: 'Category', sortable: true },
]

// Filtering
const filteredWhats = computed(() => {
  const ug = selectedUnitGroup.value.trim()
  const cat = selectedCategory.value.trim()
  return whats.value.filter((w) => (ug === '' || w.UnitGroup === ug) && (cat === '' || w.Category === cat))
})

// Apply multi-term search (all terms must match)
const filteredWhatsSearch = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (q === '') return filteredWhats.value
  const parts = q.split(' ')
  return filteredWhats.value.filter((w) => {
    const hay = `${w.ShortName || ''} ${w.Name || ''} ${w.UnitGroup || ''} ${w.Category || ''}`.toLowerCase()
    return parts.every((p) => hay.includes(p))
  })
})

watch(filteredWhatsSearch, (newVal) => {
  // If current selection is not in filtered list, clear it
  if (selected.value.length === 1) {
    const cur = selected.value[0]
    if (!newVal.some((w) => w.ID === cur.ID)) {
      selected.value = []
    }
  }
})

// (Dialog initialization handled directly in open())

// Ensure current selections remain valid when visibility changes
watch([visibleUnitGroups, () => selectedUnitGroup.value], () => {
  if (selectedUnitGroup.value !== '' && !visibleUnitGroups.value.some((ug) => ug.ID === selectedUnitGroup.value)) {
    selectedUnitGroup.value = ''
  }
})

watch([visibleCategories, () => selectedCategory.value], () => {
  if (selectedCategory.value !== '' && !visibleCategories.value.some((cat) => cat.ID === selectedCategory.value)) {
    selectedCategory.value = ''
  }
})

const close = (): void => {
  isOpen.value = false
  if (resolver) {
    resolver(null)
    resolver = null
  }
}

const onOK = (): void => {
  if (currentSelectedWhat.value && selectedUnit.value !== '' && identifier.value.trim() !== '') {
    // Convert string inputs to numbers or null
    const toNum = (s: string): number | null => {
      const t = s.trim()
      if (t === '') return null
      const n = Number(t)
      return Number.isFinite(n) ? n : null
    }

    // Build sensor details if sampling is Sensor
    let sensorDetails: model.SensorDetails | null = null
    if (sampling.value === model.Sampling.Sensor) {
      sensorDetails = model.createSensorDetails({
        type: sensorType.value,
        principle: sensorPrinciple.value,
        t90: toNum(sensorT90Str.value) ?? 0.0,
      })
    }

    // Build auto sampler details if sampling is AutoSampler
    let autoSamplerDetails: model.AutoSamplerDetails | null = null
    if (sampling.value === model.Sampling.AutoSampler) {
      autoSamplerDetails = model.createAutoSamplerDetails({
        proportional: autoSamplerProportional.value,
        interval: toNum(autoSamplerIntervalStr.value) ?? 1.0,
        offset: toNum(autoSamplerOffsetStr.value) ?? 0.0,
        timestampPosition: autoSamplerTimestampPos.value,
      })
    }

    const res: DialogResult = {
      identifier: identifier.value.trim(),
      what: currentSelectedWhat.value,
      unit: selectedUnit.value,
      notes: notes.value,
      depth: toNum(depthStr.value),
      location: location.value,
      sampling: sampling.value,
      sensorDetails,
      autoSamplerDetails,
    }
    isOpen.value = false
    if (resolver) {
      resolver(res)
      resolver = null
    }
  }
}

// Computed helpers for current selection
const currentSelectedWhat = computed(() => (selected.value.length === 1 ? selected.value[0] : undefined))

const selectableUnits = computed(() => {
  const ug = currentSelectedWhat.value?.UnitGroup || ''
  if (ug === '') return [] as meta.Unit[]
  return (metaModelRef.value?.Units || []).filter((u) => (u.UnitGroup || '') === ug)
})

// Computed helpers for sampling
const samplingOptions = computed(() => [
  { title: 'Sensor', value: model.Sampling.Sensor },
  { title: 'Grab Sampling', value: model.Sampling.GrabSampling },
  { title: 'Auto Sampler', value: model.Sampling.AutoSampler },
  { title: 'Calculated', value: model.Sampling.Calculated },
])

const sensorTypeOptions = computed(() => [
  { title: 'In Situ', value: model.SensorType.InSitu },
  { title: 'Ex Situ', value: model.SensorType.ExSitu },
])

const measurementPrincipleOptions = computed(() => [
  { title: 'ISE (Ion Selective Electrode)', value: model.MeasurementPrinciple.ISE },
  { title: 'GSE (Galvanic Sensor Electrode)', value: model.MeasurementPrinciple.GSE },
  { title: 'Colorimetric', value: model.MeasurementPrinciple.Colorimetric },
  { title: 'Spectral', value: model.MeasurementPrinciple.Spectral },
])

const proportionalTypeOptions = computed(() => [
  { title: 'Volume', value: model.ProportionalType.Volume },
  { title: 'Time', value: model.ProportionalType.Time },
  { title: 'Flow', value: model.ProportionalType.Flow },
])

const timestampPosOptions = computed(() => [
  { title: 'Start', value: model.TimestampPos.Start },
  { title: 'Middle', value: model.TimestampPos.Middle },
  { title: 'End', value: model.TimestampPos.End },
])

const showSensorDetails = computed(() => sampling.value === model.Sampling.Sensor)
const showAutoSamplerDetails = computed(() => sampling.value === model.Sampling.AutoSampler)

watch(currentSelectedWhat, (w) => {
  if (!w) {
    selectedUnit.value = ''
    return
  }
})

// Watch sampling changes to reset detail fields to defaults when switching types
watch(sampling, (newSampling) => {
  // Reset sensor details to defaults
  sensorType.value = model.SensorType.InSitu
  sensorPrinciple.value = model.MeasurementPrinciple.ISE
  sensorT90Str.value = '0'

  // Reset auto sampler details to defaults
  autoSamplerProportional.value = model.ProportionalType.Volume
  autoSamplerIntervalStr.value = '1'
  autoSamplerOffsetStr.value = '0'
  autoSamplerTimestampPos.value = model.TimestampPos.Start
})

function normalizeLocationOptions(values: string[], current?: string): string[] {
  const cleaned = values.filter((v) => typeof v === 'string' && v.trim() !== '')
  const unique: string[] = []
  for (const val of cleaned) {
    if (!unique.includes(val)) {
      unique.push(val)
    }
  }
  if (current && current.trim() !== '' && !unique.includes(current)) {
    unique.unshift(current)
  }
  return unique
}

// Lookup tag name by ID from backend (best-effort; falls back to empty string)
async function lookupTagNameById(tagID: string): Promise<string> {
  if (!tagID) return ''
  try {
    const resp = await (window.parent as any).dashboardApp.sendViewRequestAsync('GetTagNameFromID', { tagID })
    if (resp && typeof resp.Name === 'string') return resp.Name
  } catch (err) {
    console.error('GetTagNameFromID failed for', tagID, err)
  }
  return ''
}

// Public API: open the dialog and await result
function open(
  title: string,
  srcTagID: string,
  metaModel: meta.MetaModel,
  init?: Partial<DialogResult>,
  tagLocationEnumValues?: string[],
): Promise<DialogResult | null> {
  // ensure any previous pending resolver is cancelled
  if (resolver) {
    try {
      resolver(null)
    } catch {
    } finally {
      resolver = null
    }
  }
  dialogTitle.value = title
  sourceTagID.value = srcTagID
  // Resolve source tag name from backend for display without delaying dialog
  sourceTagName.value = '…'
  void lookupTagNameById(srcTagID).then((name) => {
    // Only apply if dialog still points to same tag
    if (sourceTagID.value === srcTagID) {
      sourceTagName.value = name || ''
    }
  })
  metaModelRef.value = metaModel
  // reset state and show
  search.value = ''
  selectedUnitGroup.value = ''
  selectedCategory.value = ''
  selected.value = []
  selectedUnit.value = ''
  notes.value = ''
  identifier.value = ''
  depthStr.value = ''
  location.value = ''
  locationOptions.value = normalizeLocationOptions(tagLocationEnumValues || [], init?.location)

  // Reset sampling data to defaults
  sampling.value = model.Sampling.Sensor
  sensorType.value = model.SensorType.InSitu
  sensorPrinciple.value = model.MeasurementPrinciple.ISE
  sensorT90Str.value = '0'
  autoSamplerProportional.value = model.ProportionalType.Volume
  autoSamplerIntervalStr.value = '1'
  autoSamplerOffsetStr.value = '0'
  autoSamplerTimestampPos.value = model.TimestampPos.Start
  // preselect if provided
  if (init) {
    if (init.what) {
      selected.value = [init.what]
      selectedUnitGroup.value = init.what.UnitGroup || ''
      selectedCategory.value = init.what.Category || ''
    }
    if (init.unit) selectedUnit.value = init.unit || ''
    if (init.notes) notes.value = init.notes || ''
    if (init.identifier) identifier.value = init.identifier || ''
    if (init.depth) depthStr.value = String(init.depth)
    if (init.location) location.value = init.location

    // Initialize sampling data from init
    if (init.sampling) sampling.value = init.sampling
    if (init.sensorDetails) {
      sensorType.value = init.sensorDetails.type
      sensorPrinciple.value = init.sensorDetails.principle
      sensorT90Str.value = String(init.sensorDetails.t90)
    }
    if (init.autoSamplerDetails) {
      autoSamplerProportional.value = init.autoSamplerDetails.proportional
      autoSamplerIntervalStr.value = String(init.autoSamplerDetails.interval)
      autoSamplerOffsetStr.value = String(init.autoSamplerDetails.offset)
      autoSamplerTimestampPos.value = init.autoSamplerDetails.timestampPosition
    }
  }
  isOpen.value = true
  return new Promise<DialogResult | null>((resolve) => {
    resolver = resolve
  })
}

defineExpose({ open })
</script>

<style></style>
