<template>
  <div class="d-flex flex-column overflow-hidden" style="height: 100%">
    <v-toolbar density="compact" class="flex-shrink-0">
      <v-checkbox
        v-model="includeErrors"
        class="mr-4 ml-2"
        label="Error"
        hide-details
      />
      <v-checkbox
        v-model="includeWarnings"
        class="mr-4"
        label="Warn"
        hide-details
      />
      <v-checkbox
        v-model="includeInfos"
        class="mr-4"
        label="Info"
        hide-details
      />
      <v-text-field
        v-model="search"
        class="mr-2"
        append-inner-icon="mdi-magnify"
        label="Search"
        single-line
        hide-details
      />
      <v-btn
        icon="mdi-minus"
        size="small"
        @click="decreaseFontSize"
      />
      <span class="text-caption mx-1">{{ fontSize }}px</span>
      <v-btn
        icon="mdi-plus"
        size="small"
        class="mr-2"
        @click="increaseFontSize"
      />
      <v-btn
        :icon="wordWrap ? 'mdi-wrap' : 'mdi-wrap-disabled'"
        size="small"
        :color="wordWrap ? 'primary' : undefined"
        class="mr-2"
        title="Toggle word wrap"
        @click="toggleWordWrap"
      />
      <v-btn
        icon="mdi-delete-outline"
        size="small"
        class="mr-2"
        title="Clear log"
        @click="clearLog"
      />
      <v-btn
        icon="mdi-refresh"
        size="small"
        title="Refresh"
        @click="refreshLog"
      />
    </v-toolbar>

    <div
      ref="scrollContainer"
      class="calclog-container elevation-4 mt-2"
      :style="{ fontSize: fontSize + 'px' }"
    >
      <div
        v-for="(entry, index) in filteredEntries"
        :key="entry.ID"
        :class="[entry.Level === 'Error' ? 'calclog-error' : '', entry.Level === 'Warning' ? 'calclog-warning' : '', wordWrap ? 'calclog-line-wrap' : 'calclog-line-nowrap']"
        class="calclog-line"
      >{{ entry.Line }}</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onBeforeUnmount } from 'vue'
import type { CalcLogEvent, CalcLogEntry } from './global'

interface DashboardApi {
  sendViewRequestAsync(request: string, payload: unknown, responseType?: 'text' | 'blob'): Promise<unknown>
}

const FONT_SIZE_KEY = 'calclog-font-size'
const DEFAULT_FONT_SIZE = 13
const MIN_FONT_SIZE = 8
const MAX_FONT_SIZE = 28

const props = defineProps<{
  calcId: string
  active: boolean
  newCalcLogEvent: CalcLogEvent | null
}>()

const savedFontSize = parseInt(localStorage.getItem(FONT_SIZE_KEY) ?? '', 10)
const fontSize = ref(savedFontSize >= MIN_FONT_SIZE && savedFontSize <= MAX_FONT_SIZE ? savedFontSize : DEFAULT_FONT_SIZE)

const setFontSize = (size: number): void => {
  fontSize.value = Math.min(MAX_FONT_SIZE, Math.max(MIN_FONT_SIZE, size))
  localStorage.setItem(FONT_SIZE_KEY, String(fontSize.value))
}
const increaseFontSize = (): void => setFontSize(fontSize.value + 1)
const decreaseFontSize = (): void => setFontSize(fontSize.value - 1)

const WORD_WRAP_KEY = 'calclog-word-wrap'
const wordWrap = ref(localStorage.getItem(WORD_WRAP_KEY) === 'true')
const toggleWordWrap = (): void => {
  wordWrap.value = !wordWrap.value
  localStorage.setItem(WORD_WRAP_KEY, String(wordWrap.value))
}

const search = ref('')
const includeErrors = ref(true)
const includeWarnings = ref(true)
const includeInfos = ref(true)
const entries = ref<CalcLogEntry[]>([])
const setOfEntryIds: Set<number> = new Set()
const scrollContainer = ref<HTMLElement | null>(null)
let requestVersion = 0

const dashboard = window.parent['dashboardApp'] as DashboardApi

const updateEntriesMap = (): void => {
  setOfEntryIds.clear()
  entries.value.forEach((e) => setOfEntryIds.add(e.ID))
}

