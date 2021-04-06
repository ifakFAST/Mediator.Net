
export interface TreeNode {
  ID: string
  ParentID: string
  First: boolean
  Last: boolean
  Name: string
  Type: string
  Variables: VariableVal[]
  Children: TreeNode[]
}

export interface VariableVal {
  Name: string
  Struct: boolean
  Dim: number
  V: string
  T: string
  Q: Quality
}

export type Quality = 'Good' | 'Uncertain' | 'Bad'

export interface TypeMap {
  [key: string]: TypeInfo
}

export interface TypeInfo {
  ObjectMembers: ObjMemInfo[]
}

export interface ObjMemInfo {
  Type: string
  Array: string
}

export interface ObjectMember {
  Key: string
  Name: string
  Type: string
  IsScalar: boolean
  IsOption: boolean
  IsArray: boolean
  Category: string
  Browseable: boolean
  BrowseValues: string[]
  BrowseValuesLoading: boolean
  Value: string
  ValueOriginal: string
  EnumValues: string[]
  StructMembers: StructMember[]
  DefaultValue: string
}

export interface StructMember {
  Name: string
  Type: string
  IsScalar: boolean
  IsOption: boolean
  IsArray: boolean
  EnumValues: string[]
  StructMembers: StructMember[]
}

export interface ChildType {
  TypeName: string
  Members: string[]
}

export interface ObjectMap {
  [key: string]: TreeNode
}

export interface AddObjectParams {
  ParentObjID: string
  ParentMember: string
  NewObjID: string
  NewObjType: string
  NewObjName: string
}

export interface SaveMember {
  Name: string
  Value: string
}
