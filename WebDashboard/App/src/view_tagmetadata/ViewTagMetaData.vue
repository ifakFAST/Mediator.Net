<template>
  <v-container
    fluid
    class="pa-3"
  >
    <v-row>
      <v-col>
        <Splitter
          :default-percent="75"
          style="height: 550px; overflow-y: auto"
        >
          <template #left-pane>
            <FlowEditor
              :height="500"
              :model="flowModel"
              :blockParamsChange="blockParamsChange"
              :block2ContextMenu="block2ContextMenu"
              :saveEnabled="modelModified"
              :saving="saving"
              :externalCommand="externalCommand"
              @modified="onFlowModelModified"
              @blockDrop="onBlockDrop"
              @interactive="onInteractiveClick"
              @contextblock="onBlockContextClicked"
              @save="saveFlowModel"
              @block_selection="onBlockSelectionChanged"
            />
          </template>
          <template #right-pane>
            <v-container>
              <v-tabs
                v-model="currentTab"
                density="compact"
              >
                <v-tab value="blocks">BlockLib</v-tab>
                <v-tab value="tags">Tags</v-tab>
              </v-tabs>

              <v-window v-model="currentTab">
                <v-window-item
                  value="blocks"
                  :transition="false"
                  :reverse-transition="false"
                >
                  <v-container>
                    <p
                      v-for="block in blockLibrary"
                      :key="block.name"
                      class="blockitem"
                      draggable="true"
                      @dragstart="
                        (e) => {
                          onBlockDragStart(block, e)
                        }
                      "
                    >
                      {{ block.name }}
                    </p>
                    <p @click="onEditBlockLibrary">Edit block library</p>
                  </v-container>
                </v-window-item>
                <v-window-item
                  value="tags"
                  :transition="false"
                  :reverse-transition="false"
                >
                  <v-container>
                    <div class="d-flex align-center mb-2">
                      <div class="d-flex flex-nowrap overflow-auto flex-grow-0 flex-shrink-0">
                        <v-btn
                          v-for="m in moduleInfos"
                          :key="m.ID"
                          density="compact"
                          class="mr-1"
                          :color="m.ID === selectedModuleId ? 'primary' : undefined"
                          :variant="m.ID === selectedModuleId ? 'elevated' : 'text'"
                          @click="() => onSelectModule(m.ID)"
                        >
                          {{ m.Name }}
                        </v-btn>
                      </div>
                      <v-text-field
                        v-model="tagsSearch"
                        label="Search tags"
                        single-line
                        hide-details
                        density="compact"
                        class="ml-2 flex-grow-1"
                      />
                    </div>
                    <div>
                      <p
                        v-for="t in filteredAvailableTags"
                        :key="t.ID"
                        class="mb-1"
                        draggable="true"
                        @dragstart="(e) => onTagDragStart(t, e)"
                      >
                        {{ t.Name }}
                      </p>
                      <p v-if="filteredAvailableTags.length === 0">No tags found</p>
                    </div>
                  </v-container>
                </v-window-item>
              </v-window>
            </v-container>
          </template>
        </Splitter>

        <v-card>
          <v-card-text>
            <div
              class="d-flex align-center mb-4"
              style="gap: 8px"
            >
              <div class="text-h6 mr-4">Assigned Tags</div>

              <v-text-field
                v-model="search"
                label="Search"
                style="max-width: 220px"
              ></v-text-field>

              <v-btn
                density="compact"
                variant="text"
                @click="onDeleteSelected"
                :disabled="selectedTagIDs.length === 0"
                icon="mdi-delete"
                title="Delete"
                aria-label="Delete"
              />
            </div>

            <v-data-table
              :headers="headers"
              :items="tagsFromSelection"
              :items-per-page="25"
              :search="search"
              density="compact"
              class="elevation-1"
              show-select
              item-value="id"
              v-model="selectedTagIDs"
            >
              <template #item.actions="{ item }">
                <v-btn
                  density="compact"
                  variant="text"
                  icon="mdi-pencil"
                  title="Edit"
                  aria-label="Edit"
                  @click="onEditTagFromRow(item)"
                />
              </template>
            </v-data-table>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </v-container>

  <DlgConfigTag ref="dlgConfigTag" />
  <v-dialog
    v-model="showBlockLibDialog"
    max-width="1200"
  >
    <v-card>
      <v-card-title>Block Library</v-card-title>
      <v-card-text>
        <FlowEditor
          :height="500"
          :model="blockLibEditedModel"
          :blockParamsChange="blockParamsChange"
          :block2ContextMenu="block2ContextMenu"
          :saveEnabled="blockLibModified"
          :saving="blockLibSaving"
          @modified="onBlockLibModified"
          @save="saveBlockLibrary"
        />
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn
          variant="text"
          @click="onRequestCloseBlockLibDialog"
          >Close</v-btn
        >
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { ref, computed, onMounted, watch } from 'vue'
import type { DataTableHeader } from '@/utils'
import type { Tag } from './model_tags'
import * as simu from './flowdiagram/model_flow'
import * as command from './flowdiagram/commands'
import * as meta from './metamodel'
import * as model from './model_tags'
import * as modules from './flowdiagram/module_types'
import FlowEditor from './flowdiagram/FlowEditor.vue'
import DlgConfigTag, { type DialogResult } from './DlgConfigTag.vue'

