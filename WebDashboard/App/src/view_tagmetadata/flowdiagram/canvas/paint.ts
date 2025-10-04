import * as draw from './draw_model'
import * as modules from '../module_types'

export class Painter implements modules.Interactive {
  interactiveElements: modules.InteractiveElement[] = []

  private scale: number
  private width: number
  private height: number
  private model: draw.Diagram
  private drawingContext: CanvasRenderingContext2D

  constructor(w: number, h: number, scale: number, model: draw.Diagram, drawingContext: CanvasRenderingContext2D) {
    this.width = w
    this.height = h
    this.scale = scale
    this.model = model
    this.drawingContext = drawingContext
  }

  addInteractiveElement(elem: modules.InteractiveElement): void {
    this.interactiveElements.push(elem)
  }

  paint(layerWater: boolean, layerSignal: boolean, layerAir: boolean): boolean {
    const ctx = this.drawingContext
    this.interactiveElements = []

    ctx.clearRect(0, 0, this.width, this.height)

    ctx.save()
    ctx.scale(this.scale, this.scale)

    let needRedraw = false

    for (const block of this.model.blocks) {
      const ports = block.ports
      const show = ports.length === 0 || ports.some((p) => (layerWater && p.layerWater) || (layerSignal && p.layerSignal) || (layerAir && p.layerAir))
      if (!show) {
        continue
      }

      const hasFrame = block.drawFrame && block.frame !== undefined
      if (hasFrame) {
        this.paintBlockFrame(block.frame!, block)
      }
      if (block.image !== undefined) {
        if (block.image.complete) {
          this.drawIcon(ctx, block)
        } else {
          needRedraw = true
        }
      }
      if (block.drawName) {
        this.drawName(ctx, block)
      }
      if (block.selected) {
        ctx.fillStyle = 'black'
        ctx.fillRect(block.x + block.w - 3, block.y + block.h - 3, 6, 6)
      }
      ctx.fillStyle = 'black'
      ctx.strokeStyle = 'black'
      ctx.lineWidth = 1.0
      ctx.font = '10px Arial'
      const drawLabel = block.drawPortLabel
      for (const port of block.ports) {
        this.paintPort(port, drawLabel)
      }
      if (block.type === 'Module') {
        const mBlock = block as draw.ModuleBlock
        if (mBlock.customDraw !== undefined) {
          ctx.save()
          mBlock.customDraw({ block: mBlock, dc: ctx, ia: this })
          ctx.restore()
        }
      } else if (block.type === 'Port') {
        const portBlock = block as draw.PortBlock
        ctx.save()
        this.drawPortBlock(portBlock, ctx)
        ctx.restore()
      }
    }

    ctx.save()
    for (const line of this.model.lines) {
      if ((layerWater && line.layerWater) || (layerSignal && line.layerSignal) || (layerAir && line.layerAir)) {
        this.paintLine(line)
      }
    }
    ctx.restore()

    const selectRect = this.model.selectRect
    if (selectRect !== undefined) {
      ctx.save()
      ctx.strokeStyle = 'black'
      ctx.lineWidth = 1
      ctx.setLineDash([5, 5])
      ctx.strokeRect(selectRect.x, selectRect.y, selectRect.w, selectRect.h)
      ctx.restore()
    }
    ctx.restore()
    return needRedraw
  }

  paintBlockFrame(frame: draw.Frame, block: draw.Block) {
    const ctx = this.drawingContext
    const x = block.x
    const y = block.y
    const w = block.w
    const h = block.h
    const ShadowColor = '#C8C8C8'
    const ShadowOff = 5

    ctx.lineWidth = frame.strokeWidth

    switch (frame.shape) {
      case 'Rectangle': {
        if (frame.shadow) {
          ctx.fillStyle = ShadowColor
          ctx.fillRect(x + ShadowOff, y + ShadowOff, w, h)
        }
        ctx.fillStyle = frame.fillColor
        ctx.strokeStyle = frame.strokeColor
        ctx.fillRect(x, y, w, h)
        ctx.strokeRect(x, y, w, h)
        break
      }
      case 'RoundedRectangle': {
        const r = (frame.var1 === undefined ? 0 : frame.var1) * block.h
        if (frame.shadow) {
          ctx.fillStyle = ShadowColor
          ctx.strokeStyle = ShadowColor
          ctx.beginPath()
          this.roundRect(x + ShadowOff, y + ShadowOff, w, h, r)
          ctx.fill()
          ctx.stroke()
        }
        ctx.fillStyle = frame.fillColor
        ctx.strokeStyle = frame.strokeColor
        this.roundRect(x, y, w, h, r)
        ctx.fill()
        ctx.stroke()
        break
      }
      case 'Circle': {
        if (frame.shadow) {
          ctx.fillStyle = ShadowColor
          ctx.strokeStyle = ShadowColor
          ctx.beginPath()
          ctx.ellipse(x + w / 2 + ShadowOff, y + h / 2 + ShadowOff, w / 2, h / 2, 0, 0, 2 * Math.PI, false)
          ctx.fill()
          ctx.stroke()
        }
        ctx.fillStyle = frame.fillColor
        ctx.strokeStyle = frame.strokeColor
        ctx.beginPath()
        ctx.ellipse(x + w / 2, y + h / 2, w / 2, h / 2, 0, 0, 2 * Math.PI, false)
        ctx.fill()
        ctx.stroke()
        break
      }
    }
  }

