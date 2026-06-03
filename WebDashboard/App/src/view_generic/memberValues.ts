import type { ObjectMember } from './types'

const historyTypeConstraint = 'Ifak.Fast.Mediator.History'
const nullableHistoryFields = new Set(['Interval', 'Offset', 'Deadband'])

const isObject = (value: unknown): value is Record<string, unknown> => {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

const normalizeJsonValue = (value: unknown): unknown => {
  if (Array.isArray(value)) {
    return value.map((item) => normalizeJsonValue(item))
  }
  if (isObject(value)) {
    const result: Record<string, unknown> = {}
    for (const key of Object.keys(value).sort()) {
      const normalized = normalizeJsonValue(value[key])
      if (normalized !== undefined) {
        result[key] = normalized
      }
    }
    return result
  }
  return value
}

const isHistoryMember = (member: ObjectMember): boolean => {
  return member.Type === 'Struct' && member.TypeConstraints === historyTypeConstraint
}

const normalizeHistoryValue = (value: unknown): unknown => {
  const normalized = normalizeJsonValue(value)
  if (!isObject(normalized)) {
    return normalized
  }

  const result: Record<string, unknown> = { ...normalized }
  for (const field of nullableHistoryFields) {
    if (result[field] === null || result[field] === undefined) {
      delete result[field]
    }
  }
  return result
}

const normalizeMemberValue = (member: ObjectMember, value: unknown): unknown => {
  return isHistoryMember(member) ? normalizeHistoryValue(value) : normalizeJsonValue(value)
}

export const isMemberValueChanged = (member: ObjectMember): boolean => {
  return JSON.stringify(normalizeMemberValue(member, member.ValueOriginal)) !== JSON.stringify(normalizeMemberValue(member, member.Value))
}

export const memberValueToJson = (member: ObjectMember): string => {
  return JSON.stringify(normalizeMemberValue(member, member.Value))
}
