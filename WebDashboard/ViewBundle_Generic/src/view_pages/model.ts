
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
  PaddingOverride?: string
  Config?: object
  EventName?: string
  EventPayload?: object
}

export interface ConfigVariableValues {
  VarDefs: ConfigVariable[]
  VarValues: Record<string, string>
}

export interface ConfigVariable {
  ID: string
  DefaultValue: string
}

export class VariableReplacer {

  // Pattern matches ${varID.key} where varID and key are non-empty strings not containing closing braces
  private static readonly variablePattern = /\$\{([^\}]+)\}/g;

  public static replaceVariables(input: string, variables: Record<string, string>): string {
    
      if (!input || !variables) {
          return input;
      }

      return input.replace(this.variablePattern, (match: string, varId: string) => {
          const value = variables[varId];
          if (value === undefined) {
              return match; // Keep original if varId not found
          }
          return value;
      });
  }
}