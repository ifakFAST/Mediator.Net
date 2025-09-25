import { paramNumeric, paramString, makeOutput, drawCenterText } from './util.js'

// Constant signal source block
const ConstBlock = {
  id: 'const',
  parameters: [
    paramNumeric('Dimension', 'Dimension', 1, 1, 1000),
    paramString('Constant_0', 'Constant Value', '1'),
  ],
  defineIOs(parameters) {
    const t = 'Signal'
    const n = parameters['Dimension'] || '1'
    const type = n === '1' ? t : t + ',' + n
    return [makeOutput('y', 'right', type)]
  },
  customDraw(ctx) {
    const str = ctx.block.parameters['Constant_0'] || ''
    drawCenterText(ctx, str)
  },
}

export default ConstBlock

