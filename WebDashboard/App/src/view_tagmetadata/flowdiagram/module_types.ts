import * as tags from '../model_tags'
import * as draw from './canvas/draw_model'

export interface ModuleBlockType {
  id: string
  parameters: ParameterDef[]
  defineIOs: (parameters: Parameters) => IO[]
  updateParams?: (parameters: Parameters) => DynamicParamProps[]
  customDraw?: (ctx: DrawingContext) => void
  supportedDropTypes?: string[]
  tagLocationEnumValues?: string[] // if defined, used for tag location selection
}

export interface DrawingContext {
  block: draw.ModuleBlock
  dc: CanvasRenderingContext2D
  ia: Interactive
}

export interface Interactive {
  addInteractiveElement(elem: InteractiveElement): void
}

export interface InteractiveElement {
  block: string
  type: string
  id: string
  x: number
  y: number
  w: number
  h: number
}

export interface Parameters {
  [key: string]: string
}

export interface IO {
  id: string
  orientation: Orientation
  input: boolean
  relPos: number // <= -1000: automatic, else 0..1
  type: string // e.g. Signal,3
}

export type Orientation = 'left' | 'right' | 'top' | 'bottom'

export interface ParameterDef {
  id: string
  name: string
  type: ParamType
  defaultValue: string
  enumValues?: string[] // only used when type == 'Enum'
  minValue?: number
  maxValue?: number
}

export type ParamType = 'Numeric' | 'String' | 'Bool' | 'Enum' | 'Custom' /* Custom is not handled by internal param dialog */

export interface DynamicParamProps {
  id: string
  minValue?: number
  maxValue?: number
  editable?: boolean
  visible?: boolean
}

function makeInput(id: string, orientation: Orientation, type: string, relPos: number = -1000): IO {
  return { id, input: true, type, relPos, orientation }
}

function makeOutput(id: string, orientation: Orientation, type: string, relPos: number = -1000): IO {
  return { id, input: false, type, relPos, orientation }
}

export function paramNumeric(id: string, name: string, defaultValue: number, minValue?: number, maxValue?: number): ParameterDef {
  return {
    id,
    name,
    type: 'Numeric',
    defaultValue: JSON.stringify(defaultValue),
    minValue,
    maxValue,
  }
}

export function paramString(id: string, name: string, defaultValue?: string): ParameterDef {
  return {
    id,
    name,
    type: 'String',
    defaultValue: defaultValue !== undefined ? defaultValue : '',
  }
}

export function paramBool(id: string, name: string, defaultValue: boolean): ParameterDef {
  return {
    id,
    name,
    type: 'Bool',
    defaultValue: JSON.stringify(defaultValue),
  }
}

export function paramEnum(id: string, name: string, defaultValue: string, enumValues: string[]): ParameterDef {
  return {
    id,
    name,
    type: 'Enum',
    defaultValue,
    enumValues,
  }
}

/////////////////////////////////////////////////////////////////////

interface DrawCenterOptions {
  font?: string
  color?: string
  offsetX?: number
  offsetY?: number
}

function drawCenterText(ctx: DrawingContext, text: string, options?: DrawCenterOptions): void {
  const dc = ctx.dc
  const b = ctx.block
  const opt = options !== undefined ? options : {}
  dc.fillStyle = opt.color !== undefined ? opt.color : b.colorForeground !== undefined ? b.colorForeground : 'black'
  dc.textAlign = 'center'
  dc.textBaseline = 'middle'
  dc.font = opt.font !== undefined ? opt.font : ctx.block.fontStr
  const offY = opt.offsetY !== undefined ? opt.offsetY : 0
  const offX = opt.offsetX !== undefined ? opt.offsetX : 0
  dc.fillText(text, b.x + 0.5 * b.w + offX * b.w, b.y + 0.5 * b.h + offY * b.h)
}

