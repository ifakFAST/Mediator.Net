<template>
  <div>
    <v-toolbar density="compact">
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
      <v-spacer />
      <v-text-field
        v-model="search"
        class="mr-2"
        append-inner-icon="mdi-magnify"
        label="Search"
        single-line
        hide-details
      />
      <v-select
        v-model="maxLines"
        :items="maxLinesOptions"
        label="Max lines"
        hide-details
        style="max-width: 130px"
        class="mr-2"
        @update:model-value="load"
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
        icon="mdi-download"
        size="small"
        class="mr-2"
        title="Download logfile"
        @click="downloadLogfile"
      />
      <v-btn
        icon="mdi-refresh"
        @click="load"
      />
    </v-toolbar>

    <div
      ref="scrollContainer"
      class="logfile-container elevation-4 mt-2"
      :style="{ fontSize: fontSize + 'px' }"
    >
      <div
        v-for="(line, index) in filteredLines"
        :key="index"
        :class="[lineClass(line), wordWrap ? 'logfile-line-wrap' : 'logfile-line-nowrap']"
        class="logfile-line"
      >{{ line }}</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted } from 'vue'

const FONT_SIZE_KEY = 'logfile-font-size'
const DEFAULT_FONT_SIZE = 13
const MIN_FONT_SIZE = 8
const MAX_FONT_SIZE = 28

const savedFontSize = parseInt(localStorage.getItem(FONT_SIZE_KEY) ?? '', 10)
const fontSize = ref(savedFontSize >= MIN_FONT_SIZE && savedFontSize <= MAX_FONT_SIZE ? savedFontSize : DEFAULT_FONT_SIZE)

const setFontSize = (size: number): void => {
  fontSize.value = Math.min(MAX_FONT_SIZE, Math.max(MIN_FONT_SIZE, size))
  localStorage.setItem(FONT_SIZE_KEY, String(fontSize.value))
}
const increaseFontSize = (): void => setFontSize(fontSize.value + 1)
const decreaseFontSize = (): void => setFontSize(fontSize.value - 1)

const WORD_WRAP_KEY = 'logfile-word-wrap'
const wordWrap = ref(localStorage.getItem(WORD_WRAP_KEY) === 'true')
const toggleWordWrap = (): void => {
  wordWrap.value = !wordWrap.value
  localStorage.setItem(WORD_WRAP_KEY, String(wordWrap.value))
}

const search = ref('')
const maxLines = ref(500)
const maxLinesOptions = [50, 100, 200, 500, 1000, 2000, 5000, 10000]
const includeErrors = ref(true)
const includeWarnings = ref(true)
const includeInfos = ref(true)
const lines = ref<string[]>([])
const scrollContainer = ref<HTMLElement | null>(null)

const load = (): void => {
  // @ts-ignore
  window.parent['dashboardApp'].sendViewRequest('LoadLogfile', { MaxLines: maxLines.value }, (strResponse: string) => {
    const response = JSON.parse(strResponse)
    lines.value = response.Lines
    nextTick(() => scrollToBottom())
  })
}

const downloadLogfile = (): void => {
  // @ts-ignore
  window.parent['dashboardApp'].sendViewRequestBlob('DownloadLogfile', {}, (blob: Blob) => {
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'logfile.txt'
    a.click()
    URL.revokeObjectURL(url)
  })
}

const appendLines = (newLines: string[]): void => {
  const atBottom = isAtBottom()
  lines.value.push(...newLines)
  if (atBottom) {
    nextTick(() => scrollToBottom())
  }
}

defineExpose({ appendLines })

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

const lineClass = (line: string): Record<string, boolean> => {
  const parts = line.split(' ')
  const level = parts.length > 2 ? parts[2] : ''
  return {
    ErrWarning: level === 'WARN',
    ErrAlarm: level === 'ERROR' || level === 'FATAL',
  }
}

const getLevel = (line: string): string => {
  const parts = line.split(' ')
  return parts.length > 2 ? parts[2] : ''
}

const filteredLines = computed((): string[] => {
  const error = includeErrors.value
  const warn = includeWarnings.value
  const info = includeInfos.value
  const allLevels = error && warn && info
  const s = search.value.toLowerCase()
  const hasSearch = s.length > 0

  if (allLevels && !hasSearch) return lines.value

  return lines.value.filter((line) => {
    if (!allLevels) {
      const level = getLevel(line)
      if (level === 'ERROR' || level === 'FATAL') { if (!error) return false }
      else if (level === 'WARN') { if (!warn) return false }
      else if (level === 'INFO') { if (!info) return false }
    }
    if (hasSearch && !line.toLowerCase().includes(s)) return false
    return true
  })
})

onMounted(() => {
  load()
})
</script>

<style scoped>
.logfile-container {
  font-family: monospace;
  overflow-y: auto;
  overflow-x: auto;
  height: calc(100vh - 190px);
  background-color: #1e1e1e;
  padding: 8px;
}
.logfile-line {
  line-height: 1.5;
  color: #d4d4d4;
}
.logfile-line-wrap {
  white-space: pre-wrap;
  word-break: break-all;
}
.logfile-line-nowrap {
  white-space: pre;
}
.logfile-line.ErrWarning {
  color: orange;
}
.logfile-line.ErrAlarm {
  color: red;
}
</style>