  drawPortBlock(block: draw.PortBlock, ctx: CanvasRenderingContext2D): void {
    const idx = block.index
    if (idx !== undefined) {
      ctx.fillStyle = 'black'
      ctx.textAlign = 'center'
      ctx.textBaseline = 'middle'
      ctx.font = block.fontStr
      ctx.fillText(idx.toString(), block.x + 0.5 * block.w, block.y + 0.5 * block.h)
    }
  }

  paintPort(p: draw.Port, label: boolean) {
    const ctx = this.drawingContext

    if (label) {
      ctx.strokeStyle = 'black'
      this.drawPortLabel(p, ctx)
    }

    if (p.layerSignal) {
      ctx.strokeStyle = 'grey'
    } else if (p.layerWater) {
      ctx.strokeStyle = 'blue'
    } else {
      ctx.strokeStyle = 'black'
    }

    const h = 7
    const w = 3
    if (p.input) {
      this.paintInputPort(p, label, h, w)
    } else {
      this.paintOutputPort(p, label, h, w)
    }
  }

  paintInputPort(p: draw.Port, label: boolean, h: number, w: number) {
    const ctx = this.drawingContext
    ctx.beginPath()
    switch (p.orientation) {
      case 'left':
        ctx.moveTo(p.x - h, p.y - w)
        ctx.lineTo(p.x, p.y)
        ctx.lineTo(p.x - h, p.y + w)
        break
      case 'right':
        ctx.moveTo(p.x + h, p.y - w)
        ctx.lineTo(p.x, p.y)
        ctx.lineTo(p.x + h, p.y + w)
        break
      case 'top':
        ctx.moveTo(p.x - w, p.y - h)
        ctx.lineTo(p.x, p.y)
        ctx.lineTo(p.x + w, p.y - h)
        break
      case 'bottom':
        ctx.moveTo(p.x - w, p.y + h)
        ctx.lineTo(p.x, p.y)
        ctx.lineTo(p.x + w, p.y + h)
        break
    }
    if (p.state === 'open') {
      ctx.stroke()
    } else {
      ctx.fill()
    }
  }

  paintOutputPort(p: draw.Port, label: boolean, h: number, w: number) {
    const ctx = this.drawingContext
    if (p.state === 'connected') {
      return
    }
    ctx.beginPath()
    switch (p.orientation) {
      case 'left':
        ctx.moveTo(p.x, p.y - w)
        ctx.lineTo(p.x - h, p.y)
        ctx.lineTo(p.x, p.y + w)
        break
      case 'right':
        ctx.moveTo(p.x, p.y - w)
        ctx.lineTo(p.x + h, p.y)
        ctx.lineTo(p.x, p.y + w)
        break
      case 'top':
        ctx.moveTo(p.x - w, p.y)
        ctx.lineTo(p.x, p.y - h)
        ctx.lineTo(p.x + w, p.y)
        break
      case 'bottom':
        ctx.moveTo(p.x - w, p.y)
        ctx.lineTo(p.x, p.y + h)
        ctx.lineTo(p.x + w, p.y)
        break
    }
    ctx.stroke()
  }

  drawPortLabel(p: draw.Port, ctx: CanvasRenderingContext2D): void {
    switch (p.orientation) {
      case 'left':
        ctx.textAlign = 'left'
        ctx.textBaseline = 'middle'
        ctx.fillText(p.id, p.x + 3, p.y)
        break
      case 'right':
        ctx.textAlign = 'right'
        ctx.textBaseline = 'middle'
        ctx.fillText(p.id, p.x - 3, p.y)
        break
      case 'top':
        ctx.textAlign = 'center'
        ctx.textBaseline = 'top'
        ctx.fillText(p.id, p.x, p.y + 3)
        break
      case 'bottom':
        ctx.textAlign = 'center'
        ctx.textBaseline = 'bottom'
        ctx.fillText(p.id, p.x, p.y - 3)
        break
    }
  }

