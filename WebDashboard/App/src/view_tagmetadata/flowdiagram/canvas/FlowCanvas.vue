<template>
  <div :style="{ overflow: 'scroll', width: '100%', height: height + 'px' }">
    <canvas
      :id="uid"
      tabindex="0"
      @mousedown="onMouseDown"
      @mousemove="onMouseMove"
      @mouseup="onMouseUp"
      @dblclick="onDoubleClick"
      @keydown="onKeyDown"
      @contextmenu="onContextMenu"
      @dragover="onDragOver"
      @drop="onDrop"
      ref="canvasRef"
    ></canvas>
  </div>
</template>

<script lang="ts" setup>
import { ref, onMounted, watch } from 'vue'
import { Painter } from './paint'
import * as draw from './draw_model'
import * as simu from '../model_flow.ts'
import * as command from '../commands.ts'
import * as resources from '../resources.ts'
import * as modules from '../module_types.ts'

type MoveState = 'All' | 'Horizontal' | 'Vertical'

/* Props */
const props = defineProps<{
  height: number
  scale: number
  diagram: simu.FlowDiagram
  changeStackCount: number
  cut: number
  copy: number
  paste: number
  clipboard: string
  layers: string[]
}>()

/* Emits */
const emit = defineEmits<{
  (e: 'block_selection', payload: string[]): void
  (e: 'command', payload: any): void
  (e: 'edit_copy', payload: string): void
  (e: 'interactive', payload: simu.InteractiveClickEvent): void
  (e: 'blockDrop', payload: simu.BlockDropEvent): void
  (e: 'doubleclick', payload: simu.BlockDoubleClickEvent): void
  (e: 'contextmenu', payload: simu.BlockContextMenuEvent): void
  (e: 'escape'): void
}>()

const canvasWidth = 3000
const canvasHeight = 2500

const uid = Date.now().toString() + Math.random().toString(36).substr(2, 9)

const canvasRef = ref<HTMLCanvasElement | null>(null)
const drawingContext = ref<CanvasRenderingContext2D | null>(null)

const drawModel = ref<draw.Diagram>(draw.convertModel2DrawModel(props.diagram))
const drawBlockMap = ref<Map<string, draw.Block>>(new Map<string, draw.Block>())

const painter = ref<Painter | null>(null)

const rightDownStarted = ref(false)
const blockResizing = ref<draw.Block | null>(null)
const drawLine = ref<draw.Line | null>(null)
const moveState = ref<MoveState>('All')
const xOff = ref(0)
const yOff = ref(0)
const blockMoveDX = ref(0)
const blockMoveDY = ref(0)
const lineSegmentsMoved = ref(false)

const dropBlock = ref<simu.Block | null>(null)
const dropDataType = ref('')

const selectedBlockNames = ref<string[]>([])
const surpressContextMenu = ref(false)

/* Helpers to access current values similar to 'this' inside methods */
function getCanvas(): HTMLCanvasElement {
  const c = canvasRef.value
  if (!c) {
    throw new Error('Canvas ref not set')
  }
  return c
}

/* Redraw logic */
function redraw(updateSelection = false): void {
  const layerWater = props.layers.some((ll) => ll === 'Water')
  const layerSignal = props.layers.some((ll) => ll === 'Signal')
  const layerAir = props.layers.some((ll) => ll === 'Air')
  if (!painter.value) {
    return
  }
  const needRedraw = painter.value.paint(layerWater, layerSignal, layerAir)
  if (updateSelection && drawModel.value !== null && drawModel.value !== undefined) {
    const selectedNames = getSelectedBlocks().map((b) => b.name)
    if (stringArraysDiffer(selectedNames, selectedBlockNames.value)) {
      selectedBlockNames.value = selectedNames
      emit(
        'block_selection',
        selectedNames.map((b) => b),
      )
    }
  }
  if (needRedraw) {
    setTimeout(() => {
      redraw()
    }, 100)
  }
}

/* Lifecycle: mounted */
onMounted(() => {
  const canvas = document.getElementById(uid) as HTMLCanvasElement | null
  if (canvas === null) {
    console.error('Canvas element not found: ' + uid)
    return
  }
  const ctx = canvas.getContext('2d')
  if (ctx === null) {
    console.error('Failed to get drawingContext: ' + uid)
    return
  }

  // Handle high-DPI displays for crisp rendering
  const devicePixelRatio = window.devicePixelRatio || 1

  // Set the actual canvas size in memory (scaled for high-DPI)
  canvas.width = canvasWidth * devicePixelRatio
  canvas.height = canvasHeight * devicePixelRatio

  // Set the CSS size to the original dimensions
  canvas.style.width = canvasWidth + 'px'
  canvas.style.height = canvasHeight + 'px'

  // Scale the context so drawing operations automatically get scaled
  ctx.scale(devicePixelRatio, devicePixelRatio)

  canvasRef.value = canvas
  drawingContext.value = ctx
  drawModel.value = draw.convertModel2DrawModel(props.diagram)
  painter.value = new Painter(canvasWidth, canvasHeight, props.scale, drawModel.value, ctx)
  redraw(false)

  drawBlockMap.value.clear()
  for (const b of drawModel.value.blocks) {
    drawBlockMap.value.set(b.name, b)
    if (b.image !== undefined && !b.image.complete) {
      b.image.onload = () => {
        redraw(false)
      }
    }
  }
})

