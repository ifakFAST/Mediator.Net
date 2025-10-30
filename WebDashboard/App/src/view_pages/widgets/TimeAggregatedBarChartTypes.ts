export type TimeGranularityOption = 'Yearly' | 'Monthly' | 'Quarterly' | 'Weekly' | 'Daily'

export type WeekStartOption = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday'

export type BarAggregationOption = 'Average' | 'Sum'

export interface VariableRefUnresolved {
  Object: string
  Name: string
}

export interface TimeAggregatedBarChartDataSeries {
  Name: string
  Color: string
  Variable: VariableRefUnresolved
  Aggregation: BarAggregationOption
}

export interface TimeAggregatedBarChartMainConfig {
  StartTime: string
  EndTime?: string | null
  TimeGranularity: TimeGranularityOption
  WeekStart: WeekStartOption
  ShowSumOverBars: boolean
  SumFractionDigits: number
}

export interface TimeAggregatedBarChartConfig {
  ChartConfig: TimeAggregatedBarChartMainConfig
  DataSeries: TimeAggregatedBarChartDataSeries[]
}

export interface TimeAggregatedBarChartSeriesData {
  Name: string
  Color: string
  Values: number[]
}

export interface LoadDataResponse {
  Labels: string[]
  Series: TimeAggregatedBarChartSeriesData[]
}
