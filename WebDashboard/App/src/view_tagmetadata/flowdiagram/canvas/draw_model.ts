import * as modules from '../module_types'
import type { Tag } from '../../model_tags'

export interface Diagram {
  blocks: Block[]
  lines: Line[]
  selectRect?: Rect
}

export interface Rect {
  x: number
  y: number
  w: number
  h: number
}

export interface Block {
  name: string
  type: BlockType
  x: number
  y: number
  w: number
  h: number
  ports: Port[]
  selected: boolean
  drawFrame: boolean
  drawName: boolean
  drawPortLabel: boolean
  flipName: boolean
  rotation: Rotation
  colorForeground?: string // default is black
  colorBackground?: string // default is white
  frame?: Frame
  icon?: Icon
  image?: HTMLImageElement
  font?: Font
  fontStr: string
  supportedDropTypes: string[]
}

export type Rotation = '0' | '90' | '180' | '270'

export type BlockType = 'Module' | 'Macro' | 'Port'

export interface ModuleBlock extends Block {
  type: 'Module'
  moduleType: modules.ModuleBlockType
  parameters: Parameters
  customDraw?: (ctx: modules.DrawingContext) => void
  tags: Tag[]
}

export interface MacroBlock extends Block {
  type: 'Macro'
  simuDiagramStr: string
}

export interface PortBlock extends Block {
  type: 'Port'
  io: IO
  index: number
  lineType: LineType
  dimension: number
}

export type LineType = 'Water' | 'Air' | 'Signal'

export type IO = 'In' | 'Out'

export interface Parameters {
  [key: string]: string
}

export interface AssignedTag {
  id: string
  name: string
  comment: string
  distLeft?: number
  distTop?: number
  depth?: number
}

export interface Port {
  id: string
  orientation: Orientation
  input: boolean
  type: string // e.g. Signal,3
  x: number
  y: number
  state: PortState
  lineIdx?: number
  layerWater: boolean
  layerSignal: boolean
  layerAir: boolean
}

export type Orientation = 'left' | 'right' | 'top' | 'bottom'

export type PortState = 'open' | 'connected'

export interface Frame {
  shape: Shape
  strokeWidth: number
  strokeColor: string
  fillColor: string
  shadow: boolean
  var1?: number
  var2?: number
}

export type Shape = 'Rectangle' | 'RoundedRectangle' | 'Circle'

export interface Icon {
  name: string // probably a key in a global registry of icons
  x: number
  y: number
  w: number
  h: number
  // flip: boolean
  rotate: boolean
}

export interface Line {
  source: string // id of source block
  sourceIdx: number
  dest: string // id of destination block
  destIdx: number
  type: string
  style: LineStyle
  points: SelectablePoint[]
  selected: boolean
  start?: Point
  end?: Point
  tmpDraw?: Point
  layerWater: boolean
  layerSignal: boolean
  layerAir: boolean
}

export interface LineStyle {
  width: number
  color: string
}

export const LineStyleWater: LineStyle = {
  width: 3,
  color: 'blue',
}

export const LineStyleSignal: LineStyle = {
  width: 1,
  color: 'grey',
}

export const LineStyleAir: LineStyle = {
  width: 3,
  color: '#9D9DFF',
}

export interface Point {
  x: number
  y: number
}

export interface SelectablePoint {
  x: number
  y: number
  selected: boolean
}

export interface Font {
  family: string
  size: number // in pixels
  style: FontStyle
  weight: FontWeight
}

export type FontStyle = 'Normal' | 'Italic' | 'Oblique'

export type FontWeight = 'Normal' | 'Bold' | 'Thin'

//////////////////////////////////////////////////////////////////////////////////////
// CONVERT HIGH-LEVEL SIMU MODEL TO LOW_LEVEL DRAW MODEL
//////////////////////////////////////////////////////////////////////////////////////

import * as simu from '../model_flow'

const imageCache: { [key: string]: HTMLImageElement } = {}

export function convertModel2DrawModel(model: simu.FlowDiagram): Diagram {
  const convertedBlocks = model.blocks.map(convertBlock)
  const blockMap = new Map<string, Block>()
  for (const b of convertedBlocks) {
    blockMap.set(b.name, b)
  }

  return {
    blocks: convertedBlocks,
    lines: model.lines.map((line, i) => convertLine(line, i, blockMap)),
  }
}

