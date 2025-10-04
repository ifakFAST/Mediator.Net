<template>
  <div @contextmenu="onContextMenu">
    <img
      alt=""
      :src="imgSrc"
      style="min-height: 30px; max-width: 100%; max-height: 100%; object-fit: contain"
      :style="{ height: theHeight }"
    />

    <v-menu
      v-model="contextMenu.show"
      :target="[contextMenu.clientX, contextMenu.clientY]"
    >
      <v-list>
        <v-list-item @click="onConfigureStaticImage">
          <v-list-item-title>Set static image...</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import type { TimeRange } from '../../utils'

interface Config {
  ImgPath: string
  Mode: 'Static' | 'Dynamic'
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
const imageMode = ref<'Static' | 'Dynamic'>('Static')
const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})
const canUpdateConfig = ref(false)

onMounted(() => {
  canUpdateConfig.value = (window.parent as any).dashboardApp.canUpdateViewConfig()
})

const theHeight = computed((): string => {
  if (props.height.trim() === '') {
    return 'auto'
  }
  return props.height
})

const imgSrc = computed((): string => {
  if (props.config.Mode === 'Static') {
    return props.config.ImgPath
  }
  return ''
})

const onConfigureStaticImage = (): void => {
  const inputElement = document.createElement('input')
  inputElement.type = 'file'
  inputElement.accept = 'image/*'
  inputElement.onchange = () => {
    const curFiles = inputElement.files
    if (curFiles?.length === 0) {
      return
    }
    const file = curFiles?.[0]
    if (!file) return

    const reader = new FileReader()
    reader.onload = () => {
      const arrayBuffer = reader.result as ArrayBuffer
      const byteArray = new Uint8Array(arrayBuffer)
      sendStaticImage(file.name, Array.from(byteArray))
    }
    reader.readAsArrayBuffer(file)
  }
  inputElement.click()
}

const sendStaticImage = async (fileName: string, data: number[]): Promise<void> => {
  const para = {
    fileName,
    data,
  }
  try {
    console.log('sendStaticImage', para)
    await props.backendAsync('SetStaticImage', para)
  } catch (err: any) {
    alert(err.message)
  }
}

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
  showConfigDialog.value = true
}

const saveConfig = async (): Promise<void> => {
  showConfigDialog.value = false
  const para = {}
  try {
    await props.backendAsync('SaveConfig', para)
  } catch (err: any) {
    alert(err.message)
  }
}
</script>