/* Watches */
watch(
  () => props.scale,
  (newScale) => {
    if (!drawingContext.value || !drawModel.value) return
    painter.value = new Painter(canvasWidth, canvasHeight, newScale, drawModel.value, drawingContext.value)
    redraw(false)
  },
)

watch(
  () => props.diagram,
  () => {
    onModelChanged()
  },
)

watch(
  () => props.changeStackCount,
  () => {
    onModelChanged()
  },
)

watch(
  () => props.cut,
  () => {
    const str = selection2Str()
    emit('edit_copy', str)
    sendDeleteSeletionCmd()
  },
)

watch(
  () => props.copy,
  () => {
    const str = selection2Str()
    emit('edit_copy', str)
  },
)

watch(
  () => props.paste,
  () => {
    const str = props.clipboard
    let obj: any
    try {
      obj = JSON.parse(str)
    } catch {
      return
    }
    const blocks: simu.Block[] = obj.blocks
    const lines: simu.Line[] = obj.lines
    if (blocks === undefined || lines === undefined) {
      return
    }
    // Remove any tags from pasted blocks (including inside macros)
    removeTagsFromSimuBlocks(blocks)
    unselectAll()

    const setOfBlockNames = new Set<string>()
    for (const block of drawModel.value.blocks) {
      setOfBlockNames.add(block.name)
    }

    const blocks2Rename = blocks.filter((b) => setOfBlockNames.has(b.name))

    if (blocks2Rename.length > 0) {
      const renameMap: { [index: string]: string } = {}
      for (const block of blocks2Rename) {
        const newName = makeUniqueIdFromSet(setOfBlockNames, block.name)
        renameMap[block.name] = newName
        setOfBlockNames.add(newName)
        block.name = newName
      }

      for (const line of lines) {
        const renamedSource = renameMap[line.source]
        if (renamedSource !== undefined) {
          line.source = renamedSource
        }
        const renamedDest = renameMap[line.dest]
        if (renamedDest !== undefined) {
          line.dest = renamedDest
        }
      }
    }

    const drawBlocks = blocks.map(draw.convertBlock)
    drawBlocks.forEach((b) => (b.selected = true))
    drawModel.value.blocks.push(...drawBlocks)

    const copyPointSelected = (p: simu.Point) => {
      return { x: p.x, y: p.y, selected: true }
    }
    const drawLines = lines.map((line) => {
      const drawLine: draw.Line = {
        source: line.source,
        sourceIdx: line.sourceIdx,
        dest: line.dest,
        destIdx: line.destIdx,
        type: line.type,
        style: draw.LineStyleSignal,
        points: line.points.map(copyPointSelected),
        selected: true,
        layerWater: false,
        layerSignal: false,
        layerAir: false,
      }
      return drawLine
    })
    drawModel.value.lines.push(...drawLines)

    const cmd = new command.AddBlocksAndLines(blocks, lines)
    emit('command', cmd)
  },
)

watch(
  () => props.layers,
  () => {
    redraw(false)
  },
  { deep: true, immediate: false },
)

/* Model change handler */
function onModelChanged(): void {
  const diagram = props.diagram

  const selectedLines: Array<{ idx: number; points: boolean[] }> = []
  if (drawModel.value.lines.length === diagram.lines.length) {
    for (let i = 0; i < drawModel.value.lines.length; ++i) {
      const line = drawModel.value.lines[i]
      if (line.selected) {
        selectedLines.push({ idx: i, points: line.points.map((p) => p.selected) })
      }
    }
  }
  const setOfSelectedBlocks = new Set<string>()
  for (const block of drawModel.value.blocks) {
    if (block.selected) {
      setOfSelectedBlocks.add(block.name)
    }
  }

  drawModel.value = draw.convertModel2DrawModel(diagram)

  for (const block of drawModel.value.blocks) {
    block.selected = setOfSelectedBlocks.has(block.name)
  }
  for (const selLine of selectedLines) {
    const line = drawModel.value.lines[selLine.idx]
    line.selected = true
    if (line.points.length === selLine.points.length) {
      for (let i = 0; i < line.points.length; ++i) {
        line.points[i].selected = selLine.points[i]
      }
    }
  }

  drawBlockMap.value.clear()
  for (const b of drawModel.value.blocks) {
    drawBlockMap.value.set(b.name, b)
  }
  if (drawingContext.value) {
    painter.value = new Painter(canvasWidth, canvasHeight, props.scale, drawModel.value, drawingContext.value)
  }
  redraw(true)
}

/* Selection and utils */
function selection2Str(): string {
  const blocks = getSelectedBlocks().map(makeSimuBlockFromDrawBlock)
  const lines = getSelectedLines().map(makeSimuLineFromDrawLine)
  for (const line of lines) {
    if (blocks.every((b) => b.name !== line.source)) {
      line.source = ''
    }
    if (blocks.every((b) => b.name !== line.dest)) {
      line.dest = ''
    }
  }
  const selection = {
    blocks,
    lines,
  }
  return JSON.stringify(selection)
}

function isAnyBlockSelected(): boolean {
  return drawModel.value.blocks.some((x) => x.selected)
}