export function convertBlock(b: simu.Block): Block {
  let img: HTMLImageElement | undefined
  if (b.icon !== undefined && b.icon.name !== '') {
    const url = b.icon.name
    img = imageCache[url]
    if (img === undefined) {
      const dashboardApp = (window.parent as any).dashboardApp
      const fullUrl = dashboardApp.getBackendUrl() + '/ctx/block_images/' + url
      if (fullUrl) {
        img = document.createElement('img')
        img.src = fullUrl
        imageCache[url] = img
      }
    }
  }

  if (b.type === 'Module') {
    const moduleBlock = b as simu.ModuleBlock
    const type: modules.ModuleBlockType = modules.MapOfModuleTypes[moduleBlock.moduleType]
    const ios: modules.IO[] = type !== undefined ? type.defineIOs(moduleBlock.parameters) : []
    const ports = ios.map((io) => makePortFromIO(b, io, ios))

    const res: ModuleBlock = {
      name: b.name,
      type: b.type,
      moduleType: type,
      parameters: moduleBlock.parameters,
      x: b.x,
      y: b.y,
      w: b.w,
      h: b.h,
      ports,
      selected: false,
      drawFrame: b.drawFrame,
      drawName: b.drawName,
      drawPortLabel: b.drawPortLabel === undefined ? false : b.drawPortLabel,
      flipName: b.flipName,
      rotation: b.rotation !== undefined ? b.rotation : '0',
      colorForeground: b.colorForeground, // default is black
      colorBackground: b.colorBackground, // default is white
      frame: b.frame,
      icon: b.icon,
      image: img,
      font: b.font,
      fontStr: makeFont(b.font),
      customDraw: type.customDraw,
      supportedDropTypes: type.supportedDropTypes === undefined ? [] : type.supportedDropTypes,
      tags: moduleBlock.tags,
    }
    return res
  } else if (b.type === 'Macro') {
    const macroBlock = b as simu.MacroBlock
    const ios = getMacroBlockIOs(macroBlock)
    const ports = ios.map((io) => makePortFromIO(macroBlock, io, ios))
    const res: MacroBlock = {
      name: b.name,
      type: b.type,
      simuDiagramStr: JSON.stringify(macroBlock.diagram),
      x: b.x,
      y: b.y,
      w: b.w,
      h: b.h,
      ports,
      selected: false,
      drawFrame: b.drawFrame,
      drawName: b.drawName,
      flipName: b.flipName,
      rotation: b.rotation !== undefined ? b.rotation : '0',
      drawPortLabel: b.drawPortLabel === undefined ? true : b.drawPortLabel,
      colorForeground: b.colorForeground, // default is black
      colorBackground: b.colorBackground, // default is white
      frame: b.frame,
      icon: b.icon,
      image: img,
      font: b.font,
      fontStr: makeFont(b.font),
      supportedDropTypes: [],
    }
    return res
  } else {
    const portBlock = b as simu.PortBlock
    const ios = getPortBlockIOs(portBlock)
    const ports = ios.map((io) => makePortFromIO(portBlock, io, ios))
    const res: PortBlock = {
      name: b.name,
      type: b.type,
      io: portBlock.io,
      index: portBlock.index,
      lineType: portBlock.lineType,
      dimension: portBlock.dimension,
      x: b.x,
      y: b.y,
      w: b.w,
      h: b.h,
      ports,
      selected: false,
      drawFrame: b.drawFrame,
      drawName: b.drawName,
      drawPortLabel: false,
      flipName: b.flipName,
      rotation: b.rotation !== undefined ? b.rotation : '0',
      colorForeground: b.colorForeground, // default is black
      colorBackground: b.colorBackground, // default is white
      frame: b.frame,
      icon: b.icon,
      image: img,
      font: b.font,
      fontStr: makeFont(b.font),
      supportedDropTypes: [],
    }
    return res
  }
}

function getPortBlockIOs(port: simu.PortBlock): modules.IO[] {
  const result: modules.IO = {
    id: 'x',
    orientation: 'right',
    input: false,
    relPos: -1000,
    type: 'Signal',
  }
  if (port.io === 'In') {
    result.orientation = 'right'
    result.input = false
  } else {
    result.orientation = 'left'
    result.input = true
  }

  const type = port.lineType || 'Signal'
  const dim = port.dimension || 1

  result.type = type
  if (dim !== 1) {
    result.type = type + ',' + dim
  }

  return [result]
}

function getMacroBlockIOs(macro: simu.MacroBlock): modules.IO[] {
  const portBlocks = macro.diagram.blocks.filter((bl) => bl.type === 'Port') as simu.PortBlock[]
  const inPorts = portBlocks.filter((p) => p.io === 'In').sort((a, b) => a.index - b.index)
  const outPorts = portBlocks.filter((p) => p.io === 'Out').sort((a, b) => a.index - b.index)

  const resIn: modules.IO[] = inPorts.map((p) => {
    return {
      id: p.name,
      orientation: 'left',
      input: true,
      relPos: -1000,
      type: makePortType(p),
    }
  })

  const resOut: modules.IO[] = outPorts.map((p) => {
    return {
      id: p.name,
      orientation: 'right',
      input: false,
      relPos: -1000,
      type: makePortType(p),
    }
  })

  return resIn.concat(resOut)
}

function makePortType(port: simu.PortBlock): string {
  const t = port.lineType || 'Signal'
  const n = port.dimension || 1
  if (n === 1) {
    return t
  }
  return t + ',' + n
}

