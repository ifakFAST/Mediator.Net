import type { Variable } from './common'

export interface XyPlotConfig {
  PlotConfig: XyPlotPlotConfig
  DataSeries: XyPlotDataSeries[]
}

export interface XyPlotPlotConfig {
  MaxDataPoints: number
  FilterByQuality: string
  XAxisName: string
  YAxisName: string
  XAxisStartFromZero: boolean
  YAxisStartFromZero: boolean
  XAxisLimitMin: number | null
  XAxisLimitMax: number | null
  YAxisLimitMin: number | null
  YAxisLimitMax: number | null
  ShowGrid: boolean
  ShowLegend: boolean
  Show45DegreeLine: boolean
  Color45DegreeLine: string
}

export interface XyPlotDataSeries {
  Name: string
  Color: string
  Size: number
  Checked: boolean
  VariableX: Variable
  VariableY: Variable
  Aggregation: string
  Resolution: string
  TimeHighlighting: string // None, LastN, LastN_Gradient
  TimeHighlightingLastN: number
  TimeHighlightingColor: string
  ShowRegression: string // None, Auto
  ColorRegression: string
}

///////////////////////////////////////////////////////

export interface XySeriesData {
  Name: string
  Color: string
  Size: number
  Checked: boolean
  Points: XyPoint[]
  Regression: Regression | null
}

export interface Regression {
  Slope: number
  Offset: number
  Color: string
}

export interface XyPoint {
  X: number
  Y: number
  Time: number
  Color?: string
}
