<template>
  <div>
    <div
      class="MarkDownHTML"
      style="min-height: 30px"
      :style="{ height: theHeight }"
      @contextmenu="onContextMenu"
      v-html="theHtmlString"
    ></div>

    <v-menu
      v-model="contextMenu.show"
      :target="[contextMenu.clientX, contextMenu.clientY]"
    >
      <v-list>
        <v-list-item @click="onConfigure">
          <v-list-item-title>Configure...</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <v-dialog
      v-model="showConfigDialog"
      max-width="880px"
      persistent
      @keydown="
        (e: any) => {
          if (e.keyCode === 27) {
            showConfigDialog = false
          }
        }
      "
    >
      <v-card>
        <v-card-title class="text-h5">Configure text</v-card-title>
        <v-card-text>
          <v-container fluid>
            <v-row>
              <v-col style="max-width: 200px">
                <v-select
                  v-model="textMode"
                  :items="['Markdown', 'HTML']"
                  label="Text mode"
                ></v-select>
              </v-col>
              <v-col>
                <v-checkbox
                  v-model="previewText"
                  label="Preview"
                ></v-checkbox>
              </v-col>
            </v-row>
          </v-container>
          <v-textarea
            ref="MyTextArea"
            v-model="text"
            class="MyTextArea"
            :rows="8"
            variant="filled"
            @keydown.tab="handleTab"
          ></v-textarea>
          <div
            v-if="previewText"
            class="MarkDownHTML"
            v-html="theHtmlStringEdit"
          ></div>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn
            color="grey-darken-1"
            variant="text"
            @click="showConfigDialog = false"
            >Cancel</v-btn
          >
          <v-btn
            color="primary-darken-1"
            variant="text"
            @click="saveConfig"
            >Save</v-btn
          >
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import type { TimeRange } from '../../utils'
import { marked } from 'marked'

interface Config {
  Text: string
  Mode: 'Markdown' | 'HTML'
}

const props = defineProps<{
  id: string
  width: string
  height: string
  config: Config
  backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  eventName: string
  eventPayload: object
  timeRange: TimeRange
  resize: number
  dateWindow: number[] | null
}>()

const showConfigDialog = ref(false)
const text = ref('')
const previewText = ref(false)
const textMode = ref<'Markdown' | 'HTML'>('Markdown')
const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})
const canUpdateConfig = ref(false)
const MyTextArea = ref()

onMounted(() => {
  canUpdateConfig.value = (window.parent as any).dashboardApp.canUpdateViewConfig()
})

const theHeight = computed((): string => {
  if (props.height.trim() === '') {
    return 'auto'
  }
  return props.height
})

const theHtmlString = computed((): string => {
  if (props.config.Mode === 'HTML') {
    return props.config.Text
  }
  return marked.parse(props.config.Text || '') as string
})

const theHtmlStringEdit = computed((): string => {
  if (textMode.value === 'HTML') {
    return text.value
  }
  return marked.parse(text.value || '') as string
})

const onContextMenu = async (e: any): Promise<void> => {
  if (canUpdateConfig.value) {
    e.preventDefault()
    e.stopPropagation()
    contextMenu.value.show = false
    contextMenu.value.clientX = e.clientX
    contextMenu.value.clientY = e.clientY

    await nextTick()
    contextMenu.value.show = true
  }
}

const onConfigure = (): void => {
  text.value = props.config.Text || ''
  textMode.value = props.config.Mode || 'Markdown'
  showConfigDialog.value = true

  setTimeout(() => {
    if (MyTextArea.value) {
      MyTextArea.value.focus()
    }
  }, 100)
}

const saveConfig = async (): Promise<void> => {
  showConfigDialog.value = false
  const para = {
    text: text.value,
    mode: textMode.value,
  }
  try {
    await props.backendAsync('SaveConfig', para)
  } catch (err: any) {
    alert(err.message)
  }
}

const handleTab = (e: any) => {
  e.preventDefault()
  const start = e.target.selectionStart
  const end = e.target.selectionEnd
  text.value = text.value.substring(0, start) + '    ' + text.value.substring(end)
  nextTick(() => {
    e.target.selectionStart = e.target.selectionEnd = start + 4
  })
}
</script>

<style>
.MyTextArea {
  font-family: monospace;
  font-weight: 500;
}

.v-field__input textarea {
  overflow-x: auto;
  white-space: pre;
}

.MarkDownHTML h1 {
  margin-top: 12px;
  margin-bottom: 12px;
}

.MarkDownHTML h2 {
  margin-top: 12px;
  margin-bottom: 12px;
}

.MarkDownHTML h3 {
  margin-top: 10px;
  margin-bottom: 10px;
}

.MarkDownHTML h4 {
  margin-top: 8px;
  margin-bottom: 8px;
}

.MarkDownHTML p {
  margin-top: 14px;
  margin-bottom: 14px;
}

.MarkDownHTML blockquote {
  margin-left: 20px;
  margin-right: 20px;
}

.MarkDownHTML ul,
.MarkDownHTML ol {
  padding-left: 30px;
  margin: 0.5em 0;
}

.MarkDownHTML table {
  border-collapse: collapse;
}

.MarkDownHTML table td,
.MarkDownHTML table th {
  padding: 1px 10px;
  border: 1px solid black;
}
</style>