interface ObjectInfo {
  ID: string
  Name: string
  Variables: string[]
}

interface ModuleInfo {
  ID: string
  Name: string
}

const metaModel = ref<meta.MetaModel>(meta.emptyMetaModel())
const flowModel = ref<simu.FlowModel>(simu.emptyFlowModel())
//const objectInfos = ref<ObjectInfo[]>([])
const moduleInfos = ref<ModuleInfo[]>([])
const search = ref('')
const tagsSearch = ref('')
const selectedModuleId = ref<string>('')
const selectedTagIDs = ref<string[]>([])

interface AvailableTag {
  Name: string
  ID: string
}

const availableTags = ref<AvailableTag[]>([])

const unassignedAvailableTags = computed((): AvailableTag[] => {
  const assignedIDs = new Set<string>()
  assignedTags.value.forEach((t: Tag) => assignedIDs.add(t.sourceTag))
  return availableTags.value.filter((t: AvailableTag) => !assignedIDs.has(t.ID))
})

const filteredAvailableTags = computed(() => {
  const q = tagsSearch.value.trim().toLowerCase()
  if (q === '') return unassignedAvailableTags.value
  const queryParts = q.split(' ')
  return unassignedAvailableTags.value.filter((tag) => {
    const name = tag.Name.toLowerCase()
    return queryParts.every((part) => name.includes(part))
  })
})

const blockParamsChange: simu.BlockParamsChanged = { block: '', parameters: {} }

//const MenuTags = 'Assigned Tags...'

const modelModified = ref<boolean>(false)
const modifiedModel = ref<simu.FlowModel>(simu.emptyFlowModel())
const saving = ref<boolean>(false)

const blockLibrary = ref<simu.Block[]>([])
const currentTab = ref('blocks')

// External command pipe to FlowEditor
const externalCommand = ref<command.Command | null>(null)

// Dialog: Configure Tag (async)
const dlgConfigTag = ref<InstanceType<typeof DlgConfigTag> | null>(null)

const headers: DataTableHeader[] = [
  { title: 'ID', key: 'id', sortable: false },
  { title: 'What', key: 'what', sortable: true },
  { title: 'Path', key: 'blockPath', sortable: true },
  { title: 'Location', key: 'location', sortable: true },
  { title: 'Unit', key: 'unit', sortable: true },
  { title: 'Source Unit', key: 'unitSource', sortable: true },
  { title: 'Source Tag', key: 'sourceTag', sortable: true },
  { title: 'Sampling', key: 'sampling', sortable: true },
  { title: '', key: 'actions', sortable: false },
  { title: 'Notes', key: 'notes', sortable: false },
]

