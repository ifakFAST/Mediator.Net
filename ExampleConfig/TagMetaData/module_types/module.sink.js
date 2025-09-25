import { makeInput } from './util.js'

const SinkBlock = {
  id: 'sink',
  parameters: [],
  defineIOs() {
    return [makeInput('u', 'left', 'Water')]
  },
}

export default SinkBlock

