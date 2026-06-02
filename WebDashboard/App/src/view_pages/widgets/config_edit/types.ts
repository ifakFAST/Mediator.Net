export interface Config {
  Items: ConfigItem[]
  ShowHeader: boolean
}

/** Fields required for default-value validation and apply (1D and 2D items). */
export interface DefaultableItem {
  Type: 'Range' | 'Enum'
  MinValue: number | null
  MaxValue: number | null
  EnumValues: string
  DefaultValue?: string | null
}

export interface ConfigItem extends DefaultableItem {
  Name?: string
  Unit: string
  Object: string | null
  Member: string | null
}

export interface ItemValue {
  Object: string
  Member: string
  Value: string
  CanEdit: boolean
}