function getTagLocationEnumValues(block: simu.ModuleBlock | null | undefined): string[] {
  if (!block || !block.moduleType) return []
  const moduleType = modules.MapOfModuleTypes[block.moduleType]
  const values = moduleType?.tagLocationEnumValues
  if (!Array.isArray(values)) return []
  return values.filter((v) => typeof v === 'string' && v.trim() !== '')
}

async function loadInitData() {
  try {
    // Load dynamic module types first
    await modules.loadModuleTypes()

    const response = await (window.parent as any).dashboardApp.sendViewRequestAsync('Init', {})
    metaModel.value = response.MetaModel || meta.emptyMetaModel()
    flowModel.value = response.FlowModel || simu.emptyFlowModel()
    //objectInfos.value = response.ObjectInfos || []
    moduleInfos.value = response.ModuleInfos || []
    // default selected module = first
    selectedModuleId.value = moduleInfos.value.length > 0 ? moduleInfos.value[0].ID : ''
    const blockLibModel: simu.FlowModel = response.BlockLibrary || simu.emptyFlowModel()
    blockLibrary.value = blockLibModel.diagram.blocks

    // Ensure that the module type exists for each block in blockLibrary; else remove block
    // Keep non-Module blocks intact; filter invalid Module blocks quickly for speed
    blockLibrary.value = (blockLibrary.value || []).filter((b) => {
      if (b.type !== 'Module') return true
      const mb = b as simu.ModuleBlock
      const mt = mb.moduleType
      return !!mt && modules.MapOfModuleTypes[mt] !== undefined
    })

    // Ensure the library has at least one block per available module type
    try {
      const existingTypes = new Set<string>()
      for (const b of blockLibrary.value) {
        if (b.type === 'Module') {
          const mb = b as simu.ModuleBlock
          if (mb.moduleType) existingTypes.add(mb.moduleType)
        }
      }
      for (const typeId of Object.keys(modules.MapOfModuleTypes)) {
        if (!existingTypes.has(typeId)) {
          // Find a free spot in the library canvas: append at the bottom
          const gap = 50
          const hasBlocks = blockLibrary.value && blockLibrary.value.length > 0
          const minX = hasBlocks ? Math.min(...blockLibrary.value.map((b) => b.x)) : 0
          const maxBottom = hasBlocks ? Math.max(...blockLibrary.value.map((b) => b.y + b.h)) : 0
          const placeX = minX
          const placeY = hasBlocks ? maxBottom + gap : 0
          const nb: simu.ModuleBlock = {
            name: typeId,
            type: 'Module',
            moduleType: typeId,
            parameters: {},
            tags: [],
            x: placeX,
            y: placeY,
            w: 120,
            h: 60,
            drawFrame: true,
            drawName: true,
            flipName: false,
          }
          blockLibrary.value.push(nb)
        }
      }
    } catch (e) {
      console.warn('Failed to augment block library from module types:', e)
    }

    // Initialize table for root diagram when nothing is selected
    lastSelectionPath.value = []
    lastSelectionBlockNames.value = []
    updateTagsTableForCurrentContext()
  } catch (error) {
    console.error('Failed to load init data:', error)
  }
}

onMounted(() => {
  loadInitData()
})

interface TagRow {
  id: string
  what: string
  blockPath: string
  location: string
  unit: string
  unitSource: string
  sourceTag: string
  sampling: string
  notes: string
}

const tagsFromSelection = ref<TagRow[]>([])
const lastSelectionPath = ref<string[]>([])
const lastSelectionBlockNames = ref<string[]>([])

