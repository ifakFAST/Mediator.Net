import { drawCenterTextAbove } from './util.js'

// Tank module type moved from static TS to dynamic JS
const TankBlock = {
  id: 'tank',
  parameters: [
    { id: 'length', name: 'Length (m)', type: 'Numeric', defaultValue: '0' },
    { id: 'width', name: 'Width (m)', type: 'Numeric', defaultValue: '0' },
    { id: 'passes', name: 'Number of passes', type: 'Numeric', defaultValue: '0' },
  ],
  defineIOs() {
    return [
      { id: 'x', input: true, type: 'Water', relPos: -1000, orientation: 'left' },
      { id: 'y', input: false, type: 'Water', relPos: -1000, orientation: 'right' },
    ]
  },
  customDraw(ctx) {
    const block = ctx.block
    const dc = ctx.dc

    const passes = JSON.parse(block.parameters['passes'] || '0')
    if (passes > 0) {
      dc.save()
      dc.translate(block.x, block.y)
      dc.strokeStyle = 'black'
      dc.lineWidth = 2
      const w = block.h / passes
      for (let i = 1; i < passes; ++i) {
        if (i % 2 === 1) {
          dc.moveTo(0, i * w)
          dc.lineTo(block.w * 0.9, i * w)
          dc.stroke()
        } else {
          dc.moveTo(0.1 * block.w, i * w)
          dc.lineTo(block.w, i * w)
          dc.stroke()
        }
      }
      dc.restore()
    }

    const W = JSON.parse(block.parameters['width'] || '0')
    const L = JSON.parse(block.parameters['length'] || '0')
    const tags = block.tags || []
    const ia = ctx.ia
    dc.fillStyle = 'blue'
    for (const tag of tags) {
      const distHead = tag.distHead
      const distSide = tag.distSide
      if (distHead && distSide) {
        const pX = Math.min(1, distHead / L)
        const x = block.x + pX * block.w
        const pY = Math.min(1, distSide / W)
        const y = block.y + pY * block.h

        const r = 4
        const rX = x - r
        const rY = y - r
        const rW = 2 * r
        const rH = 2 * r

        dc.beginPath()
        dc.ellipse(x, y, r, r, 0, 0, 2 * Math.PI, false)
        dc.fill()

        ia.addInteractiveElement({ block: block.name, x: rX, y: rY, w: rW, h: rH, type: 'tag', id: tag.id })
      }
    }

    // Optional: show dimensions above block if present
    const len = block.parameters['length']
    const wid = block.parameters['width']
    if (len !== undefined && wid !== undefined) {
      const txt = `${JSON.parse(len)} m × ${JSON.parse(wid)} m`
      drawCenterTextAbove(ctx, txt)
    }
  },
  supportedDropTypes: ['data-tag'],
  tagLocationEnumValues: ['front', 'middle', 'end'],
}

export default TankBlock

