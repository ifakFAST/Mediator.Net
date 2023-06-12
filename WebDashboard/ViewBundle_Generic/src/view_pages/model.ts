
export interface Page {
  ID: string
  Name: string
  Rows: Row[]
}

export interface Row {
  Columns: Column[]
}

export interface Column {
  Width: ColumnWidth
  Widgets: Widget[]
}

export type ColumnWidth = 'Fill' | 'Auto' | 'OneOfTwelve' | 'TwoOfTwelve' | 'ThreeOfTwelve' | 'FourOfTwelve' |
                   'FiveOfTwelve' | 'SixOfTwelve' | 'SevenOfTwelve' | 'EightOfTwelve' | 'NineOfTwelve' |
                   'TenOfTwelve' | 'ElevenOfTwelve' | 'TwelveOfTwelve'

export interface Widget {
  ID: string
  Type: string
  Title: string
  Height?: string
  Width?: string
  Config?: object
  EventName?: string
  EventPayload?: object
}