function drawCenterTextAbove(ctx: DrawingContext, text: string, options?: DrawCenterOptions): void {
  const dc = ctx.dc
  const b = ctx.block
  const opt = options !== undefined ? options : {}
  dc.fillStyle = opt.color !== undefined ? opt.color : b.colorForeground !== undefined ? b.colorForeground : 'black'
  dc.textAlign = 'center'
  dc.textBaseline = 'bottom'
  dc.font = opt.font !== undefined ? opt.font : ctx.block.fontStr
  const offY = opt.offsetY !== undefined ? opt.offsetY : 0
  const offX = opt.offsetX !== undefined ? opt.offsetX : 0
  dc.fillText(text, b.x + 0.5 * b.w + offX * b.w, b.y - 3 + offY * b.h)
}
/////////////////////////////////////////////////////////////////////

const InteractiveElementColor = 'blue'

function drawInteractiveElement(ctx: DrawingContext) {
  const tags: tags.Tag[] = ctx.block.tags || []
  if (tags.length > 0) {
    const block = ctx.block
    const dc = ctx.dc

    dc.fillStyle = InteractiveElementColor
    const r = 4
    const x = block.x + 0.5 * block.w
    const y = block.y + 0.5 * block.h
    dc.beginPath()
    dc.ellipse(x, y, r, r, 0, 0, 2 * Math.PI, false)
    dc.fill()

    ctx.ia.addInteractiveElement({ block: block.name, x: x - r, y: y - r, w: 2 * r, h: 2 * r, type: 'tag', id: tags[0].id })
  }
}

// const TankBlock: ModuleBlockType = {
//   id: 'tank',
//   parameters: [
//     paramNumeric('length', 'Length (m)', 0),
//     paramNumeric('width', 'Width (m)', 0),
//     paramNumeric('passes', 'Number of passes', 0),
//   ],
//   defineIOs(parameters: Parameters): IO[] {
//     return [makeInput('x', 'left', 'Water'), makeOutput('y', 'right', 'Water')]
//   },
//   customDraw(ctx: DrawingContext): void {
//     const block = ctx.block
//     const dc = ctx.dc

//     const passes: number = JSON.parse(ctx.block.parameters['passes'] || '0')
//     if (passes > 0) {
//       dc.save()
//       dc.translate(block.x, block.y)
//       dc.strokeStyle = 'black'
//       dc.lineWidth = 2
//       const w = block.h / passes
//       for (let i = 1; i < passes; ++i) {
//         if (i % 2 === 1) {
//           dc.moveTo(0, i * w)
//           dc.lineTo(block.w * 0.9, i * w)
//           dc.stroke()
//         } else {
//           dc.moveTo(0.1 * block.w, i * w)
//           dc.lineTo(block.w, i * w)
//           dc.stroke()
//         }
//       }
//       dc.restore()
//     }

//     const W: number = JSON.parse(ctx.block.parameters['width'] || '0')
//     const L: number = JSON.parse(ctx.block.parameters['length'] || '0')
//     const tags: tags.Tag[] = ctx.block.tags || []
//     const ia = ctx.ia
//     dc.fillStyle = InteractiveElementColor
//     for (const tag of tags) {
//       const distHead = tag.distHead
//       const distSide = tag.distSide
//       if (distHead && distSide) {
//         const pX = Math.min(1, distHead / L)
//         const x = block.x + pX * block.w
//         const pY = Math.min(1, distSide / W)
//         const y = block.y + pY * block.h

//         const r = 4
//         const rX = x - r
//         const rY = y - r
//         const rW = 2 * r
//         const rH = 2 * r

//         dc.beginPath()
//         dc.ellipse(x, y, r, r, 0, 0, 2 * Math.PI, false)
//         dc.fill()

//         ia.addInteractiveElement({ block: block.name, x: rX, y: rY, w: rW, h: rH, type: 'tag', id: tag.id })
//       }
//     }
//   },
//   supportedDropTypes: ['data-tag'],
// }

// const ConstBlock: ModuleBlockType = {
//   id: 'const',
//   parameters: [paramNumeric('Dimension', 'Dimension', 1, 1, 1000), paramString('Constant_0', 'Constant Value', '1')],
//   defineIOs(parameters: Parameters): IO[] {
//     const t = 'Signal'
//     const n = parameters['Dimension'] || '1'
//     const type = n === '1' ? t : t + ',' + n
//     return [makeOutput('y', 'right', type)]
//   },
//   customDraw(ctx: DrawingContext): void {
//     const str = ctx.block.parameters['Constant_0'] || ''
//     drawCenterText(ctx, str)
//   },
// }

