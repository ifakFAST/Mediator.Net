import { paramNumeric, makeInput, drawCenterTextAbove } from './util.js'

const PressureSourceBlock = {
  id: 'pressure_source',
  parameters: [paramNumeric('P', 'Atmospheric pressure (mbar)', 1013.25, 0, 100000)],
  defineIOs() {
    return [makeInput('o', 'right', 'Air,4')]
  },
  customDraw(ctx) {
    const b = ctx.block
    const P = b.parameters['P']
    if (P !== undefined) {
      const p = Math.round(JSON.parse(P))
      const txt = p + ' mbar'
      drawCenterTextAbove(ctx, txt)
    }
  },
}

export default PressureSourceBlock

