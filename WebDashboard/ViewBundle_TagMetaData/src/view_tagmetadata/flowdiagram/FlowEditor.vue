<template>
  <div>
    <table>
      <tbody>
        <tr>
          <td>
            <slot></slot>
          </td>
          <td>
            <v-tooltip text="Save">
              <template v-slot:activator="{ props }">
                <v-btn
                  v-bind="props"
                  size="small"
                  style="min-width: 0; min-height: 25px;"
                  :disabled="!saveEnabled || saving"
                  :loading="saving"
                  @click="onSave"
                >
                  <v-icon>mdi-content-save</v-icon>
                </v-btn>
              </template>
            </v-tooltip>
          </td>
          <td>
            <v-tooltip text="Undo">
              <template v-slot:activator="{ props }">
                <v-btn
                  v-bind="props"
                  size="small"
                  style="min-width: 0; min-height: 25px;"
                  :disabled="!undoEnabled"
                  @click="onUndo"
                >
                  <v-icon>mdi-undo</v-icon>
                </v-btn>
              </template>
            </v-tooltip>
          </td>
          <td>
            <v-tooltip text="Redo">
              <template v-slot:activator="{ props }">
                <v-btn
                  v-bind="props"
                  size="small"
                  style="min-width: 0; min-height: 25px;"
                  :disabled="!redoEnabled"
                  @click="onRedo"
                >
                  <v-icon>mdi-redo</v-icon>
                </v-btn>
              </template>
            </v-tooltip>
          </td>
          <td>&nbsp;&nbsp;</td>
          <td>
            <v-tooltip text="Zoom in">
              <template v-slot:activator="{ props }">
                <v-btn
                  v-bind="props"
                  size="small"
                  style="min-width: 0; min-height: 25px;"
                  @click="onZoomIn"
                >
                  <v-icon>mdi-magnify-plus</v-icon>
                </v-btn>
              </template>
            </v-tooltip>
          </td>
          <td>
            <v-tooltip text="Zoom Out">
              <template v-slot:activator="{ props }">
                <v-btn
                  v-bind="props"
                  size="small"
                  style="min-width: 0; min-height: 25px;"
                  @click="onZoomOut"
                >
                  <v-icon>mdi-magnify-minus</v-icon>
                </v-btn>
              </template>
            </v-tooltip>
          </td>
          <td>&nbsp;&nbsp;</td>
          <td>
            <v-tooltip text="Cut">
              <template v-slot:activator="{ props }">
                <v-btn
                  v-bind="props"
                  size="small"
                  @click="onCut"
                  :disabled="!copyEnabled"
                  style="min-width: 0; min-height: 25px;"
                >
                  <v-icon>mdi-content-cut</v-icon>
                </v-btn>
              </template>
            </v-tooltip>
          </td>
          <td>
            <v-tooltip text="Copy">
              <template v-slot:activator="{ props }">
                <v-btn
                  v-bind="props"
                  size="small"
                  @click="onCopy"
                  :disabled="!copyEnabled"
                  style="min-width: 0; min-height: 25px;"
                >
                  <v-icon>mdi-content-copy</v-icon>
                </v-btn>
              </template>
            </v-tooltip>
          </td>
          <td>
            <v-tooltip text="Paste">
              <template v-slot:activator="{ props }">
                <v-btn
                  v-bind="props"
                  size="small"
                  @click="onPaste"
                  :disabled="!pasteEnabled"
                  style="min-width: 0; min-height: 25px;"
                >
                  <v-icon>mdi-content-paste</v-icon>
                </v-btn>
              </template>
            </v-tooltip>
          </td>
          <td>&nbsp;&nbsp;</td>
          <td>
            <v-select
              style="width: 10em"
              v-model="selectedLayers"
              :items="allLayers"
              label="Layers"
              multiple
            ></v-select>
          </td>
          <td>&nbsp;&nbsp;</td>
          <td v-if="currentDiagramPath.length > 0">
            &nbsp;
            <a
              @click="jumpToMacroLevel(0)"
              style="cursor: pointer; color: #1976d2"
              >Root</a
            >
            &nbsp;|&nbsp;
            <a
              v-for="(level, index) in currentDiagramPath.slice(0, -1)"
              :key="index"
              @click="jumpToMacroLevel(index + 1)"
              style="cursor: pointer; color: #1976d2"
              >{{ level }} &nbsp;|&nbsp;</a
            >
            {{ currentDiagramPath.slice(-1)[0] }}
          </td>
        </tr>
      </tbody>
    </table>
    <FlowCanvas
      :diagram="currentDiagram"
      :changeStackCount="changeStack.length"
      :scale="scale"
      :height="height"
      :cut="cmdCut"
      :copy="cmdCopy"
      :paste="cmdPaste"
      :clipboard="clipboard"
      :layers="selectedLayers"
      @command="onCommand"
      @doubleclick="onDoubleClick"
      @contextmenu="onContextMenu"
      @escape="onEscape"
      @blockDrop="onBlockDrop"
      @interactive="onInteractiveClick"
      @edit_copy="onEditCopy"
      @block_selection="onBlockSelectionChanged"
    />
    <v-menu
      v-model="contextMenu.show"
      :target="[contextMenu.x, contextMenu.y]"
      absolute
    >
      <v-list>
        <v-list-item
          v-for="(item, index) in contextMenu.items"
          :key="index"
          @click="
            () => {
              contextMenu.show = false
              contextMenu.block && item.handler(contextMenu.block)
            }
          "
        >
          <v-list-item-title>{{ item.title }}</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>
    <DlgBlockRename
      v-model:show="showBlockRename"
      :block="configureBlock"
      :diagram="currentDiagram"
      @changed="onBlockNameChanged"
    />
    <DlgBlockParams
      v-model:show="showBlockParams"
      :block="configureBlock"
      @changed="onBlockParamsChanged"
    />
    <DlgBlockProperties
      v-model:show="showBlockProperties"
      :block="configureBlock"
      @changed="onBlockPropertiesChanged"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, reactive, nextTick } from 'vue'