// const InfluentBlock: ModuleBlockType = {
//   id: 'influent',
//   parameters: [],
//   defineIOs(parameters: Parameters): IO[] {
//     return [makeOutput('y', 'right', 'Water')]
//   },
// }

// const MixBlock: ModuleBlockType = {
//   id: 'mix',
//   parameters: [paramNumeric('nin', 'Number of inputs', 3, 1, 20)],
//   defineIOs(parameters: Parameters): IO[] {
//     const res = [makeOutput('y', 'right', 'Water')]
//     const inputs = JSON.parse(parameters['nin'] || '3')
//     for (let i = 0; i < inputs; ++i) {
//       res.push(makeInput('in_' + i, 'left', 'Water'))
//     }
//     return res
//   },
//   customDraw(ctx: DrawingContext): void {
//     const b = ctx.block
//     const dc = ctx.dc
//     const centerX = b.x + 0.5 * b.w
//     const centerY = b.y + 0.5 * b.h
//     dc.lineWidth = 3
//     dc.beginPath()
//     for (const p of b.ports) {
//       dc.moveTo(p.x, p.y)
//       dc.lineTo(centerX, centerY)
//     }
//     dc.stroke()
//   },
// }

// const DividerBlock: ModuleBlockType = {
//   id: 'divider',
//   parameters: [paramString('Dist', 'Flow distribution', '1; 1; 1')],
//   defineIOs(parameters: Parameters): IO[] {
//     const res = [makeInput('x', 'left', 'Water')]
//     const dist = parameters['Dist'] || '1; 1; 1'
//     const splits: string[] = dist.split(';')
//     for (let i = 0; i < splits.length; ++i) {
//       res.push(makeOutput('out_' + i, 'right', 'Water'))
//     }
//     return res
//   },
//   customDraw(ctx: DrawingContext): void {
//     const b = ctx.block
//     const dc = ctx.dc
//     const centerX = b.x + 0.5 * b.w
//     const centerY = b.y + 0.5 * b.h
//     dc.lineWidth = 3
//     dc.beginPath()
//     for (const p of b.ports) {
//       dc.moveTo(p.x, p.y)
//       dc.lineTo(centerX, centerY)
//     }
//     dc.stroke()
//   },
// }

// const PumpBlock: ModuleBlockType = {
//   id: 'pump',
//   parameters: [],
//   defineIOs(parameters: Parameters): IO[] {
//     return [
//       makeInput('yin', 'left', 'Water', 0.2),
//       makeInput('qpu', 'left', 'Signal', 0.66),
//       makeOutput('ymain', 'right', 'Water', 0.2),
//       makeOutput('ypump', 'bottom', 'Water'),
//     ]
//   },
// }

// const StorageBlock: ModuleBlockType = {
//   id: 'storage',
//   parameters: [paramNumeric('V', 'Maximum volume', 1000, 0.01, 1000000), paramNumeric('h', 'Depth of tank', 4, 0.01, 500)],
//   defineIOs(parameters: Parameters): IO[] {
//     return [
//       makeInput('u', 'left', 'Water', 0.0),
//       makeInput('qout', 'left', 'Signal'),
//       makeOutput('y', 'right', 'Water', 0.0),
//       makeOutput('V', 'top', 'Signal', 0.7),
//     ]
//   },
//   customDraw(ctx: DrawingContext): void {
//     const b = ctx.block
//     const dc = ctx.dc
//     dc.fillStyle = b.colorBackground !== undefined ? b.colorBackground : '#8595BD'
//     dc.fillRect(b.x, b.y + 0.5 * b.h, b.w, 0.5 * b.h)
//     dc.strokeStyle = 'black'
//     dc.lineWidth = 2
//     dc.beginPath()
//     dc.moveTo(b.x, b.y)
//     dc.lineTo(b.x, b.y + b.h)
//     dc.lineTo(b.x + b.w, b.y + b.h)
//     dc.lineTo(b.x + b.w, b.y)
//     dc.stroke()
//     const v = b.parameters['V']
//     if (v !== undefined) {
//       const txt = v + ' m³'
//       drawCenterText(ctx, txt, { offsetY: -0.2 })
//     }
//     drawInteractiveElement(ctx)
//   },
//   supportedDropTypes: ['data-tag'],
// }

