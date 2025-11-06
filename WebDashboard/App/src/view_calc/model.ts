import * as fast from '../fast_types'

export interface CalcModel {
  ID: string
  Name: string
  RootFolder: Folder
}

export interface Folder {
  ID: string
  Name: string
  History: fast.History | null
  Folders: Folder[]
  Signals: Signal[]
  Calculations: Calculation[]
}

export interface Signal {
  ID: string
  Name: string
  Unit: string
  Type: fast.DataType
  Dimension: number
  TypeConstraints: string
  Location: fast.LocationRef | null // e.g. Influent/North Side
  Comment: string
  ValueSource: fast.VariableRef | null
  History: fast.History | null
}

export interface Calculation {
  ID: string
  Name: string
  Type: string // e.g. C#, SIMBA, ...
  Subtype: string
  RunMode: string // e.g. Continuous, Triggered, InputDriven
  InitialStartTime: fast.Timestamp // ISO 8601 date-time string
  InitErrorResponse: string // e.g. Fail, Retry, Stop
  HistoryScope: string // e.g. All, ExcludeInputs, ExcludeStates, ExcludeInputsAndStates
  Enabled: boolean
  EnableOutputVarWrite: boolean
  History: fast.History | null
  WindowVisible: boolean
  Cycle: fast.Duration
  Offset: fast.Duration
  IgnoreOffsetForTimestamps: boolean
  RealTimeScale: number
  Definition: string // e.g. C# code, SIMBA project file name
  Inputs: Input[]
  Outputs: Output[]
  States: State[]
}

export interface Input {
  ID: string
  Name: string
  Type: fast.DataType
  Dimension: number
  Unit: string
  Variable: fast.VariableRef | null
  Constant: fast.DataValue | null // if defined, its value will be used instead of Variable
}

export interface Output {
  ID: string
  Name: string
  Type: fast.DataType
  Dimension: number
  Unit: string
  Variable: fast.VariableRef | null
}

export interface State {
  ID: string
  Name: string
  Type: fast.DataType
  Dimension: number
  Unit: string
}