import FlowCanvas from './canvas/FlowCanvas.vue'
import DlgBlockRename from './DlgBlockRename.vue'
import DlgBlockParams from './DlgBlockParams.vue'
import type { ParamEntry } from './DlgBlockParams.vue'
import DlgBlockProperties from './DlgBlockProperties.vue'
import * as simu from './model_flow'
import * as command from './commands'

// Interfaces
interface ContextMenu {
  show: boolean
  x: number
  y: number
  block: simu.Block | null
  items: ContextMenuItem[]
}

interface ContextMenuItem {
  title: string
  handler: (block: simu.Block) => void
}

// Props
const props = defineProps<{
  height: number
  model: simu.FlowModel
  blockParamsChange: simu.BlockParamsChanged
  block2ContextMenu: (block: simu.Block) => string[]
  saveEnabled: boolean
  saving: boolean
  externalCommand?: command.Command | null
}>()

// Emits
const emit = defineEmits<{
  modified: [isModified: boolean, model: simu.FlowModel]
  blockDrop: [event: simu.BlockDropEvent]
  interactive: [event: simu.InteractiveClickEvent]
  contextblock: [payload: { block: simu.Block; menu: string }]
  save: []
  block_selection: [blocks: simu.Block[], diagramPath: string[]]
}>()

// Utility function
const copyModel = (m: simu.FlowModel): simu.FlowModel => {
  const str = JSON.stringify(m)
  const copy = JSON.parse(str)
  return copy
}

// Reactive state
const myModel = ref<simu.FlowModel>(copyModel(props.model))
const currentDiagramPath = ref<string[]>([])
const changeStack = ref<command.Command[]>([])
const redoStack = ref<command.Command[]>([])
const showBlockRename = ref<boolean>(false)
const showBlockParams = ref<boolean>(false)
const showBlockProperties = ref<boolean>(false)
const configureBlock = ref<simu.Block | null>(null)