function isAnyLineSelected(): boolean {
  return drawModel.value.lines.some((x) => x.selected)
}

function getSelectedBlocks(): draw.Block[] {
  return drawModel.value.blocks.filter((b) => b.selected)
}

function getSelectedLines(): draw.Line[] {
  return drawModel.value.lines.filter((line) => line.selected)
}

/* Mouse / keyboard handlers */
function onMouseDown(e: MouseEvent) {
  const canvas = getCanvas()
  const rect = canvas.getBoundingClientRect()
  const x = translateX(e.clientX - rect.left)
  const y = translateY(e.clientY - rect.top)
  if (e.buttons === 1) {
    onLeftMouseButton_Down(e, x, y)
  } else if (e.buttons === 2) {
    onRightMouseButton_Down(e, x, y)
  }
}

function onLeftMouseButton_Down(e: MouseEvent, x: number, y: number) {
  const block = getBlockBelow(x, y)
  const portAndBlock = getPortBelow(x, y)

  if (portAndBlock !== null && e.buttons === 1 && portAndBlock[0].state === 'open') {
    onMouseDownOnOpenPort(portAndBlock[0], portAndBlock[1], x, y)
  } else if (block !== null) {
    if (!block.selected) {
      onBlockSelected(block, e.shiftKey)
      redraw(true)
    } else {
      const left = block.x + block.w - 3
      const right = left + 6
      const top = block.y + block.h - 3
      const bottom = top + 6
      blockResizing.value = left < x && x < right && top < y && y < bottom ? block : null
    }
  } else if (drawLine.value !== null) {
    const pt = drawLine.value.tmpDraw!
    const newPT = { x: pt.x, y: pt.y, selected: false }
    if (drawLine.value.source !== '') {
      drawLine.value.points.push(newPT)
    } else {
      drawLine.value.points.unshift(newPT)
    }
    xOff.value = pt.x
    yOff.value = pt.y
    moveState.value = invertMoveState(moveState.value)
    redraw(false)
  } else {
    const lineObj: { line: draw.Line; points: number[] } | null = getLineBelow(x, y)

    if (lineObj !== null) {
      unselectAll()
      lineObj.line.selected = true
      selectLinePoints(lineObj.line, lineObj.points, true)
      redraw(false)
    } else {
      unselectAll()
      drawModel.value.selectRect = { x, y, w: 0, h: 0 }
      redraw(true)
    }
  }
}

function onRightMouseButton_Down(_e: MouseEvent, _x: number, _y: number) {
  rightDownStarted.value = true
  surpressContextMenu.value = false
}

function onMouseDownOnOpenPort(port: draw.Port, block: draw.Block, x: number, y: number) {
  const portIdx = block.ports.findIndex((p) => p === port)
  unselectAll()
  if (drawLine.value === null) {
    const newLine: draw.Line = {
      source: !port.input ? block.name : '',
      sourceIdx: !port.input ? portIdx : -1,
      dest: port.input ? block.name : '',
      destIdx: port.input ? portIdx : -1,
      type: port.type,
      style: draw.lineStyleFromLayer(port.layerWater, port.layerSignal, port.layerAir),
      points: [],
      selected: false,
      start: !port.input ? { x: port.x, y: port.y } : undefined,
      end: port.input ? { x: port.x, y: port.y } : undefined,
      tmpDraw: { x, y },
      layerWater: port.layerWater,
      layerSignal: port.layerSignal,
      layerAir: port.layerAir,
    }
    drawModel.value.lines.push(newLine)
    port.lineIdx = drawModel.value.lines.length
    port.state = 'connected'
    drawLine.value = newLine
    moveState.value = port.orientation === 'left' || port.orientation === 'right' ? 'Horizontal' : 'Vertical'
    yOff.value = port.y
    xOff.value = port.x
  } else {
    const line = drawLine.value
    if (line && isCompatbile(line, port)) {
      line.source = line.source === '' ? block.name : line.source
      line.sourceIdx = line.sourceIdx === -1 ? portIdx : line.sourceIdx
      line.dest = line.dest === '' ? block.name : line.dest
      line.destIdx = line.destIdx === -1 ? portIdx : line.destIdx
      normalizeLine(line)
      drawLine.value = null
      const cmd = new command.LineConnectBlocks(
        line.source,
        line.sourceIdx,
        line.dest,
        line.destIdx,
        line.type /*, line.width*/,
        drawPointsToSimuPoints(line.points),
      )
      emit('command', cmd)
    }
  }
  redraw(true)
}

