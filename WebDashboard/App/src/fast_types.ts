export type DataValue = string

export type Duration = string

export type LocationRef = string

export type ObjectRef = string

export type Timestamp = string

export interface VariableRef {
  Object: ObjectRef
  Name: string
}

export interface MemberRef {
  Object: ObjectRef
  Name: string
}

export interface VTQ {
  V: DataValue
  T: Timestamp
  Q: Quality
}

export type Quality = 'Good' | 'Bad' | 'Uncertain'

export interface History {
  Mode: HistoryMode
  Interval: Duration | null
  Offset: Duration | null
}

export interface LocationInfo {
  ID: string
  Name: string
  LongName: string
  Parent: string
}

export type HistoryMode =
  | 'None'
  | 'Complete'
  | 'ValueOrQualityChanged'
  | 'Interval'
  | 'IntervalExact'
  | 'IntervalOrChanged'
  | 'IntervalExactOrChanged'

export const HistoryModeValues: HistoryMode[] = [
  'None',
  'Complete',
  'ValueOrQualityChanged',
  'Interval',
  'IntervalExact',
  'IntervalOrChanged',
  'IntervalExactOrChanged',
]

export type DataType =
  | 'Bool'
  | 'Byte'
  | 'SByte'
  | 'Int16'
  | 'UInt16'
  | 'Int32'
  | 'UInt32'
  | 'Int64'
  | 'UInt64'
  | 'Float32'
  | 'Float64'
  | 'String'
  | 'JSON'
  | 'Guid'
  | 'ObjectRef'
  | 'NamedValue'
  | 'LocationRef'
  | 'URI'
  | 'LocalDate'
  | 'LocalTime'
  | 'LocalDateTime'
  | 'Timestamp'
  | 'Duration'
  | 'Enum'
  | 'Struct'
  | 'Timeseries'

export const DataTypeValues: DataType[] = [
  'Bool',
  'Byte',
  'SByte',
  'Int16',
  'UInt16',
  'Int32',
  'UInt32',
  'Int64',
  'UInt64',
  'Float32',
  'Float64',
  'String',
  'JSON',
  'Guid',
  'ObjectRef',
  'NamedValue',
  'LocationRef',
  'URI',
  'LocalDate',
  'LocalTime',
  'LocalDateTime',
  'Timestamp',
  'Duration',
  'Enum',
  'Struct',
  'Timeseries',
]
