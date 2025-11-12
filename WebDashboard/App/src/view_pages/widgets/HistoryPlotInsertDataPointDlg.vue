<template>
  <v-dialog
    v-model="state.show"
    max-width="420px"
    persistent
    @keydown="onKeydown"
  >
    <v-card>
      <v-card-title class="text-wrap">{{ state.itemName }}</v-card-title>
      <v-card-text>
        <label class="text-subtitle-2 mb-2">Timestamp</label>
        <v-text-field
          v-model="state.timestampText"
          class="mb-2"
          :messages="[timestampHint]"
          hide-details="auto"
        ></v-text-field>
        <div class="text-caption text-grey mb-4 ml-1">Interpreted as: {{ formattedUTCTimestamp }}</div>
        <label class="text-subtitle-2 mb-2">Value</label>
        <v-text-field
          v-if="!isObject"
          v-model="state.valueText"
        ></v-text-field>
        <template v-if="isObject">
          <v-text-field
            v-for="member in objectMembers"
            :key="member.Name"
            v-model="memberValues[member.Name]"
            :label="member.Name"
          ></v-text-field>
        </template>
      </v-card-text>
      <v-card-actions>
        <v-btn
          v-if="editMode"
          color="red-darken-1"
          variant="text"
          @click="onDelete"
          >Delete</v-btn
        >
        <v-spacer></v-spacer>
        <v-btn
          color="grey-darken-1"
          variant="text"
          @click="onCancel"
          >Cancel</v-btn
        >
        <v-btn
          color="primary-darken-1"
          variant="text"
          @click="onSave"
          >{{ editMode ? 'Update' : 'Insert' }}</v-btn
        >
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { reactive, ref, computed } from 'vue'
import type { VariableInfo } from './common'

type Axis = 'Left' | 'Right'

interface ItemConfig {
  Name: string
  Axis: Axis
}

interface InsertDataPointResult {
  timestamp: number
  value: string
  delete: boolean
}

interface DialogState {
  show: boolean
  timestampText: string
  valueText: string
  itemName: string
}

const state = reactive<DialogState>({
  show: false,
  timestampText: '',
  valueText: '',
  itemName: '',
})

const timestampHint = 'YYYY-MM-DD HH:mm:ss (local time; append Z for UTC)'

let resolver: ((result: InsertDataPointResult | null) => void) | null = null

type MemberType = 'string' | 'number'
type TypedMember = { Name: string; Type: MemberType }

const editMode = ref<boolean>(false)
const isObject = ref<boolean>(false)
const objectMembers = ref<TypedMember[]>([])
const memberValues = ref<Record<string, string>>({})

const parseTypeConstraints = (str: string): TypedMember[] => {
  if (!str || /^\s*$/.test(str)) return []
  return str
    .split(',')
    .map((part) => part.trim())
    .filter((part) => part.length > 0)
    .map((part) => {
      const kv = part.split(':').filter((s) => s.length > 0) // remove empty entries
      if (kv.length !== 2) {
        throw new Error(`Invalid name/type pair: '${part}'`)
      }
      return {
        Name: kv[0].trim(),
        Type: kv[1].trim().toLowerCase() as MemberType,
      }
    })
}

const pad = (value: number): string => {
  return value.toString().padStart(2, '0')
}

const formatTimestampForDisplay = (timestamp: number): string => {
  const date = new Date(timestamp)
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(
    date.getMinutes(),
  )}:${pad(date.getSeconds())}`
}

const parseTimestampFromDisplay = (text: string): number | null => {
  const trimmed = text.trim()
  if (trimmed === '') {
    return null
  }
  const normalized = trimmed.replace(' ', 'T')
  const parsed = Date.parse(normalized)
  if (Number.isNaN(parsed)) {
    return null
  }
  return parsed
}

const formattedUTCTimestamp = computed<string>(() => {
  const timestamp = parseTimestampFromDisplay(state.timestampText)
  if (timestamp === null) {
    return 'Invalid timestamp'
  }
  const date = new Date(timestamp)
  return `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())} ${pad(date.getUTCHours())}:${pad(
    date.getUTCMinutes(),
  )}:${pad(date.getUTCSeconds())} UTC`
})

const open = async (edit: boolean, timestamp: number, yvalue: string, item: ItemConfig, variableInfo: VariableInfo, initialMemberValues?: Map<string, string>): Promise<InsertDataPointResult | null> => {
  state.timestampText = formatTimestampForDisplay(timestamp)
  state.valueText = yvalue
  state.itemName = item.Name
  state.show = true
  editMode.value = edit

  isObject.value = variableInfo.Type == 'Struct' && variableInfo.Dimension == 1
  if (isObject.value) {
    try {
      objectMembers.value = parseTypeConstraints(variableInfo.TypeConstraints)

      memberValues.value = {}

      const valueMember = objectMembers.value.find((m) => {
        const name = m.Name.toLowerCase()
        return name === 'value' || name === 'val' || name === 'v'
      })

      if (valueMember) {
        memberValues.value[valueMember.Name] = state.valueText
      }

      if (initialMemberValues) {
        for (const [key, value] of initialMemberValues) {
          memberValues.value[key] = value
        }
      }

    } catch (err: any) {
      alert(`Failed to parse variable type constraints: ${err.message}`)
      objectMembers.value = []
      memberValues.value = {}
    }
  }

  return await new Promise<InsertDataPointResult | null>((resolve) => {
    resolver = resolve
  })
}

const closeDialog = (): void => {
  state.show = false
}

const resolveAndReset = (result: InsertDataPointResult | null): void => {
  const currentResolver = resolver
  resolver = null
  currentResolver?.(result)
}

const onCancel = (): void => {
  closeDialog()
  resolveAndReset(null)
}

const onDelete = (): void => {
  if (!confirm('Are you sure you want to delete this data point?')) {
    return
  }
  const timestamp = parseTimestampFromDisplay(state.timestampText)
  if (timestamp === null) {
    alert('Invalid timestamp format.')
    return
  }
  closeDialog()
  resolveAndReset({
    timestamp,
    value: '',
    delete: true
  })
}

function parseNumberSafely(str: string): number | null {
  if (!str || !str.trim()) {
    return null // Explicitly handle empty input
  }
  const num = Number(str)
  return isNaN(num) ? null : num
}

const onSave = (): void => {
  const timestamp = parseTimestampFromDisplay(state.timestampText)
  if (timestamp === null) {
    alert('Invalid timestamp format.')
    return
  }
  let value: string

  if (isObject.value) {
    // Construct JSON object from member values
    const obj: Record<string, string | number> = {}
    for (const member of objectMembers.value) {
      const val = memberValues.value[member.Name] || ''
      if (member.Type === 'number') {
        const parsedNumber = parseNumberSafely(val)
        if (parsedNumber === null) {
          alert(`Invalid number format for '${member.Name}'.`)
          return
        }
        obj[member.Name] = parsedNumber
      } else {
        obj[member.Name] = val
      }
    }
    value = JSON.stringify(obj)
    console.log('Constructed object value:', value)
  } else {
    const parsedNumber = parseNumberSafely(state.valueText)
    if (parsedNumber === null) {
      alert('Invalid number format for value.')
      return
    }
    value = state.valueText
  }

  closeDialog()
  resolveAndReset({
    timestamp,
    value,
    delete: false
  })
}

const onKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    onCancel()
  }
}

defineExpose({
  open,
})
</script>
