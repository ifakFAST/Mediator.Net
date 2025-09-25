import { paramString, makeInput, makeOutput } from './util.js'

const DividerBlock = {
  id: 'divider',
  parameters: [paramString('Dist', 'Flow distribution', '1; 1; 1')],
  defineIOs(parameters) {
    const res = [makeInput('x', 'left', 'Water')]
    const dist = parameters['Dist'] || '1; 1; 1'
    const splits = dist.split(';')
    for (let i = 0; i < splits.length; ++i) {
      res.push(makeOutput('out_' + i, 'right', 'Water'))
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

export default DividerBlock

