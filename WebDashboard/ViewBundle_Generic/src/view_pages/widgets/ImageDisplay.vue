<template>
  <div @contextmenu="onContextMenu">

    <img :src="imgSrc" alt="" v-bind:style="{ height: theHeight }" 
         style="min-height: 30px; max-width: 100%; max-height: 100%; object-fit: contain;" >

    <v-menu v-model="contextMenu.show" :position-x="contextMenu.clientX" :position-y="contextMenu.clientY" absolute offset-y>
      <v-list>
        <v-list-item @click="onConfigureStaticImage" >
          <v-list-item-title>Set static image...</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue } from 'vue-property-decorator'
import { TimeRange } from '../../utils'

interface Config {
  ImgPath: string
  Mode: 'Static' | 'Dynamic'
}

@Component({
  components: {
  },
})
export default class ImageDisplay extends Vue {

  @Prop({ default() { return '' } }) id: string
  @Prop({ default() { return '' } }) width: string
  @Prop({ default() { return '' } }) height: string
  @Prop({ default() { return {} } }) config: Config
  @Prop() backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  @Prop({ default() { return '' } }) eventName: string
  @Prop({ default() { return {} } }) eventPayload: object
  @Prop({ default() { return {} } }) timeRange: TimeRange
  @Prop({ default() { return 0 } }) resize: number
  @Prop({ default() { return null } }) dateWindow: number[]

  showConfigDialog = false
  imageMode: 'Static' | 'Dynamic' = 'Static'
  contextMenu = {
    show: false,
    clientX: 0,
    clientY: 0,
  }
  canUpdateConfig = false

  mounted(): void {
    this.canUpdateConfig = window.parent['dashboardApp'].canUpdateViewConfig()
  }

  get theHeight(): string {
    if (this.height.trim() === '') { return 'auto' }
    return this.height
  }

  get imgSrc(): string {
    if (this.config.Mode === 'Static') {
      return this.config.ImgPath
    }
    return ''
  }

  onConfigureStaticImage(): void {
    const context = this
    const inputElement = document.createElement('input')
    inputElement.type = 'file'
    inputElement.accept = 'image/*'
    inputElement.onchange = () => {
      const curFiles = inputElement.files
      if (curFiles.length === 0) {
        return;
      }
      const file = curFiles[0]
      const reader = new FileReader()
      reader.onload = () => {
        const arrayBuffer = reader.result as ArrayBuffer
        const byteArray = new Uint8Array(arrayBuffer)
        context.sendStaticImage(file.name, Array.from(byteArray))
      }
      reader.readAsArrayBuffer(file)
    }
    inputElement.click()
  }

  async sendStaticImage(fileName: string, data: number[]): Promise<void> {
    const para = {
      fileName,
      data,
    }
    try {
      console.log('sendStaticImage', para)
      await this.backendAsync('SetStaticImage', para)
    } 
    catch (err) {
      alert(err.message)
    }
  }

  onContextMenu(e: any): void {
    if (this.canUpdateConfig) {
      e.preventDefault()
      e.stopPropagation()
      this.contextMenu.show = false
      this.contextMenu.clientX = e.clientX
      this.contextMenu.clientY = e.clientY
      const context = this
      this.$nextTick(() => {
        context.contextMenu.show = true
      })
    }
  }

  onConfigure(): void {
    this.showConfigDialog = true
    const context = this
    const doFocus = () => {
      const txt = context.$refs.MyTextArea as HTMLElement
      txt.focus()
    }
    setTimeout(doFocus, 100)
  }

  async saveConfig(): Promise<void> {
    this.showConfigDialog = false
    const para = {

    }
    try {
      await this.backendAsync('SaveConfig', para)
    } 
    catch (err) {
      alert(err.message)
    }
  }
}

</script>

<style>

</style>