function onMouseUp(e: MouseEvent) {
  const canvas = getCanvas()
  const rect = canvas.getBoundingClientRect()
  const x = translateX(e.clientX - rect.left)
  const y = translateY(e.clientY - rect.top)
  const element = getInteractiveElementBelow(x, y)
  rightDownStarted.value = false
  if (drawModel.value.selectRect !== undefined) {
    selectAllBlocksAndLinesInRect(drawModel.value.selectRect)
    drawModel.value.selectRect = undefined
    redraw(true)
  } else if (blockMoveDX.value !== 0 || blockMoveDY.value !== 0) {
    const selected = getSelectedBlocks().map((b) => b.name)
    const cmd = new command.Move(selected, blockMoveDX.value, blockMoveDY.value, getChangedLines())
    blockMoveDX.value = 0
    blockMoveDY.value = 0
    emit('command', cmd)
    surpressContextMenu.value = e.button === 2
  } else if (lineSegmentsMoved.value) {
    const cmd = new command.Move([], 0, 0, getChangedLines())
    lineSegmentsMoved.value = false
    emit('command', cmd)
  } else if (blockResizing.value !== null) {
    const cmd = new command.ResizeBlock(blockResizing.value.name, blockResizing.value.w, blockResizing.value.h, getChangedLines())
    blockResizing.value = null
    emit('command', cmd)
  } else if (element !== null) {
    const block = props.diagram.blocks.find((b) => b.name === element.block)
    if (block !== undefined) {
      const ev: simu.InteractiveClickEvent = {
        block,
        type: element.type,
        x: element.x,
        y: element.y,
        id: element.id,
      }
      emit('interactive', ev)
    }
  }
}

/* Helper: get changed lines since last saved diagram */
function getChangedLines(): command.LinePoints[] {
  const res: command.LinePoints[] = []
  if (props.diagram.lines.length !== drawModel.value.lines.length) {
    return []
  }
  for (let i = 0; i < props.diagram.lines.length; ++i) {
    const lineSimu: simu.Line = props.diagram.lines[i]
    const lineDraw: draw.Line = drawModel.value.lines[i]
    if (!pointsEqual(lineSimu.points, lineDraw.points)) {
      res.push({
        lineIdx: i,
        points: lineDraw.points.map((pt) => ({ x: pt.x, y: pt.y })),
      })
    }
  }
  return res
}

function pointsEqual(pointsA: simu.Point[], pointsB: draw.Point[]): boolean {
  if (pointsA.length !== pointsB.length) {
    return false
  }
  for (let i = 0; i < pointsA.length; ++i) {
    const pt1 = pointsA[i]
    const pt2 = pointsB[i]
    if (pt1.x !== pt2.x || pt1.y !== pt2.y) {
      return false
    }
  }
  return true
}

function onDoubleClick(e: MouseEvent) {
  const canvas = getCanvas()
  const rect = canvas.getBoundingClientRect()
  const x = translateX(e.clientX - rect.left)
  const y = translateY(e.clientY - rect.top)
  const drawBlock = getBlockBelow(x, y)
  if (drawBlock === null) {
    return
  }
  const event: simu.BlockDoubleClickEvent = {
    x: e.clientX,
    y: e.clientY,
    block: makeSimuBlockFromDrawBlock(drawBlock),
  }
  emit('doubleclick', event)
}

function onContextMenu(e: MouseEvent) {
  e.preventDefault()
  if (surpressContextMenu.value) {
    surpressContextMenu.value = false
    return
  }
  const canvas = getCanvas()
  const rect = canvas.getBoundingClientRect()
  const x = translateX(e.clientX - rect.left)
  const y = translateY(e.clientY - rect.top)
  const drawBlock = getBlockBelow(x, y)
  if (drawBlock === null) {
    return
  }
  const event: simu.BlockContextMenuEvent = {
    x: e.clientX,
    y: e.clientY,
    block: makeSimuBlockFromDrawBlock(drawBlock),
  }
  emit('contextmenu', event)
}

function onKeyDown(e: KeyboardEvent) {
  if (e.key === 'Delete') {
    sendDeleteSeletionCmd()
  }

  if (e.key === 'Escape') {
    emit('escape')
    if (drawLine.value !== null) {
      drawLine.value = null
      const c = canvasRef.value
      if (c) c.style.cursor = 'default'
      onModelChanged()
    }
  }
}

function sendDeleteSeletionCmd(): void {
  const blockIdx: number[] = []
  for (let i = 0; i < drawModel.value.blocks.length; ++i) {
    const b = drawModel.value.blocks[i]
    if (b.selected) {
      blockIdx.push(i)
    }
  }

  const lineIdx: number[] = []
  for (let i = 0; i < drawModel.value.lines.length; ++i) {
    const line = drawModel.value.lines[i]
    if (line.selected) {
      lineIdx.push(i)
    }
  }

  const cmd = new command.DeleteBlocksAndLines(blockIdx, lineIdx)
  emit('command', cmd)
}

function onMouseMove(e: MouseEvent) {
  const canvas = getCanvas()
  const rect = canvas.getBoundingClientRect()
  const x = translateX(e.clientX - rect.left)
  const y = translateY(e.clientY - rect.top)
  const moveX = e.movementX / props.scale
  const moveY = e.movementY / props.scale

  if (drawLine.value !== null) {
    switch (moveState.value) {
      case 'All':
        drawLine.value.tmpDraw = { x, y }
        break
      case 'Horizontal':
        drawLine.value.tmpDraw = { x, y: yOff.value }
        break
      case 'Vertical':
        drawLine.value.tmpDraw = { x: xOff.value, y }
        break
    }
    redraw(false)
  } else if (e.buttons === 1 && drawModel.value.selectRect !== undefined) {
    drawModel.value.selectRect.w += moveX
    drawModel.value.selectRect.h += moveY
    redraw(false)
  } else if (e.buttons === 1 && blockResizing.value !== null) {
    blockResizing.value.w += moveX
    blockResizing.value.h += moveY
    updateBlock(blockResizing.value)
    redraw(false)
  } else if (e.buttons === 1 && isAnyBlockSelected()) {
    moveSelectedBlocksAndLines(e)
    redraw(false)
  } else if (e.buttons === 1 && isAnyLineSelected()) {
    moveSelectedLineSegments(e)
    redraw(false)
  } else if (e.buttons === 2 && isAnyBlockSelected()) {
    if (rightDownStarted.value) {
      rightDownStarted.value = false
      duplicateSelection()
    } else {
      moveSelectedBlocksAndLines(e)
    }
    redraw(true)
  }
  updateMouseCursor(e, x, y)
}

