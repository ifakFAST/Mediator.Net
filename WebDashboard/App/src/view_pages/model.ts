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

export type ColumnWidth =
  | 'Fill'
  | 'Auto'
  | 'OneOfTwelve'
  | 'TwoOfTwelve'
  | 'ThreeOfTwelve'
  | 'FourOfTwelve'
  | 'FiveOfTwelve'
  | 'SixOfTwelve'
  | 'SevenOfTwelve'
  | 'EightOfTwelve'
  | 'NineOfTwelve'
  | 'TenOfTwelve'
  | 'ElevenOfTwelve'
  | 'TwelveOfTwelve'

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
  // Pattern matches ${varID} where varID ia a non-empty string not containing closing braces
  private static readonly variablePattern = /\$\{([^\}]+)\}/g

  public static replaceVariables(input: string, variables: Record<string, string>, notFoundReplacement?: string): string {
    return this.replaceVariablesBase(input, variables, this.variablePattern, notFoundReplacement)
  }

  private static replaceVariablesBase(input: string, variables: Record<string, string>, pattern: RegExp, notFoundReplacement?: string): string {
    if (!input || !variables) {
      return input
    }

    return input.replace(pattern, (match: string, varId: string) => {
      const value = variables[varId]
      if (value === undefined) {
        return notFoundReplacement ?? match
      }
      return value
    })
  }
}
