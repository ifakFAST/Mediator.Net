
export interface Config {
  Items: ConfigItem[]
  ShowHeader: boolean
}

export interface ConfigItem {
  Name?: string
  Unit: string
  Object: string | null
  Member: string | null
  Type: 'Range' | 'Enum'
  MinValue: number | null
  MaxValue: number | null
  EnumValues: string
}

export interface ItemValue {
  Object: string
  Member: string
  Value: string
  CanEdit: boolean
}
