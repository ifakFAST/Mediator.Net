<template>
  <div>

    <div v-bind:style="{ height: theHeight }" class="MarkDownHTML" style="min-height: 30px;" @contextmenu="onContextMenu" v-html="theHtmlString"></div>

    <v-menu v-model="contextMenu.show" :position-x="contextMenu.clientX" :position-y="contextMenu.clientY" absolute offset-y>
      <v-list>
        <v-list-item @click="onConfigure" >
          <v-list-item-title>Configure...</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <v-dialog v-model="showConfigDialog" max-width="880px" persistent @keydown="(e) => { if (e.keyCode === 27) { showConfigDialog = false; }}" >
      <v-card>
        <v-card-title class="headline">Configure text</v-card-title>
        <v-card-text>          
          <v-container fluid>
            <v-row>
              <v-col style="max-width: 200px;">
                <v-select v-model="textMode" :items="['Markdown', 'HTML']" label="Text mode" hide-details  ></v-select>
              </v-col>
              <v-col>
                <v-checkbox v-model="previewText" label="Preview" hide-details ></v-checkbox>  
              </v-col>
            </v-row>
          </v-container>
          <v-textarea  ref="MyTextArea" class="MyTextArea" filled v-model="text" :rows="8" @keydown.tab="handleTab"></v-textarea>
          <div v-if="previewText" class="MarkDownHTML" v-html="theHtmlStringEdit"></div>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn color="grey darken-1"    text @click="showConfigDialog = false">Cancel</v-btn>
          <v-btn color="primary darken-1" text @click="saveConfig">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue } from 'vue-property-decorator'
import { TimeRange } from '../../utils'
import { marked } from 'marked';

interface Config {
  Text: string
  Mode: 'Markdown' | 'HTML'
}

@Component({
  components: {
  },
})
export default class TextDisplay extends Vue {

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
  text = ''
  previewText = false
  textMode: 'Markdown' | 'HTML' = 'Markdown'
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

  get theHtmlString(): string {

    if (this.config.Mode === 'HTML') {
      return this.text
    }

    return marked.parse(this.config.Text || '') as string
  }

  get theHtmlStringEdit(): string {

    if (this.textMode === 'HTML') {
      return this.text
    }

    return marked.parse(this.config.Text || '') as string
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
    this.text = this.config.Text || ''
    this.textMode = this.config.Mode || 'Markdown'
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
      text: this.text,
      mode: this.textMode,
    }
    try {
      await this.backendAsync('SaveConfig', para)
    } 
    catch (err) {
      alert(err.message)
    }
  }

  handleTab(e) {
    e.preventDefault();
    const start = e.target.selectionStart;
    const end = e.target.selectionEnd;
    this.text = this.text.substring(0, start) + "    " + this.text.substring(end);
    this.$nextTick(() => {
      e.target.selectionStart = e.target.selectionEnd = start + 4;
    });
  }
}

</script>

<style>

.MyTextArea {
  font-family: monospace;
  font-weight: 500;
}

.v-text-field__slot textarea {
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
