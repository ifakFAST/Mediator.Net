import * as simu from './model_flow'
import * as tags from '../model_tags'

export abstract class Command {
  diagramPath: string[] = []
  abstract apply(model: simu.FlowDiagram): void
}

export class Move extends Command {
  private blocks: string[] = []
  private dx = 0
  private dy = 0
  private changedLinePoints: LinePoints[] = []

  constructor(blocks: string[], dx: number, dy: number, changedLinePoints: LinePoints[]) {
    super()
    this.blocks = blocks
    this.dx = dx
    this.dy = dy
    this.changedLinePoints = copyLinePoints(changedLinePoints)
  }

  apply(model: simu.FlowDiagram): void {
    const blocks = model.blocks
    for (const block of this.blocks) {
      const bb = blocks.find((b) => b.name === block)
      if (bb !== undefined) {
        bb.x += this.dx
        bb.y += this.dy
      }
    }
    for (const line of this.changedLinePoints) {
      model.lines[line.lineIdx].points = copyPoints(line.points)
    }
  }
}

export class ResizeBlock extends Command {
  private block = ''
  private w = 0
  private h = 0
  private changedLinePoints: LinePoints[] = []

  constructor(block: string, w: number, h: number, changedLinePoints: LinePoints[]) {
    super()
    this.block = block
    this.w = w
    this.h = h
    this.changedLinePoints = copyLinePoints(changedLinePoints)
  }

  apply(model: simu.FlowDiagram): void {
    const bb = model.blocks.find((b) => b.name === this.block)
    if (bb !== undefined) {
      bb.w = this.w
      bb.h = this.h
    }
    for (const line of this.changedLinePoints) {
      model.lines[line.lineIdx].points = copyPoints(line.points)
    }
  }
}

export class AddBlocksAndLines extends Command {
  private blocks = ''
  private lines = ''

  constructor(blocks: simu.Block[], lines: simu.Line[]) {
    super()
    this.blocks = JSON.stringify(blocks)
    this.lines = JSON.stringify(lines)
  }

  apply(model: simu.FlowDiagram): void {
    const blocks = JSON.parse(this.blocks)
    model.blocks.push(...blocks)
    const lines = JSON.parse(this.lines)
    model.lines.push(...lines)
  }
}

export class LineConnectBlocks extends Command {
  private src = ''
  private srcIdx = 0
  private dst = ''
  private dstIdx = 0
  private type = ''
  // private width = 0
  private points = ''

  constructor(src: string, srcIdx: number, dst: string, dstIdx: number, type: string /*, width: number*/, points: simu.Point[]) {
    super()
    this.src = src
    this.srcIdx = srcIdx
    this.dst = dst
    this.dstIdx = dstIdx
    this.type = type
    // this.width = width
    this.points = JSON.stringify(points)
  }

  apply(model: simu.FlowDiagram): void {
    model.lines.push({
      dest: this.dst,
      destIdx: this.dstIdx,
      source: this.src,
      sourceIdx: this.srcIdx,
      points: JSON.parse(this.points),
      type: this.type,
      // width: this.width,
    })
  }
}

export class DeleteBlocksAndLines extends Command {
  private blocksIdx: number[] = []
  private linesIdx: number[] = []

  constructor(blocksIdx: number[], linesIdx: number[]) {
    super()
    this.blocksIdx = blocksIdx.map((x) => x).sort((x, y) => y - x) // copy and sort desc
    this.linesIdx = linesIdx.map((x) => x).sort((x, y) => y - x) // copy and sort desc
  }

  apply(model: simu.FlowDiagram): void {
    const setOfDeletedBlockNames = new Set<string>()
    for (const i of this.blocksIdx) {
      setOfDeletedBlockNames.add(model.blocks[i].name)
      model.blocks.splice(i, 1)
    }
    for (const i of this.linesIdx) {
      model.lines.splice(i, 1)
    }

    for (const line of model.lines) {
      if (setOfDeletedBlockNames.has(line.dest)) {
        line.dest = ''
        line.destIdx = -1
      }
      if (setOfDeletedBlockNames.has(line.source)) {
        line.source = ''
        line.sourceIdx = -1
      }
    }
  }
}

export class BlockParamsChanged extends Command {
  private block = ''
  private parameters = ''

  constructor(block: string, newParams: simu.Parameters) {
    super()
    this.block = block
    this.parameters = JSON.stringify(newParams)
  }

  apply(model: simu.FlowDiagram): void {
    const b = model.blocks.find((bl) => bl.name === this.block)
    if (b !== undefined) {
      if (b.type === 'Module') {
        ;(b as simu.ModuleBlock).parameters = JSON.parse(this.parameters)
      } else if (b.type === 'Port') {
        const port = b as simu.PortBlock
        const parameters = JSON.parse(this.parameters)
        port.index = parseInt(parameters['index'] || port.index, 10)
        port.io = parameters['io'] || port.io
        port.lineType = parameters['lineType'] || port.lineType
        port.dimension = parseInt(parameters['dimension'] || port.dimension, 10)
      }
    }
  }
}

export class BlockPropertiesChanged extends Command {
  private block = ''

  constructor(block: simu.Block) {
    super()
    this.block = JSON.stringify(block)
  }

  apply(model: simu.FlowDiagram): void {
    const block: simu.Block = JSON.parse(this.block)
    const b = model.blocks.find((bl) => bl.name === block.name)
    if (b !== undefined) {
      b.drawFrame = block.drawFrame
      b.drawName = block.drawName
      b.drawPortLabel = block.drawPortLabel
      b.flipName = block.flipName
      b.colorForeground = block.colorForeground
      b.colorBackground = block.colorBackground
      b.frame = block.frame
      b.icon = block.icon
      b.font = block.font
    }
  }
}