const contextMenu = reactive<ContextMenu>({
  show: false,
  x: 0,
  y: 0,
  block: null,
  items: [],
})

const scale = ref(1)
const cmdCopy = ref(0)
const cmdCut = ref(0)
const cmdPaste = ref(0)
const clipboard = ref('')
const selectedBlockNames = ref<string[]>([])

const selectedLayers = ref<string[]>(['Water', 'Signal', 'Air'])
const allLayers = ref<string[]>(['Water', 'Signal', 'Air'])

// Computed properties
const currentDiagram = computed((): simu.FlowDiagram => {
  if (myModel.value === null || myModel.value === undefined || myModel.value.diagram === undefined) {
    return {
      blocks: [],
      lines: [],
    }
  }
  const res = findDiagram(myModel.value, currentDiagramPath.value)
  if (res === null) {
    currentDiagramPath.value = []
    return myModel.value.diagram
  } else {
    return res
  }
})

const undoEnabled = computed((): boolean => {
  return changeStack.value.length > 0
})

const redoEnabled = computed((): boolean => {
  return redoStack.value.length > 0
})

const copyEnabled = computed((): boolean => {
  return selectedBlockNames.value.length > 0
})

const pasteEnabled = computed((): boolean => {
  return clipboard.value !== ''
})

// Watchers
watch(
  () => props.model,
  (model: simu.FlowModel) => {
    myModel.value = copyModel(model)
    changeStack.value = []
    redoStack.value = []
  },
)

watch(
  () => props.blockParamsChange,
  (change: simu.BlockParamsChanged) => {
    const cmd = new command.BlockParamsChanged(change.block, change.parameters)
    onCommand(cmd)
  },
)

watch(
  () => props.externalCommand,
  (cmd) => {
    if (cmd) {
      onCommand(cmd)
    }
  },
)

// Methods
const findDiagram = (model: simu.FlowModel, path: string[]): simu.FlowDiagram | null => {
  if (path.length === 0) {
    return model.diagram
  }
  const macro = findMacroBlock(model.diagram, path, 0)
  if (macro !== null) {
    return macro.diagram
  } else {
    return null
  }
}

const findMacroBlock = (diagram: simu.FlowDiagram, names: string[], level: number): simu.MacroBlock | null => {
  const name = names[level]
  for (const b of diagram.blocks) {
    if (b.type === 'Macro' && b.name === name) {
      const macro = b as simu.MacroBlock
      if (level === names.length - 1) {
        return macro
      }
      return findMacroBlock(macro.diagram, names, level + 1)
    }
  }
  console.error('FlowEditor.findMacroBlock: No macro block found with name: ' + name)
  return null
}

const jumpToMacroLevel = (level: number): void => {
  currentDiagramPath.value = currentDiagramPath.value.slice(0, level)
  // Notify parent about path change so it can refresh context-dependent views
  emit(
    'block_selection',
    [],
    currentDiagramPath.value.map((s) => s),
  )
}

const onZoomIn = () => {
  scale.value *= 1.1
}

const onZoomOut = () => {
  scale.value /= 1.1
}

const onCut = (): void => {
  cmdCut.value = new Date().getTime()
}

const onCopy = (): void => {
  cmdCopy.value = new Date().getTime()
}

const onPaste = (): void => {
  cmdPaste.value = new Date().getTime()
}

const onCommand = (cmd: command.Command) => {
  cmd.diagramPath = currentDiagramPath.value.map((s) => s)
  console.info(cmd)
  changeStack.value.push(cmd)
  redoStack.value = []
  cmd.apply(currentDiagram.value)
  emit('modified', true, myModel.value)
}

const onSave = () => {
  emit('save')
}

const onUndo = () => {
  const cmd = changeStack.value.pop()
  if (cmd !== undefined) {
    redoStack.value.push(cmd)
    applyChangeStack()
  }
}