  paintLine(line: draw.Line): void {
    const ctx = this.drawingContext
    const s = line.start
    const e = line.end

    if (s === undefined && e === undefined) {
      return
    }

    const drawing = s === undefined || e === undefined

    const style = line.style

    if (drawing) {
      ctx.strokeStyle = line.selected ? 'red' : style.color
      ctx.setLineDash([4, 4])
    } else {
      ctx.strokeStyle = line.selected ? 'red' : style.color
      ctx.setLineDash([])
    }

    ctx.lineWidth = style.width
    ctx.beginPath()

    if (s !== undefined) {
      ctx.moveTo(s.x, s.y)
      for (const pt of line.points) {
        ctx.lineTo(pt.x, pt.y)
      }
      if (line.tmpDraw !== undefined) {
        ctx.lineTo(line.tmpDraw.x, line.tmpDraw.y)
      }
      if (e !== undefined) {
        ctx.lineTo(e.x, e.y)
      }
    } else {
      ctx.moveTo(e!.x, e!.y)
      for (let i = line.points.length - 1; i >= 0; i--) {
        const pt = line.points[i]
        ctx.lineTo(pt.x, pt.y)
      }
      if (line.tmpDraw !== undefined) {
        ctx.lineTo(line.tmpDraw.x, line.tmpDraw.y)
      }
    }

    ctx.fillStyle = 'black'
    const dx = 3
    for (const pt of line.points) {
      if (pt.selected) {
        ctx.fillRect(pt.x - dx, pt.y - dx, 2 * dx, 2 * dx)
      }
    }

    ctx.stroke()
  }

  drawIcon(ctx: CanvasRenderingContext2D, block: draw.Block): void {
    const image = block.image!
    const iconX = block.icon!.x
    const iconY = block.icon!.y
    const iconW = block.icon!.w
    const iconH = block.icon!.h

    const noIconRotate = !(block.icon!.rotate || false)

    if (block.rotation === '0' || noIconRotate) {
      try {
        ctx.drawImage(image, block.x + iconX * block.w, block.y + iconY * block.h, iconW * block.w, iconH * block.h)
      } catch {}
    } else {
      const toRadian = Math.PI / 180.0
      ctx.save()
      try {
        switch (block.rotation) {
          case '90': {
            ctx.translate(block.x, block.y)
            ctx.rotate(90 * toRadian)
            const offX = (1 - iconW) * block.h
            const x = 0 - iconX * block.h + offX
            const y = -block.w + iconY * block.w
            const w = iconW * block.h
            const h = iconH * block.w
            ctx.drawImage(image, x, y, w, h)
            break
          }
          case '180': {
            ctx.translate(block.x, block.y)
            ctx.rotate(180 * toRadian)
            const offX = (1 - iconW) * block.w
            const offY = (1 - iconH) * block.h
            const x = -block.w - iconX * block.w + offX
            const y = -block.h - iconY * block.h + offY
            const w = iconW * block.w
            const h = iconH * block.h
            ctx.drawImage(image, x, y, w, h)
            break
          }
          case '270': {
            ctx.translate(block.x, block.y)
            ctx.rotate(270 * toRadian)
            const offX = (1 - iconW) * block.h
            const x = -block.h - iconX * block.h + offX
            const y = 0 + iconY * block.w
            const w = iconW * block.h
            const h = iconH * block.w
            ctx.drawImage(image, x, y, w, h)
            break
          }
        }
      } catch {}
      ctx.restore()
    }
  }

  drawName(ctx: CanvasRenderingContext2D, block: draw.Block): void {
    ctx.fillStyle = 'black'
    ctx.font = block.fontStr

    const centerX = block.x + 0.5 * block.w
    const centerY = block.y + 0.5 * block.h

    const ori = this.getNameOrientation(block)
    switch (ori) {
      case 'top':
        ctx.textAlign = 'center'
        ctx.textBaseline = 'bottom'
        ctx.fillText(block.name, centerX, block.y - 3)
        break
      case 'bottom':
        ctx.textAlign = 'center'
        ctx.textBaseline = 'top'
        ctx.fillText(block.name, centerX, block.y + block.h + 3)
        break
      case 'left':
        ctx.textAlign = 'right'
        ctx.textBaseline = 'middle'
        ctx.fillText(block.name, block.x - 3, centerY)
        break
      case 'right':
        ctx.textAlign = 'left'
        ctx.textBaseline = 'middle'
        ctx.fillText(block.name, block.x + block.w + 3, centerY)
        break
    }
  }

  getNameOrientation(block: draw.Block): draw.Orientation {
    switch (block.rotation) {
      case '0':
        return block.flipName ? 'top' : 'bottom'
      case '90':
        return block.flipName ? 'right' : 'left'
      case '180':
        return block.flipName ? 'bottom' : 'top'
      case '270':
        return block.flipName ? 'left' : 'right'
    }
    return 'bottom'
  }

  roundRect(x: number, y: number, w: number, h: number, r: number) {
    const ctx = this.drawingContext
    if (w < 2 * r) {
      r = w / 2
    }
    if (h < 2 * r) {
      r = h / 2
    }
    ctx.beginPath()
    ctx.moveTo(x + r, y)
    ctx.arcTo(x + w, y, x + w, y + h, r)
    ctx.arcTo(x + w, y + h, x, y + h, r)
    ctx.arcTo(x, y + h, x, y, r)
    ctx.arcTo(x, y, x + w, y, r)
    ctx.closePath()
  }
}
