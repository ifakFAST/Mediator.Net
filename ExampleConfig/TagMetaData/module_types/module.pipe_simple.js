import { paramNumeric, makeOutput, drawCenterTextAbove } from './util.js'

const PipeBlock = {
  id: 'pipe_simple',
  parameters: [
    paramNumeric('zeta_e', 'K factor, Resistance coefficient for fittings', 0.1, 0.00001, 10),
    paramNumeric('k', 'Absolute roughness (mm)', 0.02, 0.00001, 100),
    paramNumeric('l', 'Pipe length (m)', 2, 0.00001, 100000),
    paramNumeric('d', 'Pipe diameter (m)', 0.1, 0.00001, 100000),
  ],
  defineIOs() {
    return [makeOutput('i', 'left', 'Air,4'), makeOutput('o', 'right', 'Air,4')]
  },
  customDraw(ctx) {
    const b = ctx.block
    const dc = ctx.dc

    dc.strokeStyle = 'black'
    dc.lineWidth = 2

    dc.beginPath()
    dc.moveTo(b.x, b.y)
    dc.lineTo(b.x + b.w, b.y + b.h)
    dc.moveTo(b.x, b.y + b.h)
    dc.lineTo(b.x + b.w, b.y)
    dc.stroke()

    const zeta = b.parameters['zeta_e']
    const dia = b.parameters['d']
    const len = b.parameters['l']
    if (len !== undefined && dia !== undefined && zeta !== undefined) {
      const dia2 = JSON.parse(dia) * 1000.0
      const txt = dia2 + ', ' + len + ' m' + ', ' + zeta
      drawCenterTextAbove(ctx, txt)
    }
  },
}

export default PipeBlock