const onRedo = () => {
  const cmd = redoStack.value.pop()
  if (cmd !== undefined) {
    changeStack.value.push(cmd)
    applyChangeStack()
  }
}

const applyChangeStack = () => {
  const newModel = copyModel(props.model)
  for (const cmd of changeStack.value) {
    const diagram = findDiagram(newModel, cmd.diagramPath)
    if (diagram !== null) {
      cmd.apply(diagram)
    }
  }
  myModel.value = newModel
  emit('modified', changeStack.value.length !== 0, newModel)
}

const onEscape = () => {
  contextMenu.show = false
}

const onContextMenu = (e: simu.BlockContextMenuEvent) => {
  contextMenu.show = false
  contextMenu.x = e.x
  contextMenu.y = e.y
  contextMenu.block = e.block
  contextMenu.items = contextMenuItemsForBlock(e.block)
  if (contextMenu.items.length > 0) {
    nextTick(() => {
      contextMenu.show = true
    })
  }
}

const contextMenuItemsForBlock = (b: simu.Block): ContextMenuItem[] => {
  const entries = [
    { title: 'Rename...', handler: onRenameBlock },
    { title: 'Parameters...', handler: onConfigureBlockParams },
    { title: 'Block Properties...', handler: onConfigureBlockProperties },
    { title: 'Rotate', handler: onRotateBlock },
  ]
  const otherEntries = props.block2ContextMenu(b)
  for (const entry of otherEntries) {
    entries.push({
      title: entry,
      handler: (bl: simu.Block) => {
        emit('contextblock', { block: bl, menu: entry })
      },
    })
  }
  return entries
}

const onDoubleClick = (e: simu.BlockDoubleClickEvent) => {
  if (e.block.type === 'Macro') {
    const macroBlock = e.block as simu.MacroBlock
    currentDiagramPath.value.push(macroBlock.name)
    // Notify parent about path change (no selection)
    emit(
      'block_selection',
      [],
      currentDiagramPath.value.map((s) => s),
    )
  } else {
    onConfigureBlockParams(e.block)
  }
}

const onRotateBlock = (block: simu.Block): void => {
  const cmd = new command.BlockRotate(block)
  onCommand(cmd)
}

const onRenameBlock = (block: simu.Block): void => {
  configureBlock.value = block
  showBlockRename.value = true
}

const onConfigureBlockParams = (block: simu.Block): void => {
  configureBlock.value = block
  showBlockParams.value = true
}

const onConfigureBlockProperties = (block: simu.Block): void => {
  configureBlock.value = block
  showBlockProperties.value = true
}

const onBlockDrop = (x: simu.BlockDropEvent): void => {
  x.diagramPath = currentDiagramPath.value.map((s) => s)
  emit('blockDrop', x)
}

const onInteractiveClick = (x: simu.InteractiveClickEvent): void => {
  emit('interactive', x)
}

const onBlockNameChanged = (oldName: string, newName: string): void => {
  const cmd = new command.BlockRename(oldName, newName)
  onCommand(cmd)
}

const onBlockParamsChanged = (newParams: ParamEntry[]): void => {
  const block = configureBlock.value
  if (block !== null) {
    const parameters: simu.Parameters = {}
    for (const p of newParams) {
      parameters[p.id] = p.value
    }
    const cmd = new command.BlockParamsChanged(block.name, parameters)
    onCommand(cmd)
  }
}

const onBlockPropertiesChanged = (block: simu.Block): void => {
  if (block !== null) {
    const cmd = new command.BlockPropertiesChanged(block)
    onCommand(cmd)
  }
}

const onEditCopy = (str: string): void => {
  clipboard.value = str
}

const onBlockSelectionChanged = (blockNames: string[]): void => {
  selectedBlockNames.value = blockNames
  const blocks = currentDiagram.value.blocks.filter((b) => blockNames.includes(b.name))
  emit(
    'block_selection',
    blocks,
    currentDiagramPath.value.map((s) => s),
  )
}
</script>