function updateMouseCursor(e: MouseEvent, x: number, y: number) {
  let hoverPort = false
  let hoverInteractiveElement = false
  if (e.buttons === 0) {
    const portAndBlock = getPortBelow(x, y)
    const port = portAndBlock === null ? null : portAndBlock[0]
    hoverPort = port !== null && port.state === 'open' && (drawLine.value === null || isCompatbile(drawLine.value, port))

    const element = getInteractiveElementBelow(x, y)
    hoverInteractiveElement = element !== null
  }

  const c = canvasRef.value
  if (!c) return

  if (hoverPort) {
    c.style.cursor = 'crosshair'
  } else if (hoverInteractiveElement) {
    c.style.cursor = 'pointer'
  } else {
    c.style.cursor = drawLine.value !== null ? resources.CURSOR_PEN : 'default'
  }
}

function moveSelectedBlocksAndLines(e: MouseEvent) {
  const moveX = e.movementX / props.scale
  const moveY = e.movementY / props.scale
  blockMoveDX.value += moveX
  blockMoveDY.value += moveY
  const selectedBlocks = getSelectedBlocks()
  for (const block of selectedBlocks) {
    block.x += moveX
    block.y += moveY
  }
  const selectedLines = getSelectedLines()
  for (const line of selectedLines) {
    for (const pt of line.points) {
      pt.x += moveX
      pt.y += moveY
    }
  }
  for (const block of selectedBlocks) {
    updateBlock(block)
  }
}

function moveSelectedLineSegments(e: MouseEvent) {
  const moveX = e.movementX / props.scale
  const moveY = e.movementY / props.scale
  for (const line of getSelectedLines()) {
    const selectedPts = line.points.filter((pt) => pt.selected)
    if (selectedPts.length >= 2) {
      lineSegmentsMoved.value = true
      const vertical = selectedPts.every((pt) => pt.x === selectedPts[0].x)
      const horizont = selectedPts.every((pt) => pt.y === selectedPts[0].y)
      for (const pt of selectedPts) {
        if (vertical) {
          pt.x += moveX
        } else if (horizont) {
          pt.y += moveY
        } else {
          pt.x += moveX
          pt.y += moveY
        }
      }
    }
  }
}

function onBlockSelected(block: draw.Block, shift: boolean) {
  if (shift) {
    // keep other selections
    block.selected = true
  } else {
    unselectAll()
    block.selected = true
  }
}

function unselectAll() {
  for (const block of drawModel.value.blocks) {
    block.selected = false
  }
  for (const line of drawModel.value.lines) {
    line.selected = false
    for (const pt of line.points) {
      pt.selected = false
    }
  }
}

/* Hit detection utilities */
function getBlockBelow(x: number, y: number, delta = 0): draw.Block | null {
  const blocks = drawModel.value.blocks
  for (let i = blocks.length - 1; i >= 0; --i) {
    const b = blocks[i]
    const left = b.x - delta
    const top = b.y - delta
    const right = b.x + b.w + Math.max(3, delta) // +3 for selection rect
    const bottom = b.y + b.h + Math.max(3, delta) // +3 for selection rect
    if (left < x && x < right && top < y && y < bottom) {
      return b
    }
  }
  return null
}

function getPortBelow(x: number, y: number): [draw.Port, draw.Block] | null {
  const block = getBlockBelow(x, y, 6)
  if (block === null) {
    return null
  }
  for (const p of block.ports) {
    const delta = 6
    const left = p.x - delta
    const top = p.y - delta
    const right = p.x + delta
    const bottom = p.y + delta
    if (left < x && x < right && top < y && y < bottom) {
      return [p, block]
    }
  }
  return null
}

function getInteractiveElementBelow(x: number, y: number, delta = 0): modules.InteractiveElement | null {
  if (!painter.value) {
    return null
  }
  const elements = painter.value.interactiveElements
  for (let i = elements.length - 1; i >= 0; --i) {
    const b = elements[i]
    const left = b.x - delta
    const top = b.y - delta
    const right = b.x + b.w + delta
    const bottom = b.y + b.h + delta
    if (left < x && x < right && top < y && y < bottom) {
      return b
    }
  }
  return null
}

function getLineBelow(x: number, y: number): { line: draw.Line; points: number[] } | null {
  const lines = drawModel.value.lines
  for (let i = lines.length - 1; i >= 0; --i) {
    const line = lines[i]
    const hit: number[] | null = isLineHit(x, y, line)
    if (hit !== null) {
      return { line, points: hit }
    }
  }
  return null
}