function computeRowsForBlocks(blocks: simu.Block[], basePath: string[]): TagRow[] {
  const rows: TagRow[] = []

  const addRowsFromModule = (mb: simu.ModuleBlock, path: string[]) => {
    const blockPath = [...path, mb.name].join('/')
    if (mb.tags && mb.tags.length > 0) {
      for (const t of mb.tags) {
        rows.push({
          id: t.id,
          what: t.what,
          blockPath: blockPath,
          location: t.location || '',
          unit: t.unit,
          unitSource: t.unitSource,
          sourceTag: t.sourceTag,
          sampling: String(t.sampling),
          notes: t.notes,
        })
      }
    }
  }

  const collectRowsRecursive = (diagram: simu.FlowDiagram, path: string[]) => {
    for (const b of diagram.blocks) {
      if (b.type === 'Module') {
        addRowsFromModule(b as simu.ModuleBlock, path)
      } else if (b.type === 'Macro') {
        const mac = b as simu.MacroBlock
        collectRowsRecursive(mac.diagram, [...path, mac.name])
      }
    }
  }

  for (const b of blocks) {
    if (b.type === 'Module') {
      addRowsFromModule(b as simu.ModuleBlock, basePath)
    } else if (b.type === 'Macro') {
      const mac = b as simu.MacroBlock
      collectRowsRecursive(mac.diagram, [...basePath, mac.name])
    }
  }

  return rows
}

function findDiagram(model: simu.FlowModel, path: string[]): simu.FlowDiagram | null {
  if (path.length === 0) return model.diagram
  return findMacroBlock(model.diagram, path, 0)?.diagram || null
}

function findMacroBlock(diagram: simu.FlowDiagram, names: string[], level: number): simu.MacroBlock | null {
  const name = names[level]
  for (const b of diagram.blocks) {
    if (b.type === 'Macro' && b.name === name) {
      const mac = b as simu.MacroBlock
      if (level === names.length - 1) return mac
      return findMacroBlock(mac.diagram, names, level + 1)
    }
  }
  return null
}

function onBlockSelectionChanged(blocks: simu.Block[], diagramPath: string[]): void {
  lastSelectionPath.value = (diagramPath || []).slice()
  lastSelectionBlockNames.value = blocks.map((b) => b.name)
  updateTagsTableForCurrentContext()
}

// Use the in-memory modified model (from FlowEditor) when present
const assignedTags = computed((): Set<Tag> => {
  const set = new Set<Tag>()
  const effectiveModel = modelModified.value ? modifiedModel.value : flowModel.value
  getTagsRecursive(effectiveModel.diagram, set)
  return set
})

function getTagsRecursive(diagram: simu.FlowDiagram, set: Set<Tag>): void {
  diagram.blocks.forEach((b) => {
    if (b.type === 'Module') {
      const mb = b as simu.ModuleBlock
      if (mb.tags !== undefined) {
        mb.tags.forEach((t) => set.add(t))
      }
    } else if (b.type === 'Macro') {
      const mac = b as simu.MacroBlock
      getTagsRecursive(mac.diagram, set)
    }
  })
}

watch(currentTab, async (tab) => {
  if (tab === 'tags') {
    await loadAvailableTags()
  }
})

watch(selectedModuleId, async () => {
  if (currentTab.value === 'tags') {
    await loadAvailableTags()
  }
})

async function loadAvailableTags() {
  try {
    if (moduleInfos.value.length === 0) {
      availableTags.value = []
      return
    }
    const modID = selectedModuleId.value || moduleInfos.value[0].ID
    const response = await (window.parent as any).dashboardApp.sendViewRequestAsync('GetTagsForModule', {
      moduleID: modID,
    })
    availableTags.value = Array.isArray(response) ? (response as AvailableTag[]) : []
  } catch (error) {
    console.error('Failed to load available tags:', error)
    availableTags.value = []
  }
}

function onSelectModule(id: string) {
  selectedModuleId.value = id
}

async function saveFlowModel() {
  if (!modelModified.value) return

  saving.value = true
  try {
    const flowModelJson = JSON.stringify(modifiedModel.value, null, 2)
    const response = await (window.parent as any).dashboardApp.sendViewRequestAsync('SaveModel', {
      modelJson: flowModelJson,
    })

    // Update the current flowModel with the saved version
    flowModel.value = { ...modifiedModel.value }
    modelModified.value = false

    console.log('FlowModel saved successfully:', response)
  } catch (error) {
    console.error('Failed to save FlowModel:', error)
    // TODO: Show error notification to user
  } finally {
    saving.value = false
  }
}

