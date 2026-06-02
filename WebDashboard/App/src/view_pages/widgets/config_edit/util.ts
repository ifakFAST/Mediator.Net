import type { ConfigItem, DefaultableItem } from './types'

export interface EnumValEntry {
  num: number
  label: string
  color?: string
}

export function parseEnumValues(it: string): EnumValEntry[] {
  const res: EnumValEntry[] = []
  try {
    const vals: string[] = it.split(';')
    for (const item of vals) {
      const items: string[] = item.split('=')
      if (items.length === 2) {
        const key = items[0].trim()
        const value = items[1].trim()
        const isComplex = value.startsWith('{') && value.endsWith('}')

        const entry: EnumValEntry = {
          num: parseFloat(key),
          label: value,
        }

        if (isComplex) {
          const inner = value.substring(1, value.length - 1)
          const props = inner.split(',')
          entry.label = props[0].trim()
          if (props.length > 1) {
            entry.color = props[1].trim()
          }
        }

        res.push(entry)
      }
    }
  } catch {}
  return res
}

export async function onWriteItemEnum(
  it: ConfigItem,
  oldValue: string,
  enumInputDlg: (title: string, message: string, value: string, values: string[]) => Promise<string | null>,
  backendAsync: (request: string, parameters: object) => Promise<any>,
): Promise<void> {
  const hint = 'Select new value'

  const vals: EnumValEntry[] = parseEnumValues(it.EnumValues)
  const values = vals.map((v) => v.label)
  const newValue = await enumInputDlg(it.Name ?? '', hint, oldValue, values)
  if (newValue === null) {
    return
  }

  let newValueNum = ''
  for (const item of vals) {
    if (item.label === newValue) {
      newValueNum = JSON.stringify(item.num)
    }
  }

  const para = {
    theObject: it.Object,
    member: it.Member,
    jsonValue: newValueNum,
    displayValue: newValue,
    oldValue,
  }
  try {
    await backendAsync('WriteValue', para)
  } catch (exp) {
    alert(exp)
  }
  // console.info('Config: ' + JSON.stringify(this.config))
}

export async function onWriteItemNumeric(
  it: ConfigItem,
  oldValue: string,
  textInputDlg: (title: string, message: string, value: string, valid: (str: string) => string) => Promise<string | null>,
  backendAsync: (request: string, parameters: object) => Promise<any>,
): Promise<void> {
  const isValid = (str: string) => {
    try {
      const json = JSON.parse(str)
      if (typeof json === 'number') {
        const num: number = json
        if (it.MinValue !== null && num < it.MinValue) {
          return 'Minimum value is ' + it.MinValue
        }
        if (it.MaxValue !== null && num > it.MaxValue) {
          return 'Maximum value is ' + it.MaxValue
        }
        return ''
      } else {
        return 'Value must be a number'
      }
    } catch {
      return 'Not a valid value'
    }
  }

  const hint = it.MinValue !== null && it.MaxValue !== null ? `New value in range [${it.MinValue}, ${it.MaxValue}]` : ''
  const newValue = await textInputDlg(it.Name ?? '', hint, oldValue, isValid)
  if (newValue === null) {
    return
  }

  const para = {
    theObject: it.Object,
    member: it.Member,
    jsonValue: newValue,
    displayValue: newValue,
    oldValue,
  }
  try {
    await backendAsync('WriteValue', para)
  } catch (exp) {
    alert(exp)
  }
  // console.info('Config: ' + JSON.stringify(this.config))
}

export function hasDefaultValue(item: DefaultableItem): boolean {
  const v = item.DefaultValue
  return v !== undefined && v !== null && String(v).trim() !== ''
}

function parseRangeDefaultNumber(raw: string): number | null {
  const trimmed = raw.trim()
  if (trimmed === '') {
    return null
  }
  try {
    const json = JSON.parse(trimmed)
    if (typeof json === 'number' && Number.isFinite(json)) {
      return json
    }
  } catch {
    /* fall through */
  }
  const num = Number(trimmed)
  if (Number.isFinite(num)) {
    return num
  }
  return null
}

function parseExactNumber(raw: string): number | null {
  const trimmed = raw.trim()
  if (trimmed === '') {
    return null
  }
  try {
    const json = JSON.parse(trimmed)
    if (typeof json === 'number' && Number.isFinite(json)) {
      return json
    }
  } catch {
    /* fall through */
  }
  const num = Number(trimmed)
  if (Number.isFinite(num) && String(num) === trimmed) {
    return num
  }
  return null
}

export function validateItemDefaultValue(item: DefaultableItem): string {
  if (!hasDefaultValue(item)) {
    return ''
  }
  if (item.Type === 'Range') {
    const num = parseRangeDefaultNumber(String(item.DefaultValue).trim())
    if (num === null) {
      return 'Default must be a number'
    }
    if (item.MinValue !== null && num < item.MinValue) {
      return 'Default is below minimum ' + item.MinValue
    }
    if (item.MaxValue !== null && num > item.MaxValue) {
      return 'Default is above maximum ' + item.MaxValue
    }
    return ''
  }
  if (resolveDefaultJsonValue(item) === null) {
    return 'Default must match an enum value or label'
  }
  return ''
}

export function resolveDefaultJsonValue(item: DefaultableItem): string | null {
  if (!hasDefaultValue(item)) {
    return null
  }
  const raw = String(item.DefaultValue).trim()
  if (item.Type === 'Range') {
    const num = parseRangeDefaultNumber(raw)
    if (num === null) {
      return null
    }
    if (item.MinValue !== null && num < item.MinValue) {
      return null
    }
    if (item.MaxValue !== null && num > item.MaxValue) {
      return null
    }
    return JSON.stringify(num)
  }
  const vals: EnumValEntry[] = parseEnumValues(item.EnumValues)
  const asNum = parseExactNumber(raw)
  if (asNum !== null) {
    for (const entry of vals) {
      if (entry.num === asNum) {
        return JSON.stringify(entry.num)
      }
    }
  }
  for (const entry of vals) {
    if (entry.label === raw) {
      return JSON.stringify(entry.num)
    }
  }
  return null
}

export function resolveDefaultDisplayValue(item: DefaultableItem): string | null {
  const json = resolveDefaultJsonValue(item)
  if (json === null) {
    return null
  }
  if (item.Type === 'Range') {
    return json
  }
  const num = JSON.parse(json) as number
  const vals: EnumValEntry[] = parseEnumValues(item.EnumValues)
  for (const entry of vals) {
    if (entry.num === num) {
      return entry.label
    }
  }
  return String(num)
}

export function normalizeItemDefaultValue(item: DefaultableItem): void {
  if (!hasDefaultValue(item)) {
    item.DefaultValue = null
    return
  }
  if (item.Type === 'Range') {
    const num = parseRangeDefaultNumber(String(item.DefaultValue).trim())
    if (num !== null) {
      item.DefaultValue = JSON.stringify(num)
    }
    return
  }
  const json = resolveDefaultJsonValue(item)
  if (json !== null) {
    item.DefaultValue = JSON.parse(json).toString()
  }
}
