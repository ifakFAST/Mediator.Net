export interface ModuleInfo {
  ID: string
  Name: string
}

export interface Variable {
  Object: string
  Name: string
}

export interface Obj {
  Type: string
  ID: string
  Name: string
  Variables: string[]
  Members: string[]
}

export interface ObjInfo {
  Name: string
  Variables: string[]
}

export interface SelectObject {
  show: boolean
  modules: ModuleInfo[]
  selectedModuleID: string
  selectedObjectID: string
}

export interface ObjectMap {
  [key: string]: ObjInfo
}