// const SeparationBlock: ModuleBlockType = {
//   id: 'separation',
//   parameters: [],
//   defineIOs(parameters: Parameters): IO[] {
//     return [
//       makeInput('yin', 'left', 'Water', 0.02),
//       makeInput('qrs', 'left', 'Signal', 0.8),
//       makeOutput('yef', 'right', 'Water', 0.02),
//       makeOutput('yrs', 'bottom', 'Water'),
//     ]
//   },
//   customDraw(ctx: DrawingContext): void {
//     drawInteractiveElement(ctx)
//   },
//   supportedDropTypes: ['data-tag'],
// }

// const SinkBlock: ModuleBlockType = {
//   id: 'sink',
//   parameters: [],
//   defineIOs(parameters: Parameters): IO[] {
//     return [makeInput('u', 'left', 'Water')]
//   },
// }

// const SamplingPointBlock: ModuleBlockType = {
//   id: 'sampling_point',
//   parameters: [],
//   defineIOs(parameters: Parameters): IO[] {
//     return [makeInput('u', 'left', 'Water'), makeOutput('y', 'right', 'Water')]
//   },
//   customDraw(ctx: DrawingContext): void {
//     drawInteractiveElement(ctx)
//   },
//   supportedDropTypes: ['data-tag'],
// }

// const BlowerPDBlock: ModuleBlockType = {
//   id: 'blower_pd',
//   parameters: [
//     paramNumeric('q_max', 'Theoretical air flow rate at design point', 1000, 0.01, 1000000),
//     paramNumeric('eta_mech', 'Mechanical efficiency', 0.96, 0.01, 1),
//     paramNumeric('q_v_100', 'Gap losses at 100 mbar', 10, 0.01, 10000),
//     paramNumeric('eta_04', 'Electrical efficiency (eta) at 40% speed', 0.6, 0.01, 1),
//     paramNumeric('eta_08', 'Electrical efficiency (eta) at 80% speed', 0.96, 0.01, 1),
//     paramString('T1', 'Time delay', '2/(24*60)'),
//   ],
//   defineIOs(parameters: Parameters): IO[] {
//     return [makeInput('x', 'top', 'Signal'), makeOutput('i', 'left', 'Air,4'), makeOutput('o', 'right', 'Air,4')]
//   },
// }

// const BlowerCentrifugalBlock: ModuleBlockType = {
//   id: 'blower_centrifugal_vx',
//   parameters: [
//     paramString('T1', 'Time constant of blower [d]', '2/(24*60)'),
//     paramString('dP_shut', 'Maximum pressure (at flow=0) [mbar]', '800'),
//     paramString('q_opt', 'Flow rate at design point [m³/d]', '1000'),
//     paramString('dP_opt', 'Pressure at design point [mbar]', '500'),
//     paramString('q_max', 'Maximum flow rate (at Pressure=0) [m³/d]', '1400'),
//     paramString('dP_surge', 'Surge line pressure (at flow=0) [mbar]', '200'),
//     paramString('dP_surge_opt', 'Surge line pressure (at design point) [mbar]', '1000'),
//   ],
//   defineIOs(parameters: Parameters): IO[] {
//     return [
//       makeInput('x', 'top', 'Signal'),
//       makeOutput('i', 'left', 'Air,4'),
//       makeOutput('o', 'right', 'Air,4'),
//       makeOutput('P', 'right', 'Signal'),
//       makeOutput('y', 'right', 'Signal,10'),
//     ]
//   },
// }