function block2ContextMenu(b: simu.Block): string[] {
  if (b.type !== 'Module') {
    return []
  }
  const mb: simu.ModuleBlock = b as simu.ModuleBlock
  const type: modules.ModuleBlockType = modules.MapOfModuleTypes[mb.moduleType]
  if (type !== undefined && type.supportedDropTypes !== undefined && type.supportedDropTypes.find((x) => x === 'data-tag') !== undefined) {
    return []
    //return [MenuTags]
  }
  return []
}

function onFlowModelModified(modified: boolean, model: simu.FlowModel): void {
  modelModified.value = modified
  modifiedModel.value = model
  // Recompute table for current selection or full diagram if none
  updateTagsTableForCurrentContext()
}

function updateTagsTableForCurrentContext() {
  const effectiveModel = modelModified.value ? modifiedModel.value : flowModel.value
  const diagram = findDiagram(effectiveModel, lastSelectionPath.value) || effectiveModel.diagram
  if (lastSelectionBlockNames.value.length === 0) {
    // No selection: show all tags from current diagram (recursive)
    tagsFromSelection.value = computeRowsForBlocks(diagram.blocks, lastSelectionPath.value)
  } else {
    const blocks = diagram.blocks.filter((b) => lastSelectionBlockNames.value.includes(b.name))
    tagsFromSelection.value = computeRowsForBlocks(blocks, lastSelectionPath.value)
  }
}

function onTagDragStart(tag: AvailableTag, event: DragEvent) {
  if (event.dataTransfer === null) {
    return
  }
  const data = {
    id: tag.ID,
    name: tag.Name,
  }
  event.dataTransfer.setData('data-tag', JSON.stringify(data))
  // we need this hack because Chrome does no allow reading the data in dragover event:
  simu.GlobalObj.dragType = 'data-tag'
}

async function doOpenTagsEditor(block: simu.ModuleBlock, selectedTagID: string): Promise<void> {
  const tags = block.tags || []
  const tag = tags.find((t) => t.id === selectedTagID)
  if (!tag) {
    console.error(`Tag with ID ${selectedTagID} not found in block ${block.name}`)
    alert(`Tag with ID ${selectedTagID} not found in block ${block.name}`)
    return
  }
  const what = metaModel.value.Whats.find((w) => w.ID === tag.what)
  if (!what) {
    console.error(`What with ID ${tag.what} not found in MetaModel`)
    alert(`What with ID ${tag.what} not found in MetaModel`)
    return
  }
  const dlg = dlgConfigTag.value
  if (!dlg) return
  const title = 'Edit tag with ID:'
  const srcTagID = tag.sourceTag || ''
  const values: DialogResult = {
    what: what,
    unit: tag.unitSource || '',
    notes: tag.notes || '',
    identifier: tag.id,
    depth: tag.depth,
    location: tag.location,
    sampling: tag.sampling,
    sensorDetails: tag.sensorDetails,
    autoSamplerDetails: tag.autoSamplerDetails,
  }
  const locationEnumValues = getTagLocationEnumValues(block)
  const result = await dlg.open(title, srcTagID, metaModel.value, values, locationEnumValues)
  if (result) {
    const updatedTag: model.Tag = {
      ...tag,
      id: result.identifier,
      what: result.what.ID,
      unitSource: result.unit,
      unit: result.what.RefUnit,
      notes: result.notes,
      depth: result.depth ?? null,
      location: result.location,
      sampling: result.sampling,
      sensorDetails: result.sensorDetails,
      autoSamplerDetails: result.autoSamplerDetails,
    }
    externalCommand.value = new command.BlockUpdateTag(block.name, tag.id, updatedTag)
  }
}