function makePortFromIO(b: simu.Block, io: modules.IO, allIOs: modules.IO[]): Port {
  const orientation = io.orientation
  const auto = io.relPos <= -1000
  const portsOfSameOrientation = allIOs.filter((currIO) => currIO.orientation === orientation)
  const portCount = portsOfSameOrientation.length
  const idx = 1 + portsOfSameOrientation.findIndex((x) => x === io)
  const rotatedOrientation = getRotatedOrientation(orientation, b.rotation)

  const calcX = () => {
    switch (rotatedOrientation) {
      case 'left':
        return b.x
      case 'right':
        return b.x + b.w
      case 'top':
      case 'bottom':
        return auto ? b.x + offsetPort(b.w, idx, portCount) : toGrid(b.x + io.relPos * b.w)
    }
  }

  const calcY = () => {
    switch (rotatedOrientation) {
      case 'left':
      case 'right':
        return auto ? b.y + offsetPort(b.h, idx, portCount) : toGrid(b.y + io.relPos * b.h)
      case 'top':
        return b.y
      case 'bottom':
        return b.y + b.h
    }
  }

  return {
    id: io.id,
    orientation: rotatedOrientation,
    input: io.input,
    type: io.type,
    x: calcX(),
    y: calcY(),
    state: 'open',
    layerWater: isLayerWater(io.type),
    layerSignal: isLayerSignal(io.type),
    layerAir: isLayerAir(io.type),
  }
}

function getRotatedOrientation(ori: Orientation, rot?: Rotation): Orientation {
  const rotation = rot === undefined ? '0' : rot
  switch (rotation) {
    case '0':
      return ori
    case '90':
      switch (ori) {
        case 'left':
          return 'top'
        case 'right':
          return 'bottom'
        case 'top':
          return 'right'
        case 'bottom':
          return 'left'
      }
    case '180':
      switch (ori) {
        case 'left':
          return 'right'
        case 'right':
          return 'left'
        case 'top':
          return 'bottom'
        case 'bottom':
          return 'top'
      }
    case '270':
      switch (ori) {
        case 'left':
          return 'bottom'
        case 'right':
          return 'top'
        case 'top':
          return 'left'
        case 'bottom':
          return 'right'
      }
  }
  return ori
}

function offsetPort(ww: number, idx: number, portCount: number): number {
  const dd = Math.max(toGrid(ww / portCount), 2 * grid)
  const d0 = toGrid((ww - dd * (portCount - 1)) / 2)
  return d0 + (idx - 1) * dd
}

export const grid = 5

export function toGrid(x: number): number {
  return grid * Math.round(x / grid)
}

function makeFont(f?: simu.Font): string {
  if (f === undefined) {
    return '10px Arial'
  }
  return mapFontStyle(f.style) + ' ' + mapFontWeight(f.weight) + ' ' + f.size + 'px ' + f.family
}

function mapFontStyle(w: string): string {
  if (w === 'Normal') {
    return 'normal'
  }
  if (w === 'Italic') {
    return 'italic'
  }
  if (w === 'Oblique') {
    return 'oblique'
  }
  return w
}

function mapFontWeight(w: string): string {
  if (w === 'Normal') {
    return 'normal'
  }
  if (w === 'Bold') {
    return 'bold'
  }
  if (w === 'Thin') {
    return 'lighter'
  }
  return w
}

export function convertLine(line: simu.Line, lineIdx: number, blockMap: Map<string, Block>): Line {
  const resolvePortPoint = (blockID: string, portIdx: number, isInput: boolean) => {
    const block = blockMap.get(blockID)
    const hasPort = block !== undefined && portIdx < block.ports.length && portIdx >= 0
    const port = hasPort ? block!.ports[portIdx] : undefined
    const valid = port !== undefined && port.type === line.type && port.input === isInput
    if (port !== undefined) {
      port.state = valid ? 'connected' : 'open'
      port.lineIdx = valid ? lineIdx : undefined
    }
    return valid ? { x: port!.x, y: port!.y } : undefined
  }

  const ptStart = resolvePortPoint(line.source, line.sourceIdx, false)
  const ptEnd = resolvePortPoint(line.dest, line.destIdx, true)

  const copyPoint = (p: Point) => {
    return { x: p.x, y: p.y }
  }

  const copyPointSelected = (p: Point) => {
    return { x: p.x, y: p.y, selected: false }
  }

  const water = isLayerWater(line.type)
  const signal = isLayerSignal(line.type)
  const air = isLayerAir(line.type)

  return {
    dest: line.dest,
    destIdx: line.destIdx,
    source: line.source,
    sourceIdx: line.sourceIdx,
    type: line.type,
    style: lineStyleFromLayer(water, signal, air),
    points: line.points.map(copyPointSelected),
    selected: false,
    start: ptStart !== undefined ? copyPoint(ptStart) : undefined,
    end: ptEnd !== undefined ? copyPoint(ptEnd) : undefined,
    layerWater: water,
    layerSignal: signal,
    layerAir: air,
  }
}

function isLayerWater(lineType: string): boolean {
  return lineType.startsWith('Water')
}

function isLayerSignal(lineType: string): boolean {
  return lineType.startsWith('Signal')
}

function isLayerAir(lineType: string): boolean {
  return lineType.startsWith('Air')
}

export function lineStyleFromLayer(water: boolean, signal: boolean, air: boolean): LineStyle {
  if (signal) {
    return LineStyleSignal
  }
  if (water) {
    return LineStyleWater
  }
  if (air) {
    return LineStyleAir
  }
  return LineStyleSignal
}