function isLineHit(x: number, y: number, line: draw.Line): number[] | null {
  const dx = 3

  const isOnLine = (p1: draw.Point, p2: draw.Point) => {
    const dd = Math.abs((p2.x - p1.x) * (y - p2.y) - (p2.y - p1.y) * (x - p2.x))
    return (
      x >= Math.min(p1.x, p2.x) - dx &&
      x <= Math.max(p1.x, p2.x) + dx &&
      y >= Math.min(p1.y, p2.y) - dx &&
      y <= Math.max(p1.y, p2.y) + dx &&
      dd <= dx * Math.max(Math.abs(p2.x - p1.x), Math.abs(p2.y - p1.y))
    )
  }

  for (let i = 0; i < line.points.length - 1; ++i) {
    if (isOnLine(line.points[i], line.points[i + 1])) {
      return [i, i + 1]
    }
  }

  if (line.start !== undefined && line.points.length > 0 && isOnLine(line.start, line.points[0])) {
    return [0]
  }

  if (line.end !== undefined && line.points.length > 0 && isOnLine(line.end, line.points[line.points.length - 1])) {
    return [line.points.length - 1]
  }

  if (line.start !== undefined && line.end !== undefined && line.points.length === 0 && isOnLine(line.start, line.end)) {
    return []
  }

  return null
}

function selectLinePoints(line: draw.Line, points: number[], select: boolean): void {
  for (const p of points) {
    line.points[p].selected = select
  }
}

function isCompatbile(line: draw.Line, port: draw.Port): boolean {
  return line.type === port.type && ((port.input && line.source !== '') || (!port.input && line.dest !== ''))
}

function invertMoveState(state: MoveState): MoveState {
  switch (state) {
    case 'Horizontal':
      return 'Vertical'
    case 'Vertical':
      return 'Horizontal'
    default:
      return state
  }
}

function selectAllBlocksAndLinesInRect(r: draw.Rect) {
  unselectAll()
  const left = Math.min(r.x, r.x + r.w)
  const right = Math.max(r.x, r.x + r.w)
  const top = Math.min(r.y, r.y + r.h)
  const bottom = Math.max(r.y, r.y + r.h)
  const ptInRect = (x: number, y: number) => left < x && x < right && top < y && y < bottom
  for (const b of drawModel.value.blocks) {
    b.selected = ptInRect(b.x, b.y) || ptInRect(b.x + b.w, b.y) || ptInRect(b.x, b.y + b.h) || ptInRect(b.x + b.w, b.y + b.h)
  }
  for (const line of drawModel.value.lines) {
    const startInRect = line.start !== undefined && ptInRect(line.start.x, line.start.y)
    const endInRect = line.end !== undefined && ptInRect(line.end.x, line.end.y)
    line.selected = startInRect || endInRect || line.points.some((pt) => ptInRect(pt.x, pt.y))
    if (line.selected) {
      for (const pt of line.points) {
        pt.selected = true
      }
    }
  }
}

function duplicateSelection() {
  const allBlocks = drawModel.value.blocks
  const selectedBlocks = getSelectedBlocks()
  const copiesBlocks: draw.Block[] = []
  const renameMap: { [index: string]: string } = {}

  const portBlocks = allBlocks.filter((b) => b.type === 'Port')
  let inCounter = portBlocks.filter((p) => (p as draw.PortBlock).io === 'In').length
  let outCounter = portBlocks.filter((p) => (p as draw.PortBlock).io === 'Out').length

  for (const block of selectedBlocks) {
    const copy: draw.Block = JSON.parse(JSON.stringify(block))
    copy.name = makeUniqueID(allBlocks, copiesBlocks, copy.name)
    copiesBlocks.push(copy)
    renameMap[block.name] = copy.name
    // Remove tags from duplicated blocks (and inside macros)
    if (copy.type === 'Module') {
      ;(copy as draw.ModuleBlock).tags = []
    } else if (copy.type === 'Macro') {
      const mac = copy as draw.MacroBlock
      try {
        const diagram: simu.FlowDiagram = JSON.parse(mac.simuDiagramStr)
        removeTagsFromSimuDiagram(diagram)
        mac.simuDiagramStr = JSON.stringify(diagram)
      } catch {
        // ignore JSON parse errors for safety
      }
    }
    if (copy.type === 'Port') {
      const port = copy as draw.PortBlock
      if (port.io === 'In') {
        inCounter += 1
        port.index = inCounter
      } else {
        outCounter += 1
        port.index = outCounter
      }
    }
  }

  const copiesLines: draw.Line[] = []
  for (const line of getSelectedLines()) {
    const copy: draw.Line = JSON.parse(JSON.stringify(line))
    const renamedSource = renameMap[copy.source]
    if (renamedSource !== undefined) {
      copy.source = renamedSource
    }
    const renamedDest = renameMap[copy.dest]
    if (renamedDest !== undefined) {
      copy.dest = renamedDest
    }
    copiesLines.push(copy)
  }

  unselectAll()

  for (const block of copiesBlocks) {
    drawModel.value.blocks.push(block)
  }
  for (const line of copiesLines) {
    drawModel.value.lines.push(line)
  }

  const cmd = new command.AddBlocksAndLines(copiesBlocks.map(makeSimuBlockFromDrawBlock), copiesLines.map(makeSimuLineFromDrawLine))
  emit('command', cmd)
}