// Helper: find ModuleBlock by absolute path parts from root diagram
function findModuleBlockByAbsolutePath(pathParts: string[]): simu.ModuleBlock | null {
  const effectiveModel = modelModified.value ? modifiedModel.value : flowModel.value
  let diagram: simu.FlowDiagram | null = effectiveModel.diagram
  if (!pathParts || pathParts.length === 0) return null
  for (let i = 0; i < pathParts.length - 1; i++) {
    const name = pathParts[i]
    const mac = diagram.blocks.find((b) => b.type === 'Macro' && b.name === name) as simu.MacroBlock | undefined
    if (!mac) return null
    diagram = mac.diagram
  }
  const modName = pathParts[pathParts.length - 1]
  const mod = diagram.blocks.find((b) => b.type === 'Module' && b.name === modName) as simu.ModuleBlock | undefined
  return mod || null
}

// Compute relative path for a row against the current diagram path
function relativePathForRow(row: TagRow): string[] {
  const rowParts = (row.blockPath || '').split('/').filter((s) => s)
  const base = lastSelectionPath.value
  // If rowParts start with base, strip it; otherwise, return rowParts as-is
  let i = 0
  while (i < rowParts.length && i < base.length && rowParts[i] === base[i]) i++
  return rowParts.slice(i)
}

// Open editor for a tag identified by a table row, using a relative-path update command
async function doOpenTagsEditorAtRelativePath(relativeParts: string[], selectedTagID: string): Promise<void> {
  // Resolve absolute block to prefill dialog values
  const absParts = [...lastSelectionPath.value, ...relativeParts]
  const block = findModuleBlockByAbsolutePath(absParts)
  if (!block) {
    alert('Module not found for row path: ' + absParts.join('/'))
    return
  }
  const tag = (block.tags || []).find((t) => t.id === selectedTagID)
  if (!tag) {
    alert('Tag not found: ' + selectedTagID)
    return
  }
  const what = metaModel.value.Whats.find((w) => w.ID === tag.what)
  if (!what) {
    alert('What not found: ' + tag.what)
    return
  }
  const dlg = dlgConfigTag.value
  if (!dlg) return
  const title = 'Edit tag with ID:'
  const srcTagID = tag.sourceTag || ''
  const values: DialogResult = {
    what: what,
    unit: tag.unitSource || '',
    notes: tag.notes || '',
    identifier: tag.id,
    depth: tag.depth,
    location: tag.location,
    sampling: tag.sampling,
    sensorDetails: tag.sensorDetails,
    autoSamplerDetails: tag.autoSamplerDetails,
  }
  const locationEnumValues = getTagLocationEnumValues(block)
  const result = await dlg.open(title, srcTagID, metaModel.value, values, locationEnumValues)
  if (result) {
    const updatedTag: model.Tag = {
      ...tag,
      id: result.identifier,
      what: result.what.ID,
      unitSource: result.unit,
      unit: result.what.RefUnit,
      notes: result.notes,
      depth: result.depth ?? null,
      location: result.location,
      sampling: result.sampling,
      sensorDetails: result.sensorDetails,
      autoSamplerDetails: result.autoSamplerDetails,
    }
    // Use a relative-path command so edits work inside nested macros too
    externalCommand.value = new command.UpdateTagByRelativePath(relativeParts, tag.id, updatedTag)
  }
}

function onEditTagFromRow(row: TagRow): void {
  const rel = relativePathForRow(row)
  if (rel.length === 0) {
    // Should not happen, but guard against it
    alert('Cannot resolve module path for row')
    return
  }
  doOpenTagsEditorAtRelativePath(rel, row.id)
}

function onBlockContextClicked(e: { block: simu.Block; menu: string }) {
  //if (e.menu === MenuTags && e.block.type === 'Module') {
  //doOpenTagsEditor(e.block as simu.ModuleBlock)
  //}
}

function onInteractiveClick(x: simu.InteractiveClickEvent): void {
  if (x.type === 'tag' && x.block.type === 'Module') {
    doOpenTagsEditor(x.block as simu.ModuleBlock, x.id)
  }
}

