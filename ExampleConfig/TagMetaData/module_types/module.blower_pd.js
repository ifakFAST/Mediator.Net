import { paramNumeric, paramString, makeInput, makeOutput } from './util.js'

const BlowerPDBlock = {
  id: 'blower_pd',
  parameters: [
    paramNumeric('q_max', 'Theoretical air flow rate at design point', 1000, 0.01, 1000000),
    paramNumeric('eta_mech', 'Mechanical efficiency', 0.96, 0.01, 1),
    paramNumeric('q_v_100', 'Gap losses at 100 mbar', 10, 0.01, 10000),
    paramNumeric('eta_04', 'Electrical efficiency (eta) at 40% speed', 0.6, 0.01, 1),
    paramNumeric('eta_08', 'Electrical efficiency (eta) at 80% speed', 0.96, 0.01, 1),
    paramString('T1', 'Time delay', '2/(24*60)'),
  ],
  defineIOs() {
    return [makeInput('x', 'top', 'Signal'), makeOutput('i', 'left', 'Air,4'), makeOutput('o', 'right', 'Air,4')]
  },
}

export default BlowerPDBlock

