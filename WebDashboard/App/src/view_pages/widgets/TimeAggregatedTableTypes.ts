export type TimeGranularityOption = 'Yearly' | 'Monthly' | 'Quarterly' | 'Weekly' | 'Daily'

export type WeekStartOption = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday'

export type TableAggregationOption = 'Average' | 'Sum' | 'Min' | 'Max' | 'Count' | 'First' | 'Last'

export interface VariableRefUnresolved {
  Object: string
  Name: string
}

export interface TimeAggregatedTableDataSeries {
  Name: string
  Variable: VariableRefUnresolved
  Aggregation: TableAggregationOption
}

export interface TimeAggregatedTableMainConfig {
  StartTime: string
  EndTime?: string | null
  TimeGranularity: TimeGranularityOption
  WeekStart: WeekStartOption
  ShowTotalRow: boolean
  ShowTotalColumn: boolean
  TotalColumnAggregation: TableAggregationOption
  FractionDigits: number
}

export interface TimeAggregatedTableConfig {
  TableConfig: TimeAggregatedTableMainConfig
  DataSeries: TimeAggregatedTableDataSeries[]
}

export interface TimeAggregatedTableSeriesData {
  Name: string
  Color: string
  Values: number[]
}

export interface TimeAggregatedTableRow {
  Values: (number | null)[]
  Level: number
  ParentIndex?: number
  IsExpanded?: boolean
  CanExpand: boolean
  StartTime: string
  EndTime: string
  Granularity: TimeGranularityOption
  Children?: TimeAggregatedTableRow[]
}

export interface LoadDataResponse {
  Rows: TimeAggregatedTableRow[]
  SeriesNames: string[]
  TotalRow?: (number | null)[]
}

export interface LoadChildDataResponse {
  Rows: TimeAggregatedTableRow[]
}