function onBlockDragStart(block: simu.Block, event: DragEvent) {
  if (event.dataTransfer === null) {
    return
  }
  event.dataTransfer.setData('simu-block', JSON.stringify(block))
  // we need this hack because Chrome does no allow reading the data in dragover event:
  simu.GlobalObj.dragType = 'simu-block'
}

async function onBlockDrop(x: simu.BlockDropEvent): Promise<void> {
  if (x.type === 'data-tag' && x.blockType === 'Module') {
    const blockName = x.blockName
    const diagramPath = x.diagramPath || []
    const fullPathParts = [...(diagramPath || []), blockName]
    const moduleBlock: simu.ModuleBlock | null = findModuleBlockByAbsolutePath(fullPathParts)
    if (!moduleBlock) {
      alert('Module block not found: ' + blockName)
      return
    }

    // Open configuration dialog to select What from MetaModel
    try {
      const data = JSON.parse(x.data) as { id: string; name: string }
      if (!data.id || !data.name) {
        console.error('Invalid tag data:', data)
        return
      }

      // Prevent assigning the same source tag twice
      for (const t of assignedTags.value) {
        if (t.sourceTag === data.id) {
          alert(`Tag with id ${data.id} is already assigned`)
          return
        }
      }

      const dlg = dlgConfigTag.value
      if (!dlg) return

      // Generate a unique identifier within the current model
      // Use current assigned tags (from effective model) to avoid collisions
      const usedIds = new Set<string>()
      for (const t of assignedTags.value) usedIds.add(t.id)
      let identifier = generateId()
      // In the unlikely event of collision, regenerate until unique
      // Keep loop tight and bounded (fast exit in practice)
      let tries = 0
      while (usedIds.has(identifier) && tries < 32) {
        identifier = generateId()
        tries++
      }

      const locationEnumValues = getTagLocationEnumValues(moduleBlock)
      const result = await dlg.open('Create new tag with ID: ', data.id, metaModel.value, { identifier }, locationEnumValues)

      if (result) {
        const what: meta.What = result.what
        const unit: string = result.unit
        const notes: string = result.notes
        const identifier: string = result.identifier
        // const fullBlockPath = [...(x.diagramPath || []), moduleBlock.name].join('/')
        const newTag: model.Tag = {
          id: identifier,
          what: what.ID,
          unitSource: unit,
          unit: what.RefUnit,
          sampling: result.sampling,
          sensorDetails: result.sensorDetails,
          autoSamplerDetails: result.autoSamplerDetails,
          notes: notes,
          sourceTag: data.id,
          depth: result.depth ?? null,
          location: result.location,
        }
        externalCommand.value = new command.BlockAddTag(blockName, newTag)
      }
    } catch (err) {
      console.error('Failed to open tag config dialog:', err)
    }
  }
}

function generateId(): string {
  let low16: number
  let highNibble: number

  // Try to use crypto for better randomness
  const crypto = globalThis.crypto
  if (crypto?.getRandomValues) {
    try {
      // Generate only the bytes we need (3 bytes = 24 bits, we use 20)
      const randomBytes = new Uint8Array(3)
      crypto.getRandomValues(randomBytes)

      // Use first 2 bytes for the lower 16 bits
      low16 = (randomBytes[0] << 8) | randomBytes[1]

      // Use part of the third byte for high nibble (a-f)
      highNibble = 0xa + (randomBytes[2] % 6)
    } catch {
      // Fallback to Math.random if crypto fails
      low16 = (Math.random() * 0x10000) | 0
      highNibble = 0xa + ((Math.random() * 6) | 0)
    }
  } else {
    // Non-crypto fallback
    low16 = (Math.random() * 0x10000) | 0
    highNibble = 0xa + ((Math.random() * 6) | 0)
  }

  // Combine high nibble with low 16 bits to get range 0xa0000..0xfffff
  const value = (highNibble << 16) | low16

  // Convert to hex string (always exactly 5 lowercase characters)
  return value.toString(16).toUpperCase()
}

