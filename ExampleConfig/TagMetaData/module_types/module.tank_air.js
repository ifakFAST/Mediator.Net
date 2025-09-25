import { paramNumeric, makeInput, drawInteractiveElement } from './util.js'

const TankAirBlock = {
  id: 'tank_air',
  parameters: [
    paramNumeric('nin_left', 'Inputs left', 1, 0, 100),
    paramNumeric('nin_right', 'Inputs right', 1, 0, 100),
    paramNumeric('nin_top', 'Inputs top', 0, 0, 100),
    paramNumeric('nin_bottom', 'Inputs bottom', 0, 0, 100),
  ],
  defineIOs(parameters) {
    const left = JSON.parse(parameters['nin_left'] || '1')
    const right = JSON.parse(parameters['nin_right'] || '1')
    const top = JSON.parse(parameters['nin_top'] || '0')
    const bottom = JSON.parse(parameters['nin_bottom'] || '0')
    const res = []
    for (let i = 0; i < left; ++i) res.push(makeInput('i' + i, 'left', 'Air,4'))
    for (let i = 0; i < right; ++i) res.push(makeInput('i' + i, 'right', 'Air,4'))
    for (let i = 0; i < top; ++i) res.push(makeInput('i' + i, 'top', 'Air,4'))
    for (let i = 0; i < bottom; ++i) res.push(makeInput('i' + i, 'bottom', 'Air,4'))
    return res
  },
  customDraw(ctx) {
    drawInteractiveElement(ctx)
  },
  supportedDropTypes: ['data-tag'],
}

export default TankAirBlock

