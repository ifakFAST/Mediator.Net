
import * as fast from '../../fast_types'

export type MemberTypeEnum = 'History' | 'String' | 'Text' | 'DataType' | 'Number' | 'Duration' | 'Boolean'

export function defaultValueFromMemberType(t: MemberTypeEnum): any {

  switch (t) {
    case 'History': {
      const h: fast.History = { Mode: 'None', Interval: null, Offset: null }
      return h
    }
    case 'String':   return ''
    case 'Text':     return ''
    case 'Boolean':  return 'false'
    case 'Duration': return '10 s'
    case 'DataType': return 'Float64'
    case 'Number':   return 0
    default:
      console.error('Can not get default value for unknown type: ' + t)
      return undefined
  }
}