function onDeleteSelected() {
  if (selectedTagIDs.value.length === 0) return
  const count = selectedTagIDs.value.length
  const msg = count === 1 ? 'Delete the selected tag?' : `Delete ${count} selected tags?`
  if (!confirm(msg)) return
  // Use command to remove tags across the current diagram (and nested macros)
  externalCommand.value = new command.DiagramRemoveTags([...selectedTagIDs.value])
  selectedTagIDs.value = []
}

function onEditBlockLibrary() {
  // Open dialog with a FlowEditor to edit the block library
  // Create a FlowModel from current library blocks (deep copy)
  const model: simu.FlowModel = simu.emptyFlowModel()
  try {
    model.diagram.blocks = JSON.parse(JSON.stringify(blockLibrary.value || []))
    model.diagram.lines = []
  } catch {
    // Fallback shallow copy if JSON copy fails
    model.diagram.blocks = (blockLibrary.value || []).map((b) => ({ ...b }))
    model.diagram.lines = []
  }
  blockLibEditedModel.value = model
  blockLibModified.value = false
  blockLibSaving.value = false
  showBlockLibDialog.value = true
}

// Block library editor state and actions
const showBlockLibDialog = ref<boolean>(false)
const blockLibEditedModel = ref<simu.FlowModel>(simu.emptyFlowModel())
const blockLibModified = ref<boolean>(false)
const blockLibSaving = ref<boolean>(false)
// Guard to avoid double-confirm when we already confirmed programmatic close
const blockLibSuppressCloseConfirm = ref<boolean>(false)

function onBlockLibModified(modified: boolean, model: simu.FlowModel): void {
  blockLibModified.value = modified
  blockLibEditedModel.value = model
}
// Warn user when closing dialog with unsaved changes
function onRequestCloseBlockLibDialog(): void {
  if (blockLibModified.value) {
    const ok = confirm('You have unsaved changes. Close without saving?')
    if (!ok) return
    // Suppress the watcher confirmation since user already confirmed
    blockLibSuppressCloseConfirm.value = true
  }
  showBlockLibDialog.value = false
}

watch(showBlockLibDialog, (val) => {
  // Intercept closings triggered by outside click or ESC
  if (!val) {
    if (blockLibSuppressCloseConfirm.value) {
      blockLibSuppressCloseConfirm.value = false
      return
    }
    if (blockLibModified.value) {
      const ok = confirm('You have unsaved changes. Close without saving?')
      if (!ok) {
        // Reopen dialog to prevent accidental close
        showBlockLibDialog.value = true
      }
    }
  }
})
async function saveBlockLibrary(): Promise<void> {
  if (!blockLibModified.value) return
  blockLibSaving.value = true
  try {
    const libJson = JSON.stringify(blockLibEditedModel.value, null, 2)
    await (window.parent as any).dashboardApp.sendViewRequestAsync('SaveBlockLibrary', {
      modelJson: libJson,
    })
    // Reflect changes in the right-pane list
    blockLibrary.value = (blockLibEditedModel.value.diagram.blocks || []).map((b) => ({ ...b }))
    blockLibModified.value = false
  } catch (e) {
    console.error('Failed to save block library:', e)
    alert('Failed to save block library.')
  } finally {
    blockLibSaving.value = false
  }
}
</script>

<style scoped>
/* Make the first columns minimal and non-wrapping, let the last (Notes) fill */
:deep(.v-data-table .v-table__wrapper table th:not(:last-child)),
:deep(.v-data-table .v-table__wrapper table td:not(:last-child)) {
  width: 1%;
  white-space: nowrap;
}

:deep(.v-data-table .v-table__wrapper table th:last-child),
:deep(.v-data-table .v-table__wrapper table td:last-child) {
  width: auto; /* takes remaining space */
  white-space: normal; /* allow wrapping for long notes */
}
</style>
