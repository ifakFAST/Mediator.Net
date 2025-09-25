import { makeInput, makeOutput, drawInteractiveElement } from './util.js'

const SeparationBlock = {
  id: 'separation',
  parameters: [],
  defineIOs() {
    return [
      makeInput('yin', 'left', 'Water', 0.02),
      makeInput('qrs', 'left', 'Signal', 0.8),
      makeOutput('yef', 'right', 'Water', 0.02),
      makeOutput('yrs', 'bottom', 'Water'),
    ]
  },
  customDraw(ctx) {
    drawInteractiveElement(ctx)
  },
  supportedDropTypes: ['data-tag'],
}

export default SeparationBlock