// const PipeBlock: ModuleBlockType = {
//   id: 'pipe_simple',
//   parameters: [
//     paramNumeric('zeta_e', 'K factor, Resistance coefficient for fittings', 0.1, 0.00001, 10),
//     paramNumeric('k', 'Absolute roughness (mm)', 0.02, 0.00001, 100),
//     paramNumeric('l', 'Pipe length (m)', 2, 0.00001, 100000),
//     paramNumeric('d', 'Pipe diameter (m)', 0.1, 0.00001, 100000),
//   ],
//   defineIOs(parameters: Parameters): IO[] {
//     return [makeOutput('i', 'left', 'Air,4'), makeOutput('o', 'right', 'Air,4')]
//   },
//   customDraw(ctx: DrawingContext): void {
//     const b = ctx.block
//     const dc = ctx.dc

//     dc.strokeStyle = 'black'
//     dc.lineWidth = 2

//     dc.beginPath()
//     dc.moveTo(b.x, b.y)
//     dc.lineTo(b.x + b.w, b.y + b.h)
//     dc.moveTo(b.x, b.y + b.h)
//     dc.lineTo(b.x + b.w, b.y)
//     dc.stroke()

//     const zeta = b.parameters['zeta_e']
//     const dia = b.parameters['d']
//     const len = b.parameters['l']
//     if (len !== undefined && dia !== undefined && zeta !== undefined) {
//       const dia2 = JSON.parse(dia) * 1000.0
//       const txt = dia2 + ', ' + len + ' m' + ', ' + zeta
//       drawCenterTextAbove(ctx, txt)
//     }
//   },
// }

// const ValveBlock: ModuleBlockType = {
//   id: 'valve',
//   parameters: [
//     paramEnum('KvCv', 'Resistance coefficient type', 'Kv', ['Kv', 'Cv']),
//     paramNumeric('kv100', 'Kv100 (Cv100) value, maximum flow at norm conditions, full open', 100, 0.00001, 10000000),
//     paramNumeric('kv50', 'Kv50 (Cv50) value, flow at norm conditions, 50% open', 30, 0.00001, 10000000),
//     paramNumeric('kv0', 'Kv0 (Cv0) value, minimum flow at norm conditions, closed', 3, 0.00001, 10000000),
//     paramEnum('valvetype', 'Valve type', 'linear', ['linear', 'equal_percentage', 'quadratic']),
//   ],
//   defineIOs(parameters: Parameters): IO[] {
//     return [makeOutput('i', 'left', 'Air,4'), makeOutput('o', 'right', 'Air,4'), makeInput('h', 'top', 'Signal'), makeOutput('y', 'bottom', 'Signal')]
//   },
// }

// const TankAirBlock: ModuleBlockType = {
//   id: 'tank_air',
//   parameters: [
//     paramNumeric('nin_left', 'Inputs left', 1, 0, 100),
//     paramNumeric('nin_right', 'Inputs right', 1, 0, 100),
//     paramNumeric('nin_top', 'Inputs top', 0, 0, 100),
//     paramNumeric('nin_bottom', 'Inputs bottom', 0, 0, 100),
//   ],
//   defineIOs(parameters: Parameters): IO[] {
//     const left: number = JSON.parse(parameters['nin_left'] || '1')
//     const right: number = JSON.parse(parameters['nin_right'] || '1')
//     const top: number = JSON.parse(parameters['nin_top'] || '0')
//     const bottom: number = JSON.parse(parameters['nin_bottom'] || '0')
//     const res: IO[] = []
//     for (let i = 0; i < left; ++i) {
//       res.push(makeInput('i' + i, 'left', 'Air,4'))
//     }
//     for (let i = 0; i < right; ++i) {
//       res.push(makeInput('i' + i, 'right', 'Air,4'))
//     }
//     for (let i = 0; i < top; ++i) {
//       res.push(makeInput('i' + i, 'top', 'Air,4'))
//     }
//     for (let i = 0; i < bottom; ++i) {
//       res.push(makeInput('i' + i, 'bottom', 'Air,4'))
//     }
//     return res
//   },
//   customDraw(ctx: DrawingContext): void {
//     drawInteractiveElement(ctx)
//   },
//   supportedDropTypes: ['data-tag'],
// }

