<template>
  <v-app>
    <v-main>

      <v-toolbar flat dense color="white" style="margin-top: 0px; margin-bottom: 14px;">
        <a v-bind:class="classObject(page.ID)"
            v-for="page in pages" :key="page.ID"
            @click="switchPage(page.ID)"> {{ page.Name }} </a>
        <v-spacer></v-spacer>
        <v-btn v-if="editPage" icon @click="editPage = false">
          <v-icon>check</v-icon>
        </v-btn>
        <v-menu v-if="canUpdateConfig" bottom left offset-y>
          <template v-slot:activator="{ on }">
            <v-btn v-on="on" icon><v-icon>more_vert</v-icon></v-btn>
          </template>
          <v-list>
            <v-list-item @click="page_Add">
              <v-list-item-title>Add new Page</v-list-item-title>
            </v-list-item>
            <v-list-item @click="page_Rename" v-if="hasPage">
              <v-list-item-title>Rename Page</v-list-item-title>
            </v-list-item>
            <v-list-item @click="page_Duplicate" v-if="hasPage">
              <v-list-item-title>Duplicate Page</v-list-item-title>
            </v-list-item>
            <v-list-item @click="page_MoveLeft" v-if="canPageMoveLeft">
              <v-list-item-title>Move Left</v-list-item-title>
            </v-list-item>
            <v-list-item @click="page_MoveRight" v-if="canPageMoveRight">
              <v-list-item-title>Move Right</v-list-item-title>
            </v-list-item>
            <v-list-item @click="page_Delete" v-if="hasPage">
              <v-list-item-title>Delete Page</v-list-item-title>
            </v-list-item>
            <v-list-item @click="editPage = !editPage" v-if="hasPage">
              <v-list-item-title>{{ editPage ? 'Fix Page Layout' : 'Edit Page Layout' }}</v-list-item-title>
            </v-list-item>
          </v-list>
        </v-menu>

      </v-toolbar>

      <page :page="currentPage" :dateWindow="dateWindow" @date-window-changed="onDateWindowChanged" :widgetTypes="widgetTypes" :editPage="editPage" @configchanged="onPageConfigChanged"></page>

      <confirm ref="confirm"></confirm>
      <dlg-text-input ref="textInput"></dlg-text-input>

    </v-main>
  </v-app>
</template>

<script lang="ts">

import { Component, Vue } from 'vue-property-decorator'
import DlgTextInput from './DlgTextInput.vue'
import Confirm from '../components/Confirm.vue'
import Page from './Page.vue'
import * as model from './model'
import * as utils from '../utils'

@Component({
  components: {
    Page,
    Confirm,
    DlgTextInput,
  },
})
export default class ViewPages extends Vue {

  pages: model.Page[] = []
  currentPageID = ''
  widgetTypes: string[] = []
  widgetMap = new Map<string, model.Widget>()
  editPage = false
  dateWindow: number[] = null
  canUpdateConfig = false

  mounted(): void {
    const context = this
    window.parent['dashboardApp'].registerViewEventListener( (eventName: string, eventPayload: any) => {
      if (eventName === 'WidgetEvent') {
        const pageID: string = eventPayload.PageID
        if (pageID === context.currentPageID) {
          const widgetID: string = eventPayload.WidgetID
          const event: string = eventPayload.EventName
          const content: object = eventPayload.Content
          const widget = context.widgetMap.get(widgetID)
          if (widget) {
            widget.EventName = event
            widget.EventPayload = content
          }
        }
      }
      else if (eventName === 'WidgetConfigChanged') {
        const pageID: string = eventPayload.PageID
        if (pageID === context.currentPageID) {
          const widgetID: string = eventPayload.WidgetID
          const config: object = eventPayload.Config
          const widget = context.widgetMap.get(widgetID)
          if (widget) {
            widget.Config = config
          }
        }
      }
    })
    this.canUpdateConfig = window.parent['dashboardApp'].canUpdateViewConfig()
    this.loadAllPages()
  }

  loadAllPages(): void {
    const context = this
    window.parent['dashboardApp'].sendViewRequest('Init', { }, (strResponse: string) => {
      const response = JSON.parse(strResponse)
      context.pages = response.configuration.Pages
      context.widgetTypes = response.widgetTypes
      context.currentPageID = (context.pages.length > 0) ? context.pages[0].ID : ''
      context.updateWidgetMap()
    })
  }

  switchPage(newPageID: string): void {
    const context = this
    window.parent['dashboardApp'].sendViewRequest('SwitchToPage', { pageID: newPageID }, (strResponse: string) => {
      context.currentPageID = newPageID
      context.updateWidgetMap()
    })
  }

  onPageConfigChanged(page: model.Page): void {
    const pageID = page.ID
    if (pageID !== this.currentPageID) { return }
    const i = this.pages.findIndex((p) => p.ID === pageID)
    if (i < 0) { return }
    Vue.set(this.pages, i, page)
    this.updateWidgetMap()
  }

  onDateWindowChanged(window: number[]): void {

    if (window === null && this.dateWindow === null) { return }
    if (window !== null && this.dateWindow !== null && window[0] === this.dateWindow[0] && window[1] === this.dateWindow[1] ) { return }

    console.info('ViewPages.onDateWindowChanged !!!')

    this.dateWindow = window
  }

  updateWidgetMap(): void {
    this.widgetMap.clear()
    const page = this.currentPage
    if (page === null) { return }
    for (const row of page.Rows) {
      for (const col of row.Columns) {
        for (const w of col.Widgets) {
          this.widgetMap.set(w.ID, w)
          Vue.set(w, 'EventName', '')
          Vue.set(w, 'EventPayload', {})
        }
      }
    }
  }

