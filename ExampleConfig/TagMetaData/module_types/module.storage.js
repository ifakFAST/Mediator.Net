import { paramNumeric, makeInput, makeOutput, drawCenterText, drawInteractiveElement } from './util.js'

const StorageBlock = {
  id: 'storage',
  parameters: [
    paramNumeric('V', 'Maximum volume', 1000, 0.01, 1000000),
    paramNumeric('h', 'Depth of tank', 4, 0.01, 500),
  ],
  defineIOs() {
    return [
      makeInput('u', 'left', 'Water', 0.0),
      makeInput('qout', 'left', 'Signal'),
      makeOutput('y', 'right', 'Water', 0.0),
      makeOutput('V', 'top', 'Signal', 0.7),
    ]
  },
  customDraw(ctx) {
    const b = ctx.block
    const dc = ctx.dc
    dc.fillStyle = b.colorBackground !== undefined ? b.colorBackground : '#8595BD'
    dc.fillRect(b.x, b.y + 0.5 * b.h, b.w, 0.5 * b.h)
    dc.strokeStyle = 'black'
    dc.lineWidth = 2
    dc.beginPath()
    dc.moveTo(b.x, b.y)
    dc.lineTo(b.x, b.y + b.h)
    dc.lineTo(b.x + b.w, b.y + b.h)
    dc.lineTo(b.x + b.w, b.y)
    dc.stroke()
    const v = b.parameters['V']
    if (v !== undefined) {
      const txt = v + ' m³'
      drawCenterText(ctx, txt, { offsetY: -0.2 })
    }
    drawInteractiveElement(ctx)
  },
  supportedDropTypes: ['data-tag'],
}

export default StorageBlock

