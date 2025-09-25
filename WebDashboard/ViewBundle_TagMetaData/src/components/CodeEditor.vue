<template>
  <div>
    <div
      ref="rootEditor"
      :style="getStyle()"
    ></div>
    <v-toolbar
      density="compact"
      flat
    >
      <v-btn
        size="small"
        variant="text"
        @click="decreaseFontSize"
      >
        <v-icon>mdi-minus</v-icon>
      </v-btn>
      <v-btn
        size="small"
        variant="text"
        @click="increaseFontSize"
      >
        <v-icon>mdi-plus</v-icon>
      </v-btn>
      <v-btn
        size="small"
        variant="text"
        @click="editor.execCommand('find')"
      >
        <v-icon>mdi-magnify</v-icon>
      </v-btn>
      <v-btn
        size="small"
        variant="text"
        @click="editor.execCommand('replace')"
      >
        <v-icon>mdi-find-replace</v-icon>
      </v-btn>
      <v-btn
        :disabled="undoDisabled"
        size="small"
        variant="text"
        @click="editor.execCommand('undo')"
      >
        <v-icon>mdi-undo</v-icon>
      </v-btn>
      <v-btn
        :disabled="redoDisabled"
        size="small"
        variant="text"
        @click="editor.execCommand('redo')"
      >
        <svg
          height="24"
          viewBox="0 0 24 24"
          width="24"
          xmlns="http://www.w3.org/2000/svg"
        >
          <path
            d="M18.4 10.6C16.55 9 14.15 8 11.5 8c-4.65 0-8.58 3.03-9.96 7.22L3.9 16a8.002 8.002 0 0 1 7.6-5.5c1.95 0 3.73.72 5.12 1.88L13 16h9V7z"
            fill="currentColor"
          />
        </svg>
      </v-btn>
      <v-select
        v-model="theme"
        class="ml-3"
        :items="themes"
        style="max-width: 130px"
      ></v-select>
      <v-btn
        size="small"
        variant="text"
        @click="showKeyBindings"
      >
        <v-icon>mdi-help-circle-outline</v-icon>
      </v-btn>
    </v-toolbar>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onBeforeUnmount } from 'vue'
import ace from 'ace-builds/src-noconflict/ace'
import 'ace-builds/src-noconflict/mode-csharp'
import 'ace-builds/src-noconflict/mode-python'
import 'ace-builds/src-noconflict/theme-textmate'
import 'ace-builds/src-noconflict/theme-chrome'
import 'ace-builds/src-noconflict/theme-github'
import 'ace-builds/src-noconflict/theme-twilight'
import 'ace-builds/src-noconflict/theme-github_dark'
import 'ace-builds/src-noconflict/theme-monokai'
import 'ace-builds/src-noconflict/theme-terminal'
import 'ace-builds/src-noconflict/theme-xcode'
import 'ace-builds/src-noconflict/ext-language_tools'
import 'ace-builds/src-noconflict/ext-searchbox'
import 'ace-builds/src-noconflict/ext-keybinding_menu'

const value = defineModel<string>({ required: true })

const props = defineProps<{
  lang: string
  height: string
  width: string
}>()

const rootEditor = ref<HTMLElement | null>(null)
const editor = ref<any>(null)
const themes = ref(['textmate', 'xcode', 'chrome', 'github', 'github_dark', 'monokai', 'twilight', 'terminal'])
const theme = ref('textmate')
const contentBackup = ref('')
const undoDisabled = ref(true)
const redoDisabled = ref(true)
const codeEditorProperties = ref({
  fontSize: 14,
  theme: 'textmate',
})

watch(
  () => value.value,
  (val: string) => {
    if (contentBackup.value !== val) {
      editor.value.session.setValue(val, 1)
      contentBackup.value = val
    }
  },
)

watch(theme, (newTheme: string) => {
  codeEditorProperties.value.theme = newTheme
  editor.value.setTheme('ace/theme/' + newTheme)
  saveProperties()
})

watch(
  () => props.lang,
  (newLang: string) => {
    editor.value.getSession().setMode(typeof newLang === 'string' ? 'ace/mode/' + newLang : newLang)
  },
)

watch(
  () => props.height,
  () => {
    editor.value.resize()
  },
)

watch(
  () => props.width,
  () => {
    editor.value.resize()
  },
)

onMounted(() => {
  const lang = props.lang || 'text'
  editor.value = ace.edit(rootEditor.value, {
    value: value.value,
  })
  editor.value.setOptions({
    enableBasicAutocompletion: true,
  })
  editor.value.$blockScrolling = Infinity

  const savedProperties = localStorage.getItem('codeEditorProperties')
  if (savedProperties) {
    codeEditorProperties.value = JSON.parse(savedProperties)
    theme.value = codeEditorProperties.value.theme
  }

  editor.value.setFontSize(codeEditorProperties.value.fontSize)
  editor.value.getSession().setMode(typeof lang === 'string' ? 'ace/mode/' + lang : lang)
  editor.value.setTheme('ace/theme/' + codeEditorProperties.value.theme)

  contentBackup.value = value.value

  editor.value.on('change', () => {
    const content = editor.value.getValue()
    value.value = content
    contentBackup.value = content
    undoDisabled.value = !editor.value.session.getUndoManager().hasUndo()
    redoDisabled.value = !editor.value.session.getUndoManager().hasRedo()
  })
})

onBeforeUnmount(() => {
  editor.value.destroy()
  editor.value.container.remove()
})

const px = (n: string): string => {
  if (/^\d*$/.test(n)) {
    return n + 'px'
  }
  return n
}

const getStyle = (): Record<string, string> => ({
  height: props.height ? px(props.height) : '100%',
  width: props.width ? px(props.width) : '100%',
  marginTop: '8px',
})

const increaseFontSize = (): void => {
  const newSize = codeEditorProperties.value.fontSize + 1
  editor.value.setFontSize(newSize)
  codeEditorProperties.value.fontSize = newSize
  saveProperties()
}

const decreaseFontSize = (): void => {
  const newSize = codeEditorProperties.value.fontSize - 1
  editor.value.setFontSize(newSize)
  codeEditorProperties.value.fontSize = newSize
  saveProperties()
}

const showKeyBindings = (): void => {
  ace.config.loadModule('ace/ext/keybinding_menu', (module: any) => {
    module.init(editor.value)
    editor.value.showKeyboardShortcuts()
  })
}

const saveProperties = (): void => {
  localStorage.setItem('codeEditorProperties', JSON.stringify(codeEditorProperties.value))
}
</script>

<style scoped>
/* Your styles here */
</style>
