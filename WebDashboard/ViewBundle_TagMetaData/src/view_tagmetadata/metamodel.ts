// MetaModel type definitions

// Main MetaModel class
export interface MetaModel {
  Whats: What[]
  UnitGroups: UnitGroup[]
  Units: Unit[]
  Categories: Category[]
}

export interface What {
  ID: string
  Name: string
  ShortName: string
  UnitGroup: string // e.g. Concentration, Flow, Load, Power
  Category: string // e.g. Liquid, Gas, Nitrogen, Phosphorus, Dissolved Oxygen, Energy
  RefUnit: string
}

export interface Unit {
  ID: string
  UnitGroup: string
  IsSI: boolean // Default: true
  Factor: number // Default: 1.0
  Offset: number // Default: 0.0
}

export interface UnitGroup {
  ID: string // e.g. Concentration, Flow, Load, Power
}

export interface Category {
  ID: string // e.g. Liquid, Gas, Nitrogen, Phosphorus, Dissolved Oxygen, Energy
}

// Helper function to create an empty MetaModel instance
export function emptyMetaModel(): MetaModel {
  return {
    Whats: [],
    UnitGroups: [],
    Units: [],
    Categories: [],
  }
}
