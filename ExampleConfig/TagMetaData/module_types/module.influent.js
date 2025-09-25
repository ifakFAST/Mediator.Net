import { makeOutput } from './util.js'

// Influent water source block
const InfluentBlock = {
  id: 'influent',
  parameters: [],
  defineIOs() {
    return [makeOutput('y', 'right', 'Water')]
  },
}

export default InfluentBlock