function updateBlock(b: draw.Block): void {
  const isSelected = b.selected

  const lines: number[] = []
  for (const p of b.ports) {
    if (p.lineIdx !== undefined) {
      lines.push(p.lineIdx)
    }
  }

  const simuBlock = makeSimuBlockFromDrawBlock(b)
  Object.assign(b, draw.convertBlock(simuBlock))
  b.selected = isSelected
  drawBlockMap.value.set(b.name, b)

  for (const lineIdx of lines) {
    const drawLineObj: draw.Line = drawModel.value.lines[lineIdx]
    const simuLine: simu.Line = makeSimuLineFromDrawLine(drawLineObj)
    const lineIsSelected = drawLineObj.selected
    const points = drawLineObj.points
    Object.assign(drawLineObj, draw.convertLine(simuLine, lineIdx, drawBlockMap.value))
    for (let i = 0; i < points.length; ++i) {
      drawLineObj.points[i].selected = points[i].selected
    }
    normalizeLine(drawLineObj)
    drawLineObj.selected = lineIsSelected
  }
}

function normalizeLine(line: draw.Line): void {
  const blockSrc = drawBlockMap.value.get(line.source)
  const blockDst = drawBlockMap.value.get(line.dest)

  const portSrc = blockSrc !== undefined ? blockSrc.ports[line.sourceIdx] : undefined
  const portDst = blockDst !== undefined ? blockDst.ports[line.destIdx] : undefined

  const srcHorizontal = portSrc === undefined ? true : portSrc.orientation === 'left' || portSrc.orientation === 'right'

  if (portSrc !== undefined) {
    if (line.points.length === 0) {
      addPoint(line, portSrc)
    } else {
      const p1 = line.points[0]
      if (srcHorizontal) {
        p1.y = portSrc.y
      } else {
        p1.x = portSrc.x
      }
    }
  }

  if (portDst !== undefined) {
    const dstHorizontal = portDst.orientation === 'left' || portDst.orientation === 'right'
    if (line.points.length === 1 && dstHorizontal === srcHorizontal) {
      addPoint(line, portDst)
      const p1 = line.points[0]
      const p2 = line.points[1]
      const pMid = dstHorizontal ? { x: p2.x, y: p1.y, selected: false } : { x: p1.x, y: p2.y, selected: false }
      line.points.splice(1, 0, pMid)
    } else {
      if (line.points.length === 0) {
        addPoint(line, portDst)
      }
      const p1 = line.points[line.points.length - 1]
      if (dstHorizontal) {
        p1.y = portDst.y
      } else {
        p1.x = portDst.x
      }
    }
  }
}

function addPoint(line: draw.Line, port: draw.Port) {
  let x = 0
  let y = 0
  const off = 2 * draw.grid
  switch (port.orientation) {
    case 'left':
      x = port.x - off
      y = port.y
      break
    case 'right':
      x = port.x + off
      y = port.y
      break
    case 'top':
      x = port.x
      y = port.y + off
      break
    case 'bottom':
      x = port.x
      y = port.y - off
      break
  }
  const isSrc = !port.input
  if (isSrc) {
    line.points.unshift({ x, y, selected: false })
  } else {
    line.points.push({ x, y, selected: false })
  }
}

function translateX(x: number): number {
  return x / props.scale
}

function translateY(y: number): number {
  return y / props.scale
}

function makeSimuBlockFromDrawBlock(block: draw.Block): simu.Block {
  const x = Math.round(block.x)
  const y = Math.round(block.y)
  const w = Math.round(block.w)
  const h = Math.round(block.h)

  if (block.type === 'Module') {
    const b = block as draw.ModuleBlock
    const res: simu.ModuleBlock = {
      name: b.name,
      type: b.type,
      moduleType: b.moduleType.id,
      parameters: b.parameters,
      x,
      y,
      w,
      h,
      frame: b.frame,
      drawFrame: b.drawFrame,
      drawName: b.drawName,
      drawPortLabel: b.drawPortLabel,
      flipName: b.flipName,
      rotation: b.rotation,
      colorForeground: b.colorForeground,
      colorBackground: b.colorBackground,
      icon: b.icon,
      font: b.font,
      tags: b.tags,
    }
    return res
  } else if (block.type === 'Macro') {
    const b = block as draw.MacroBlock
    const diagram: simu.FlowDiagram = JSON.parse(b.simuDiagramStr)
    const res: simu.MacroBlock = {
      name: b.name,
      type: b.type,
      diagram,
      x,
      y,
      w,
      h,
      frame: b.frame,
      drawFrame: b.drawFrame,
      drawName: b.drawName,
      drawPortLabel: b.drawPortLabel,
      flipName: b.flipName,
      rotation: b.rotation,
      colorForeground: b.colorForeground,
      colorBackground: b.colorBackground,
      icon: b.icon,
      font: b.font,
    }
    return res
  } else {
    const b = block as draw.PortBlock
    const res: simu.PortBlock = {
      name: b.name,
      type: b.type,
      io: b.io,
      index: b.index,
      lineType: b.lineType,
      dimension: b.dimension,
      x,
      y,
      w,
      h,
      frame: b.frame,
      drawFrame: b.drawFrame,
      drawName: b.drawName,
      drawPortLabel: b.drawPortLabel,
      flipName: b.flipName,
      rotation: b.rotation,
      colorForeground: b.colorForeground,
      colorBackground: b.colorBackground,
      icon: b.icon,
      font: b.font,
    }
    return res
  }
}

