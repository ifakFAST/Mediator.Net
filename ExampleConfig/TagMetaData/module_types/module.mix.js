import { paramNumeric, makeInput, makeOutput } from './util.js'

// Mixer block with configurable number of inputs
const MixBlock = {
  id: 'mix',
  parameters: [paramNumeric('nin', 'Number of inputs', 3, 1, 20)],
  defineIOs(parameters) {
    const res = [makeOutput('y', 'right', 'Water')]
    const inputs = JSON.parse(parameters['nin'] || '3')
    for (let i = 0; i < inputs; ++i) {
      res.push(makeInput('in_' + i, 'left', 'Water'))
    }
    return res
  },
  customDraw(ctx) {
    const b = ctx.block
    const dc = ctx.dc
    const centerX = b.x + 0.5 * b.w
    const centerY = b.y + 0.5 * b.h
    dc.lineWidth = 3
    dc.beginPath()
    for (const p of b.ports) {
      dc.moveTo(p.x, p.y)
      dc.lineTo(centerX, centerY)
    }
    dc.stroke()
  },
}

export default MixBlock