// const PressureSourceBlock: ModuleBlockType = {
//   id: 'pressure_source',
//   parameters: [paramNumeric('P', 'Atmospheric pressure (mbar)', 1013.25, 0, 100000)],
//   defineIOs(parameters: Parameters): IO[] {
//     return [makeInput('o', 'right', 'Air,4')]
//   },
//   customDraw(ctx: DrawingContext): void {
//     const b = ctx.block
//     const P = b.parameters['P']
//     if (P !== undefined) {
//       const p = Math.round(JSON.parse(P))
//       const txt = p + ' mbar'
//       drawCenterTextAbove(ctx, txt)
//     }
//   },
// }

// const TagTrashBlock: ModuleBlockType = {
//   id: 'tag_trash',
//   parameters: [],
//   defineIOs(parameters: Parameters): IO[] {
//     return []
//   },
//   customDraw(ctx: DrawingContext): void {
//     drawInteractiveElement(ctx)
//   },
//   supportedDropTypes: ['data-tag'],
// }

/////////////////////////////////////////////////////////////////////

export interface ModuleBlockTypes {
  [key: string]: ModuleBlockType
}

export const ListOfModuleTypes = [
  // ConstBlock,
  // InfluentBlock,
  // DividerBlock,
  // MixBlock,
  // PumpBlock,
  // TankBlock,
  // StorageBlock,
  // SeparationBlock,
  // SinkBlock,
  // SamplingPointBlock,
  // BlowerPDBlock,
  // BlowerCentrifugalBlock,
  // PipeBlock,
  // ValveBlock,
  // TankAirBlock,
  // PressureSourceBlock,
  // TagTrashBlock,
]

export let MapOfModuleTypes: ModuleBlockTypes = createMapFromList(ListOfModuleTypes)

function createMapFromList(blocks: ModuleBlockType[]): ModuleBlockTypes {
  const map: ModuleBlockTypes = {}
  for (const block of blocks) {
    map[block.id] = block
  }
  return map
}

interface DynamicModuleType {
  default: ModuleBlockType
}

function validateModuleType(moduleType: any): moduleType is ModuleBlockType {
  return (
    typeof moduleType === 'object' &&
    typeof moduleType.id === 'string' &&
    Array.isArray(moduleType.parameters) &&
    typeof moduleType.defineIOs === 'function'
  )
}

export async function loadModuleTypes(): Promise<void> {
  try {
    // Get module type IDs from backend
    const response = await (window.parent as any).dashboardApp.sendViewRequestAsync('GetModuleTypes', {})
    const moduleTypeIds: string[] = response || []

    // Start with existing static module types
    const dynamicModuleTypes: ModuleBlockTypes = { ...MapOfModuleTypes }
    const dashboardApp = (window.parent as any).dashboardApp

    // Load each dynamic module type
    for (const moduleTypeId of moduleTypeIds) {
      try {
        // Construct the URL for the JS module file
        const moduleUrl =
          dashboardApp.getBackendUrl() +
          '/view_tagmetadata/moduletype/module.' +
          moduleTypeId +
          '.js?' +
          dashboardApp.getDashboardViewContext()

        // Import the ES module
        const moduleExport: DynamicModuleType = await import(/* @vite-ignore */ moduleUrl)

        // Validate the module structure
        if (moduleExport && moduleExport.default && validateModuleType(moduleExport.default)) {
          const moduleType = moduleExport.default

          // Check if ID matches filename
          if (moduleType.id !== moduleTypeId) {
            console.warn(`Module type ID mismatch: expected '${moduleTypeId}', got '${moduleType.id}'`)
            continue
          }

          // Add to global module type map
          dynamicModuleTypes[moduleType.id] = moduleType
        } else {
          console.error(`Invalid module type structure in '${moduleTypeId}.js'`)
        }
      } catch (error) {
        console.error(`Failed to load module type '${moduleTypeId}':`, error)
      }
    }

    // Update the global module types map
    MapOfModuleTypes = dynamicModuleTypes
  } catch (error) {
    console.error('Failed to load dynamic module types:', error)
    // Keep existing static types on failure
  }
}