  classObject(pageID: string) {
    const sel = this.currentPageID === pageID
    return {
      selectedpage: sel,
      nonselectedpage: !sel,
    }
  }

  async page_Add(): Promise<void> {
    const title = await this.textInputDlg('New Page Name', 'Define the name of the new page', 'Page')
    if (title === null) { return }
    const para = {
      pageID: utils.findUniqueID('P', 4, this.getAllPageIDs()),
      title,
    }
    try {
      const pages: model.Page[] = await window.parent['dashboardApp'].sendViewRequestAsync('ConfigPageAdd', para)
      this.pages = pages
      this.editPage = true
      this.switchPage(para.pageID)
    }
    catch (err) {
      const exp = err as Error
      alert(exp.message)
    }
  }

  async page_Rename(): Promise<void> {
    const page = this.currentPage
    if (page === null) { return }
    const title = await this.textInputDlg('Rename Page', 'Define the new name of the page', page.Name)
    if (title === null) { return }
    const para = {
      pageID: page.ID,
      title,
    }
    try {
      await window.parent['dashboardApp'].sendViewRequestAsync('ConfigPageRename', para)
      page.Name = title
    }
    catch (err) {
      const exp = err as Error
      alert(exp.message)
    }    
  }

  async page_MoveLeft(): Promise<void> {
    const page = this.currentPage
    if (page === null) { return }
    const pageIdx = this.pages.findIndex((p) => p.ID === page.ID)
    if (pageIdx < 0) { return }

    try {
      await window.parent['dashboardApp'].sendViewRequestAsync('ConfigPageMoveLeft', { pageID: page.ID })

      const arr = this.pages
      const tmp = arr[pageIdx]
      Vue.set(arr, pageIdx, arr[pageIdx - 1])
      Vue.set(arr, pageIdx - 1, tmp)
    }
    catch (err) {
      const exp = err as Error
      alert(exp.message)
    }
  }

  async page_MoveRight(): Promise<void> {
    const page = this.currentPage
    if (page === null) { return }
    const pageIdx = this.pages.findIndex((p) => p.ID === page.ID)
    if (pageIdx < 0) { return }

    try {
      await window.parent['dashboardApp'].sendViewRequestAsync('ConfigPageMoveRight', { pageID: page.ID })

      const arr = this.pages
      const tmp = arr[pageIdx]
      Vue.set(arr, pageIdx, arr[pageIdx + 1])
      Vue.set(arr, pageIdx + 1, tmp)
    }
    catch (err) {
      const exp = err as Error
      alert(exp.message)
    }
  }

  async page_Duplicate(): Promise<void> {
    const page = this.currentPage
    if (page === null) { return }
    const para = {
      pageID: page.ID,
      newPageID: utils.findUniqueID('P', 4, this.getAllPageIDs()),
      title: 'Copy of ' + page.Name,
    }
    try {
      const pages: model.Page[] = await window.parent['dashboardApp'].sendViewRequestAsync('ConfigPageDuplicate', para)
      this.pages = pages
      this.switchPage(para.pageID)
    }
    catch (err) {
      const exp = err as Error
      alert(exp.message)
    }
  }

  async page_Delete(): Promise<void> {
    const page = this.currentPage
    if (page === null) { return }
    const pageIdx = this.pages.findIndex((p) => p.ID === page.ID)
    if (pageIdx < 0) { return }
    const pageCount = this.pages.length

    if (!await this.confirmDelete('Delete Page?', `Do you really want to delete the page ${page.Name}?`)) {
      return
    }

    try {
      
      await window.parent['dashboardApp'].sendViewRequestAsync('ConfigPageDelete', { pageID: page.ID })

      this.pages.splice(pageIdx, 1)

      let newPageID = ''
      if (pageCount === 1) {
        newPageID = ''
      }
      else if (pageIdx === pageCount - 1) {
        newPageID = this.pages[pageIdx - 1].ID
      }
      else {
        newPageID = this.pages[pageIdx].ID
      }
      this.switchPage(newPageID)
    }
    catch (err) {
      const exp = err as Error
      alert(exp.message)
    }
  }

  getAllPageIDs(): Set<string> {
    const set = new Set<string>()
    for (const page of this.pages) {
      set.add(page.ID)
    }
    return set
  }

  async textInputDlg(title: string, message: string, value: string): Promise<string | null> {
    const textInput = this.$refs.textInput as any
    return textInput.open(title, message, value)
  }

  get currentPage(): model.Page | null {
    const i = this.pages.findIndex((p) => p.ID === this.currentPageID)
    if (i < 0) { return null }
    return this.pages[i]
  }

  get hasPage(): boolean {
    return this.pages.length > 0
  }

  get canPageMoveLeft(): boolean {
    if (this.pages.length === 0) { return false }
    const i = this.pages.findIndex((p) => p.ID === this.currentPageID)
    return i > 0
  }

  get canPageMoveRight(): boolean {
    if (this.pages.length === 0) { return false }
    const i = this.pages.findIndex((p) => p.ID === this.currentPageID)
    return i < this.pages.length - 1
  }

  async confirmBox(title: string, message: string, color?: string): Promise<boolean> {
    const confirm = this.$refs.confirm as any
    if (await confirm.open(title, message, { color, hasCancel: true })) {
      return true
    }
    return false
  }

  async confirmDelete(title: string, message: string): Promise<boolean> {
    const confirm = this.$refs.confirm as any
    if (await confirm.open(title, message, { color: 'red', hasCancel: true })) {
      return true
    }
    return false
  }
}
</script>

<style>
  .selectedpage {
    font-weight: bold;
    color: black !important;
    margin: 12px !important;
  }

  .nonselectedpage {
    font-weight: normal;
    color: grey !important;
    margin: 12px !important;
  }

</style>