function makeSimuLineFromDrawLine(line: draw.Line): simu.Line {
  return {
    type: line.type,
    source: line.source,
    sourceIdx: line.sourceIdx,
    dest: line.dest,
    destIdx: line.destIdx,
    // width: line.width,
    points: drawPointsToSimuPoints(line.points),
  }
}

function drawPointsToSimuPoints(points: draw.SelectablePoint[]): simu.Point[] {
  const copyPoint = (p: draw.SelectablePoint) => {
    const x = Math.round(p.x)
    const y = Math.round(p.y)
    return { x, y }
  }
  return points.map(copyPoint)
}

// Remove tags from blocks to avoid duplicate tag IDs on paste/duplicate
function removeTagsFromSimuDiagram(diagram: simu.FlowDiagram): void {
  for (const b of diagram.blocks) {
    if (b.type === 'Module') {
      const mb = b as simu.ModuleBlock
      mb.tags = []
    } else if (b.type === 'Macro') {
      const mac = b as simu.MacroBlock
      removeTagsFromSimuDiagram(mac.diagram)
    }
  }
}

function removeTagsFromSimuBlocks(blocks: simu.Block[]): void {
  for (const b of blocks) {
    if (b.type === 'Module') {
      ;(b as simu.ModuleBlock).tags = []
    } else if (b.type === 'Macro') {
      removeTagsFromSimuDiagram((b as simu.MacroBlock).diagram)
    }
  }
}

function onDragOver(e: DragEvent) {
  const dataType = simu.GlobalObj.dragType
  if (dataType === '') {
    return
  }
  if (dataType === 'simu-block') {
    e.preventDefault()
  } else {
    const canvas = getCanvas()
    const rect = canvas.getBoundingClientRect()
    const x = translateX(e.clientX - rect.left)
    const y = translateY(e.clientY - rect.top)
    const block = getBlockBelow(x, y)
    if (block === null) {
      return
    }
    const dropType = block.supportedDropTypes.find((dt) => dataType === dt)
    if (dropType === undefined) {
      return
    }
    dropBlock.value = block
    dropDataType.value = dropType
    e.preventDefault()
  }
}

function onDrop(ev: DragEvent) {
  simu.GlobalObj.dragType = ''
  if (ev.dataTransfer === null) {
    return
  }

  const simuBlock = ev.dataTransfer.getData('simu-block')
  if (simuBlock !== '') {
    const canvas = getCanvas()
    const rect = canvas.getBoundingClientRect()
    const x = translateX(ev.clientX - rect.left)
    const y = translateY(ev.clientY - rect.top)
    ev.preventDefault()
    const allBlocks = drawModel.value.blocks
    const copy: simu.Block = JSON.parse(simuBlock)
    copy.name = makeUniqueID(allBlocks, [], copy.name)
    copy.x = x
    copy.y = y
    const cmd = new command.AddBlocksAndLines([copy], [])
    emit('command', cmd)
  } else {
    if (dropBlock.value === null) {
      return
    }
    ev.preventDefault()
    const dropData: simu.BlockDropEvent = {
      diagram: props.diagram,
      blockName: dropBlock.value.name,
      blockType: dropBlock.value.type,
      type: dropDataType.value,
      data: ev.dataTransfer.getData(dropDataType.value),
      x: ev.pageX,
      y: ev.pageY,
    }
    emit('blockDrop', dropData)
  }
}

function makeUniqueIdFromSet(takenNames: Set<string>, id: string): string {
  const digits = countTerminalDigits(id)
  const base = id.substring(0, id.length - digits)
  let i = digits === 0 ? 1 : parseInt(id.substring(id.length - digits, id.length), 10) + 1
  let name = base + i
  while (takenNames.has(name)) {
    i += 1
    name = base + i
  }
  return name
}

function makeUniqueID(blocksA: draw.Block[], blocksB: draw.Block[], id: string): string {
  const setOfBlockNames = new Set<string>()
  for (const block of blocksA) {
    setOfBlockNames.add(block.name)
  }
  for (const block of blocksB) {
    setOfBlockNames.add(block.name)
  }
  return makeUniqueIdFromSet(setOfBlockNames, id)
}

function countTerminalDigits(s: string): number {
  const lastIdx = s.length - 1
  for (let i = lastIdx; i >= 0; --i) {
    const c = s.charAt(i)
    if (c !== '0' && c !== '1' && c !== '2' && c !== '3' && c !== '4' && c !== '5' && c !== '6' && c !== '7' && c !== '8' && c !== '9') {
      return lastIdx - i
    }
  }
  return s.length
}

function stringArraysDiffer(a: string[], b: string[]): boolean {
  if (a.length !== b.length) {
    return true
  }
  for (let i = 0; i < a.length; ++i) {
    if (a[i] !== b[i]) {
      return true
    }
  }
  return false
}
</script>