const startPolling = async (): Promise<void> => {
  const version = ++requestVersion
  const requestedCalcId = props.calcId
  try {
    const res = await dashboard.sendViewRequestAsync('StartCalcLogWatch', { CalcID: requestedCalcId })
    if (version !== requestVersion || props.calcId !== requestedCalcId || !props.active) return
    entries.value = res as CalcLogEntry[]
    updateEntriesMap()
  } 
  catch (error) {
    console.error('Failed to start calc log watch.', error)
  }
}

const stopPolling = (): void => {
  requestVersion++
  dashboard.sendViewRequestAsync('StopCalcLogWatch', {}).catch((error) => {
    console.error('Failed to stop calc log watch.')
  })
}

const refreshLog = async (): Promise<void> => {
  const version = ++requestVersion
  const requestedCalcId = props.calcId
  try {
    const res = await dashboard.sendViewRequestAsync('GetCalcLog', { CalcID: requestedCalcId })
    if (version !== requestVersion || props.calcId !== requestedCalcId) return
    entries.value = res as CalcLogEntry[]
    updateEntriesMap()
  } 
  catch (error) {
    console.error('Failed to refresh calc log.', error)
  }
}

const clearLog = async (): Promise<void> => {
  try {
    await dashboard.sendViewRequestAsync('ClearCalcLog', { CalcID: props.calcId })
    entries.value = []
    setOfEntryIds.clear()
  } 
  catch (error) {
    console.error('Failed to clear calc log.', error)
  }
}

const isAtBottom = (): boolean => {
  const el = scrollContainer.value
  if (!el) return true
  return el.scrollHeight - el.scrollTop - el.clientHeight < 60
}

const scrollToBottom = (): void => {
  const el = scrollContainer.value
  if (el) {
    el.scrollTop = el.scrollHeight
  }
}

const filteredEntries = computed((): CalcLogEntry[] => {
  const error = includeErrors.value
  const warn = includeWarnings.value
  const info = includeInfos.value
  const allLevels = error && warn && info
  const s = search.value.toLowerCase()
  const hasSearch = s.length > 0

  if (allLevels && !hasSearch) return entries.value

  return entries.value.filter((e) => {
    if (!allLevels) {
      if (e.Level === 'Error' && !error) return false
      if (e.Level === 'Warning' && !warn) return false
      if (e.Level === 'Info' && !info) return false
    }
    if (hasSearch && !e.Line.toLowerCase().includes(s)) return false
    return true
  })
})

watch(
  () => props.newCalcLogEvent,
  (newEvent) => {
    if (props.active && newEvent && newEvent.CalcID === props.calcId) {
      const atBottom = isAtBottom()
      // make sure the new entries are not already contained in the entries array:
      const set = setOfEntryIds
      const uniqueNewEntries = newEvent.Entries.filter((e) => !set.has(e.ID))
      if (uniqueNewEntries.length > 0) {
        entries.value.push(...uniqueNewEntries)
        if (entries.value.length > 1000) {
          const removeFrontCount = entries.value.length - 900 // keep the latest 900 entries
          entries.value.splice(0, removeFrontCount)
          updateEntriesMap()
        }
        else {
          uniqueNewEntries.forEach((e) => set.add(e.ID))
        }
        if (atBottom) {
          nextTick(() => {
            scrollToBottom()
          })
        }
      }      
    }
  },
)

watch(
  () => props.active,
  (active) => {
    if (active) {
      startPolling()
    } 
    else {
      stopPolling()
    }
  },
  { immediate: true },
)

watch(
  () => props.calcId,
  () => {
    if (props.active) {
      entries.value = []
      setOfEntryIds.clear()
      startPolling()
    }
  },
)

onBeforeUnmount(() => {
  stopPolling()
})
</script>

<style scoped>
.calclog-container {
  font-family: monospace;
  overflow-y: auto;
  overflow-x: auto;
  flex: 1 1 0;
  min-height: 0;
  background-color: #1e1e1e;
  padding: 8px;
}
.calclog-line {
  line-height: 1.5;
  color: #d4d4d4;
}
.calclog-line-wrap {
  white-space: pre-wrap;
  word-break: break-all;
}
.calclog-line-nowrap {
  white-space: pre;
}
.calclog-line.calclog-error {
  color: #f7421d;
}
.calclog-line.calclog-warning {
  color: #e58e2b;
}
</style>
