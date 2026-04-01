export interface ObjInfo {
  ID: string
  Name: string
  Variables: string[]
}

export interface ModuleInfo {
  ID: string
  Name: string
}

export const mapObjects = new Map<string, ObjInfo>()

export const modules: ModuleInfo[] = []

export interface AdapterInfo {
  Type: string
  Show_WindowVisible: boolean
  Show_Definition: boolean
  DefinitionLabel: string
  DefinitionIsCode: boolean
  CodeLang: string
  Subtypes: string[]
}

export interface CalcLogEvent {
  CalcID: string
  Entries: CalcLogEntry[]
}

export interface CalcLogEntry {
  ID: number
  Line: string
  Level: 'Info' | 'Warning' | 'Error'
}