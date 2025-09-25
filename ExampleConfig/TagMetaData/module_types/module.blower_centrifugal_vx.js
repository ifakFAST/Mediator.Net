import { paramString, makeInput, makeOutput } from './util.js'

const BlowerCentrifugalBlock = {
  id: 'blower_centrifugal_vx',
  parameters: [
    paramString('T1', 'Time constant of blower [d]', '2/(24*60)'),
    paramString('dP_shut', 'Maximum pressure (at flow=0) [mbar]', '800'),
    paramString('q_opt', 'Flow rate at design point [m³/d]', '1000'),
    paramString('dP_opt', 'Pressure at design point [mbar]', '500'),
    paramString('q_max', 'Maximum flow rate (at Pressure=0) [m³/d]', '1400'),
    paramString('dP_surge', 'Surge line pressure (at flow=0) [mbar]', '200'),
    paramString('dP_surge_opt', 'Surge line pressure (at design point) [mbar]', '1000'),
  ],
  defineIOs() {
    return [
      makeInput('x', 'top', 'Signal'),
      makeOutput('i', 'left', 'Air,4'),
      makeOutput('o', 'right', 'Air,4'),
      makeOutput('P', 'right', 'Signal'),
      makeOutput('y', 'right', 'Signal,10'),
    ]
  },
}

export default BlowerCentrifugalBlock