export class BlockRename extends Command {
  private oldName = ''
  private newName = ''

  constructor(oldName: string, newName: string) {
    super()
    this.oldName = oldName
    this.newName = newName
  }

  apply(model: simu.FlowDiagram): void {
    const b = model.blocks.find((bl) => bl.name === this.oldName)
    if (b !== undefined) {
      b.name = this.newName
      for (const line of model.lines) {
        if (line.dest === this.oldName) {
          line.dest = this.newName
        }
        if (line.source === this.oldName) {
          line.source = this.newName
        }
      }
    }
  }
}

export class BlockRotate extends Command {
  private blockName = ''

  constructor(block: simu.Block) {
    super()
    this.blockName = block.name
  }

  apply(model: simu.FlowDiagram): void {
    const b = model.blocks.find((bl) => bl.name === this.blockName)
    if (b !== undefined) {
      b.rotation = this.newRotation(b)
    }
  }

  newRotation(b: simu.Block): simu.Rotation {
    const rotation: simu.Rotation = b.rotation !== undefined ? b.rotation : '0'
    switch (rotation) {
      case '0':
        return '90'
      case '90':
        return '180'
      case '180':
        return '270'
      case '270':
        return '0'
    }
  }
}

export interface LinePoints {
  lineIdx: number
  points: simu.Point[]
}

function copyPoints(pts: simu.Point[]): simu.Point[] {
  return JSON.parse(JSON.stringify(pts))
}

function copyLinePoints(pts: LinePoints[]): LinePoints[] {
  return JSON.parse(JSON.stringify(pts))
}

export class BlockAddTag extends Command {
  private block = ''
  private tag = ''

  constructor(blockName: string, tag: tags.Tag) {
    super()
    this.block = blockName
    this.tag = JSON.stringify(tag)
  }

  apply(model: simu.FlowDiagram): void {
    const b = model.blocks.find((bl) => bl.name === this.block)
    if (b !== undefined && b.type === 'Module') {
      const mb = b as simu.ModuleBlock
      const t: tags.Tag = JSON.parse(this.tag)
      if (!mb.tags) {
        mb.tags = []
      }
      mb.tags.push(t)
    }
  }
}

export class DiagramRemoveTags extends Command {
  private idsJson = ''

  constructor(tagIDs: string[]) {
    super()
    this.idsJson = JSON.stringify(tagIDs)
  }

  apply(model: simu.FlowDiagram): void {
    const ids: string[] = JSON.parse(this.idsJson)
    const idSet = new Set(ids)
    this.removeTagsRecursive(model, idSet)
  }

  private removeTagsRecursive(diagram: simu.FlowDiagram, idSet: Set<string>): void {
    for (const b of diagram.blocks) {
      if (b.type === 'Module') {
        const mb = b as simu.ModuleBlock
        if (mb.tags && mb.tags.length > 0) {
          mb.tags = mb.tags.filter((t) => !idSet.has(t.id))
        }
      } else if (b.type === 'Macro') {
        const mac = b as simu.MacroBlock
        this.removeTagsRecursive(mac.diagram, idSet)
      }
    }
  }
}

export class BlockUpdateTag extends Command {
  private block = ''
  private tagId = ''
  private tagJson = ''

  constructor(blockName: string, existingTagId: string, updatedTag: tags.Tag) {
    super()
    this.block = blockName
    this.tagId = existingTagId
    this.tagJson = JSON.stringify(updatedTag)
  }

  apply(model: simu.FlowDiagram): void {
    const b = model.blocks.find((bl) => bl.name === this.block)
    if (!b || b.type !== 'Module') return
    const mb = b as simu.ModuleBlock
    if (!mb.tags || mb.tags.length === 0) return
    const idx = mb.tags.findIndex((t) => t.id === this.tagId)
    if (idx < 0) return
    const newTag: tags.Tag = JSON.parse(this.tagJson)
    mb.tags[idx] = newTag
  }
}

// Update a tag located under a nested path relative to the provided diagram.
// The pathParts parameter should list names to traverse, where all but the last
// are Macro block names and the last is the target Module block name.
export class UpdateTagByRelativePath extends Command {
  private pathJson = ''
  private tagId = ''
  private tagJson = ''

  constructor(pathParts: string[], existingTagId: string, updatedTag: tags.Tag) {
    super()
    this.pathJson = JSON.stringify(pathParts)
    this.tagId = existingTagId
    this.tagJson = JSON.stringify(updatedTag)
  }

  apply(model: simu.FlowDiagram): void {
    const parts: string[] = JSON.parse(this.pathJson)
    if (!parts || parts.length === 0) return
    let diagram: simu.FlowDiagram | null = model
    // Traverse all but last as Macro names
    for (let i = 0; i < parts.length - 1; i++) {
      const name = parts[i]
      const mb = diagram.blocks.find((b) => b.type === 'Macro' && b.name === name) as simu.MacroBlock | undefined
      if (!mb) return
      diagram = mb.diagram
    }
    const modName = parts[parts.length - 1]
    const mod = diagram.blocks.find((b) => b.type === 'Module' && b.name === modName) as simu.ModuleBlock | undefined
    if (!mod || !mod.tags || mod.tags.length === 0) return
    const idx = mod.tags.findIndex((t) => t.id === this.tagId)
    if (idx < 0) return
    const newTag: tags.Tag = JSON.parse(this.tagJson)
    mod.tags[idx] = newTag
  }
}
