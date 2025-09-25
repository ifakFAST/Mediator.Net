import { makeInput, makeOutput } from './util.js'

const PumpBlock = {
  id: 'pump',
  parameters: [],
  defineIOs() {
    return [
      makeInput('yin', 'left', 'Water', 0.2),
      makeInput('qpu', 'left', 'Signal', 0.66),
      makeOutput('ymain', 'right', 'Water', 0.2),
      makeOutput('ypump', 'bottom', 'Water'),
    ]
  },
}

export default PumpBlock

