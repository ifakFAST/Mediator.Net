import type { Tag } from '../model_tags'

export interface GlobalVarObj {
  dragType: string
}

export const GlobalObj: GlobalVarObj = {
  dragType: '',
}

export interface FlowModel {
  diagram: FlowDiagram
}

export function emptyFlowModel(): FlowModel {
  return {
    diagram: {
      blocks: [],
      lines: [],
    },
  }
}

export interface FlowDiagram {
  blocks: Block[]
  lines: Line[]
}

export interface Block {
  name: string
  type: BlockType
  x: number
  y: number
  w: number
  h: number
  drawFrame: boolean
  drawName: boolean
  drawPortLabel?: boolean
  flipName: boolean
  rotation?: Rotation
  colorForeground?: string // default is black
  colorBackground?: string // default is white
  frame?: Frame
  icon?: Icon
  font?: Font
}

export type Rotation = '0' | '90' | '180' | '270'

export type BlockType = 'Module' | 'Macro' | 'Port'

export interface ModuleBlock extends Block {
  type: 'Module'
  moduleType: string
  parameters: Parameters
  tags: Tag[] // list of assigned tags
}

export interface MacroBlock extends Block {
  type: 'Macro'
  diagram: FlowDiagram
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
  name: string
  x: number
  y: number
  w: number
  h: number
  // flip: boolean
  rotate: boolean
}

export interface Line {
  type: string // e.g. Signal
  source: string // id of source block
  sourceIdx: number
  dest: string // id of destination block
  destIdx: number
  // width: number
  points: Point[]
}

export interface Point {
  x: number
  y: number
}

export interface Font {
  family: string
  size: number // in pixels
  style: FontStyle
  weight: FontWeight
}

export type FontStyle = 'Normal' | 'Italic' | 'Oblique'

export type FontWeight = 'Normal' | 'Bold' | 'Thin'

// #################################

export interface BlockDropEvent {
  diagramPath?: string[]
  diagram: FlowDiagram
  blockName: string
  blockType: string
  type: string
  data: string
  x: number
  y: number
}

export interface InteractiveClickEvent {
  block: Block
  type: string
  id: string
  x: number
  y: number
}

export interface BlockParamsChanged {
  block: string
  parameters: Parameters
}

export interface BlockContextMenuEvent {
  x: number
  y: number
  block: Block
}

export interface BlockDoubleClickEvent {
  x: number
  y: number
  block: Block
}
