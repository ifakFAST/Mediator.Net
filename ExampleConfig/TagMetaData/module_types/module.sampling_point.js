import { makeInput, makeOutput, drawInteractiveElement } from './util.js'

const SamplingPointBlock = {
  id: 'sampling_point',
  parameters: [],
  defineIOs() {
    return [makeInput('u', 'left', 'Water'), makeOutput('y', 'right', 'Water')]
  },
  customDraw(ctx) {
    drawInteractiveElement(ctx)
  },
  supportedDropTypes: ['data-tag'],
}

export default SamplingPointBlock

