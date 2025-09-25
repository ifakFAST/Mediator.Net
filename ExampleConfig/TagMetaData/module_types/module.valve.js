import { paramEnum, paramNumeric, makeInput, makeOutput } from './util.js'

const ValveBlock = {
  id: 'valve',
  parameters: [
    paramEnum('KvCv', 'Resistance coefficient type', 'Kv', ['Kv', 'Cv']),
    paramNumeric('kv100', 'Kv100 (Cv100) value, maximum flow at norm conditions, full open', 100, 0.00001, 10000000),
    paramNumeric('kv50', 'Kv50 (Cv50) value, flow at norm conditions, 50% open', 30, 0.00001, 10000000),
    paramNumeric('kv0', 'Kv0 (Cv0) value, minimum flow at norm conditions, closed', 3, 0.00001, 10000000),
    paramEnum('valvetype', 'Valve type', 'linear', ['linear', 'equal_percentage', 'quadratic']),
  ],
  defineIOs() {
    return [
      makeOutput('i', 'left', 'Air,4'),
      makeOutput('o', 'right', 'Air,4'),
      makeInput('h', 'top', 'Signal'),
      makeOutput('y', 'bottom', 'Signal'),
    ]
  },
}

export default ValveBlock

