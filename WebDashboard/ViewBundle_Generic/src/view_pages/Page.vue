<template>

  <v-container style="max-width: 98%; padding: 0px 0px !important;">

    <v-row v-for="(row, i) in pageRows" :key="i" dense v-bind:style="styleRow">

      <v-col
        v-for="(column, j) in row.Columns" :key="j" v-bind:style="getStyleCol(j)"
        :cols="columnWidthMapping(column.Width)">

        <div v-if="editPage" style="display: flex;">

          <v-spacer></v-spacer>

          <v-menu offset-y>
            <template v-slot:activator="{ on, attrs }">
              <v-btn style="padding-left: 0px; padding-right: 0px;" color="primary" text v-bind="attrs" v-on="on"> {{ 'Row ' + (i+1) }}</v-btn>
            </template>
            <v-list>
              <v-list-item @click="onInsertRow(i, false)">
                <v-list-item-title>Insert New Row Above</v-list-item-title>
              </v-list-item>
              <v-list-item @click="onInsertRow(i, true)">
                <v-list-item-title>Insert New Row Below</v-list-item-title>
              </v-list-item>
              <v-list-item @click="onMoveRow(i, false)" v-if="isEnabledRowMoveUp(i)">
                <v-list-item-title>Move Up</v-list-item-title>
              </v-list-item>
              <v-list-item @click="onMoveRow(i, true)" v-if="isEnabledRowMoveDown(i)">
                <v-list-item-title>Move Down</v-list-item-title>
              </v-list-item>
              <v-list-item @click="onRemoveRow(i)" v-if="isEnabledRowRemove(i)">
                <v-list-item-title>Remove</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-menu>

          <v-menu offset-y>
            <template v-slot:activator="{ on, attrs }">
              <v-btn style="padding-left: 0px; padding-right: 0px;" color="primary" text v-bind="attrs" v-on="on"> {{ 'Col ' + (j+1) }}</v-btn>
            </template>
            <v-list>
              <v-list-item @click="onWidgetAdd(i, j)">
                <v-list-item-title>Add Widget...</v-list-item-title>
              </v-list-item>
              <v-list-item @click="onSetColumnWidth(i, j)">
                <v-list-item-title>Set Width...</v-list-item-title>
              </v-list-item>
              <v-list-item @click="onInsertCol(i, j, false)">
                <v-list-item-title>Insert New Column Left</v-list-item-title>
              </v-list-item>
              <v-list-item @click="onInsertCol(i, j, true)">
                <v-list-item-title>Insert New Column Right</v-list-item-title>
              </v-list-item>
              <v-list-item @click="onMoveCol(i, j, false)" v-if="isEnabledColumnMoveLeft(i, j)">
                <v-list-item-title>Move Left</v-list-item-title>
              </v-list-item>
              <v-list-item @click="onMoveCol(i, j, true)" v-if="isEnabledColumnMoveRight(i, j)">
                <v-list-item-title>Move Right</v-list-item-title>
              </v-list-item>
              <v-list-item @click="onRemoveCol(i, j)" v-if="isEnabledColumnRemove(i, j)">
                <v-list-item-title>Remove</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-menu>

          <v-spacer></v-spacer>

        </div>

        <div v-for="(widget, k) in column.Widgets" :key="pageID + '____' + widget.ID">

          <p v-if="k !== 0" style="margin-top: 8px;margin-bottom: 0px;"></p>

          <div v-if="editPage" style="display: flex;">

            <v-spacer></v-spacer>

            <v-menu offset-y>

              <template v-slot:activator="{ on, attrs }">
                <v-btn style="padding-left: 0px; padding-right: 0px;" color="primary" text v-bind="attrs" v-on="on"> {{ 'Widget ' + (k+1) }}</v-btn>
              </template>

              <v-list>
                <v-list-item @click="onContextWidgetSetTitle(i, j, k)" v-if="editPage">
                  <v-list-item-title>Widget Set Title</v-list-item-title>
                </v-list-item>
                <v-list-item @click="onContextWidgetSetWidth(i, j, k)" v-if="editPage">
                  <v-list-item-title>Widget Set Width</v-list-item-title>
                </v-list-item>
                <v-list-item @click="onContextWidgetSetHeight(i, j, k)" v-if="editPage">
                  <v-list-item-title>Widget Set Height</v-list-item-title>
                </v-list-item>
                <v-list-item @click="onContextWidgetMove(i, j, k, false)" v-if="editPage && isEnabledWidgetMoveUp(i, j, k)">
                  <v-list-item-title>Widget Move Up</v-list-item-title>
                </v-list-item>
                <v-list-item @click="onContextWidgetMove(i, j, k, true)" v-if="editPage && isEnabledWidgetMoveDown(i, j, k)">
                  <v-list-item-title>Widget Move Down</v-list-item-title>
                </v-list-item>
                <v-list-item @click="onContextWidgetDelete(i, j, k)" v-if="editPage">
                  <v-list-item-title>Widget Remove</v-list-item-title>
                </v-list-item>
              </v-list>

            </v-menu>

            <v-spacer></v-spacer>

          </div>

          <widget-wrapper
            :id="widget.ID"
            :type="widget.Type"
            :title="widget.Title || ''"
            :width="widget.Width || ''"
            :height="widget.Height || ''"
            :config="widget.Config || {}"
            :eventName="widget.EventName"
            :eventPayload="widget.EventPayload"
            :backendAsync="makeBackendAsync(widget.ID)"
            :timeRange="timeRange"
            :resize="resize"
            :dateWindow="dateWindow"
            :configVariables="configVariables"
            @date-window-changed="onDateWindowChanged"
          ></widget-wrapper>

        </div>

      </v-col>

    </v-row>

    <confirm ref="confirm"></confirm>
    <dlg-set-col-width ref="setColWidth"></dlg-set-col-width>
    <dlg-widget-type ref="selectWidgetType"></dlg-widget-type>
    <dlg-text-input ref="textInput"></dlg-text-input>

  </v-container>

</template>

<script lang="ts">

import { Component, Vue, Prop, Watch } from 'vue-property-decorator'
import Confirm from '../components/Confirm.vue'
import DlgSetColWidth from './DlgSetColWidth.vue'
import DlgWidgetType from './DlgWidgetType.vue'
import DlgTextInput from './DlgTextInput.vue'
import WidgetWrapper from './WidgetWrapper.vue'
import * as model from './model'
import * as utils from '../utils'

@Component({
  components: {
    Confirm,
    DlgSetColWidth,
    DlgTextInput,
    DlgWidgetType,
    WidgetWrapper,
  },
})
export default class Page extends Vue {

  @Prop({ default() { return null } }) page: model.Page | null
  @Prop({ default() { return [] } }) widgetTypes: string[]
  @Prop({ default() { return false } }) editPage: boolean
  @Prop({ default() { return null } }) dateWindow: number[]
  @Prop({ default() { return {} } }) configVariables: model.ConfigVariableValues

  timeRange = {}
  resize = 0

  mounted() {
    const context = this
    const dashboard = window.parent['dashboardApp']
    dashboard.registerResizeListener(() => {
      context.resize = new Date().getTime()
    })

    dashboard.showTimeRangeSelector(true)
    this.timeRange = dashboard.getCurrentTimeRange()

    dashboard.registerTimeRangeListener((timeRange) => {
      context.timeRange = timeRange
    })
  }

  @Watch('page')
  watch_page(newVal: model.Page | null, old: model.Page | null): void {
    const dashboard = window.parent['dashboardApp']
    const widgets = this.getAllWidgetIDs()
    dashboard.setEventBurstCount(Math.max(1, widgets.size))
  }

  get pageID(): string {
    if (this.page === null) { return '' }
    return this.page.ID
  }

  get pageRows(): model.Row[] {
    if (this.page === null) { return [] }
    return this.page.Rows
  }

  get styleRow(): object {
    if (this.editPage) {
      return {
        'min-height': '90px',
        'background-color': 'grey',
        'padding-bottom': '3px',
      }
    }
    else {
      return {
        'min-height': '90px',
        'padding-bottom': '3px',
      }
    }
  }

  getStyleCol(j: number): object {
    if (this.editPage) {
      return {
        'background-color': (j % 2 === 1 ? '#e6e6e6' : '#F8F8F8'),
      }
    }
    else {
      return {
      }
    }
  }

  makeBackendAsync(widgetID: string): (request: string, parameter: object, responseType?: 'text' | 'blob') => Promise<any> {
    return (request: string, parameter: object, responseType?: 'text' | 'blob') => {
      const para = {
        pageID: this.pageID,
        widgetID,
        request,
        parameter,
      }
      return window.parent['dashboardApp'].sendViewRequestAsync('RequestFromWidget', para, responseType)
    }
  }

  async onContextWidgetDelete(row: number, col: number, widget: number): Promise<void> {
    const page = this.page
    if (page === null) { return }

    if (!await this.confirmDelete('Delete Widget?', 'Do you really want to delete the widget?')) {
      return
    }

    const column = page.Rows[row].Columns[col]
    const widgetID = column.Widgets[widget].ID
    window.parent['dashboardApp'].sendViewRequest('ConfigWidgetDelete', { pageID: page.ID, widgetID }, this.onGotNewPage)
  }

  onContextWidgetMove(row: number, col: number, widget: number, down: boolean): void {
    const page = this.page
    if (page === null) { return }
    window.parent['dashboardApp'].sendViewRequest('ConfigWidgetMove', { pageID: page.ID, row, col, widget, down }, this.onGotNewPage)
  }

  async onContextWidgetSetHeight(row: number, col: number, widget: number): Promise<void> {
    const page = this.page
    if (page === null) { return }
    const column = page.Rows[row].Columns[col]
    const widgetObj: model.Widget = column.Widgets[widget]

    const msg = 'Define the new height of the widget, e.g. "auto", "100px".'
    const newHeight = await this.textInputDlg('Set Widget Height', msg, widgetObj.Height || '')
    if (newHeight === null) { return }
    const para = {
      pageID: page.ID,
      row,
      col,
      widget,
      newHeight,
    }
    window.parent['dashboardApp'].sendViewRequest('ConfigWidgetSetHeight', para, this.onGotNewPage)
  }

  async onContextWidgetSetTitle(row: number, col: number, widget: number): Promise<void> {
    const page = this.page
    if (page === null) { return }
    const column = page.Rows[row].Columns[col]
    const widgetObj: model.Widget = column.Widgets[widget]

    const msg = 'Define the new title of the widget.'
    const newTitle = await this.textInputDlg('Set Widget Title', msg, widgetObj.Title || '')
    if (newTitle === null) { return }
    const para = {
      pageID: page.ID,
      row,
      col,
      widget,
      newTitle,
    }
    window.parent['dashboardApp'].sendViewRequest('ConfigWidgetSetTitle', para, this.onGotNewPage)
  }


  async onContextWidgetSetWidth(row: number, col: number, widget: number): Promise<void> {
    const page = this.page
    if (page === null) { return }
    const column = page.Rows[row].Columns[col]
    const widgetObj: model.Widget = column.Widgets[widget]

    const msg = 'Define the new width of the widget, e.g. "auto", "100px", "100%".'
    const newWidth = await this.textInputDlg('Set Widget Width', msg, widgetObj.Width || '')
    if (newWidth === null) { return }
    const para = {
      pageID: page.ID,
      row,
      col,
      widget,
      newWidth,
    }
    window.parent['dashboardApp'].sendViewRequest('ConfigWidgetSetWidth', para, this.onGotNewPage)
  }

  async onWidgetAdd(row: number, col: number): Promise<void> {
    const page = this.page
    if (page === null) { return }

    const type: string | null = await this.selectWidgetTypeDlg()
    if (type === null) { return }

    const para = {
      pageID: page.ID,
      row,
      col,
      type,
      id: utils.findUniqueID('W', 4, this.getAllWidgetIDs()),
    }
    window.parent['dashboardApp'].sendViewRequest('ConfigWidgetAdd', para, this.onGotNewPage)
  }

  getAllWidgetIDs(): Set<string> {
    const set = new Set<string>()
    const page = this.page
    if (page === null) { return set }
    for (const row of page.Rows) {
      for (const col of row.Columns) {
        for (const widget of col.Widgets) {
          set.add(widget.ID)
        }
      }
    }
    return set
  }

  columnWidthMapping(w: model.ColumnWidth): number | string | false {
    switch (w) {
      case 'Fill': return false
      case 'Auto': return 'auto'
      case 'OneOfTwelve': return 1
      case 'TwoOfTwelve': return 2
      case 'ThreeOfTwelve': return 3
      case 'FourOfTwelve': return 4
      case 'FiveOfTwelve': return 5
      case 'SixOfTwelve': return 6
      case 'SevenOfTwelve': return 7
      case 'EightOfTwelve': return 8
      case 'NineOfTwelve': return 9
      case 'TenOfTwelve': return 10
      case 'ElevenOfTwelve': return 11
      case 'TwelveOfTwelve': return 12
    }
    return false
  }

  isEnabledRowMoveUp(row: number): boolean {
    return row > 0
  }

  isEnabledRowMoveDown(row: number): boolean {
    return row < this.pageRows.length - 1
  }

  isEnabledRowRemove(row: number): boolean {
    return this.pageRows.length > 1
  }

  isEnabledColumnMoveLeft(row: number, col: number): boolean {
    return col > 0
  }

  isEnabledColumnMoveRight(row: number, col: number): boolean {
    const r = this.pageRows[row]
    return col < r.Columns.length - 1
  }

  isEnabledColumnRemove(row: number, col: number): boolean {
    const r = this.pageRows[row]
    return r.Columns.length > 1
  }

  isEnabledWidgetMoveUp(row: number, col: number, widget: number): boolean {
    return widget > 0
  }

  isEnabledWidgetMoveDown(row: number, col: number, widget: number): boolean {
    if (this.pageRows.length === 0) { return false }
    const r = this.pageRows[row]
    const c = r.Columns[col]
    return widget < c.Widgets.length - 1
  }

  onInsertRow(row: number, below: boolean): void {
    const page = this.page
    if (page === null) { return }
    const para = {
      pageID: page.ID,
      row,
      below,
    }
    const context = this
    window.parent['dashboardApp'].sendViewRequest('ConfigInsertRow', para, this.onGotNewPage)
  }

  onMoveRow(row: number, down: boolean): void {
    const page = this.page
    if (page === null) { return }
    const para = {
      pageID: page.ID,
      row,
      down,
    }
    window.parent['dashboardApp'].sendViewRequest('ConfigMoveRow', para, this.onGotNewPage)
  }

  async onRemoveRow(row: number): Promise<void> {
    const page = this.page
    if (page === null) { return }

    if (!await this.confirmDelete('Delete Row?', `Do you really want to delete row ${row + 1}?`)) {
      return
    }

    const para = {
      pageID: page.ID,
      row,
    }
    window.parent['dashboardApp'].sendViewRequest('ConfigRemoveRow', para, this.onGotNewPage)
  }

  onInsertCol(row: number, col: number, right: boolean): void {
    const page = this.page
    if (page === null) { return }
    const para = {
      pageID: page.ID,
      row,
      col,
      right,
    }
    window.parent['dashboardApp'].sendViewRequest('ConfigInsertCol', para, this.onGotNewPage)
  }

  onMoveCol(row: number, col: number, right: boolean): void {
    const page = this.page
    if (page === null) { return }
    const para = {
      pageID: page.ID,
      row,
      col,
      right,
    }
    window.parent['dashboardApp'].sendViewRequest('ConfigMoveCol', para, this.onGotNewPage)
  }

  async onRemoveCol(row: number, col: number): Promise<void> {
    const page = this.page
    if (page === null) { return }

    if (!await this.confirmDelete('Delete Column?', `Do you really want to delete column ${col + 1} in row ${row + 1}?`)) {
      return
    }

    const para = {
      pageID: page.ID,
      row,
      col,
    }
    window.parent['dashboardApp'].sendViewRequest('ConfigRemoveCol', para, this.onGotNewPage)
  }

  onGotNewPage(strResponse: string): void {
    const response = JSON.parse(strResponse)
    const newPage: model.Page = response
    this.$emit('configchanged', newPage)
  }

  onDateWindowChanged(window: number[]): void {
    this.$emit('date-window-changed', window)
  }

  async onSetColumnWidth(row: number, col: number): Promise<void> {
    const page = this.page
    if (page === null) { return }
    const column = page.Rows[row].Columns[col]
    const newWidth = await this.setColWidthDlg(column.Width)
    if (newWidth !== column.Width) {
      const para = {
        pageID: page.ID,
        row,
        col,
        width: newWidth,
      }
      window.parent['dashboardApp'].sendViewRequest('ConfigSetColWidth', para, this.onGotNewPage)
    }
  }

  async setColWidthDlg(w: model.ColumnWidth): Promise<model.ColumnWidth> {
    const setColWidth = this.$refs.setColWidth as any
    return setColWidth.open(w)
  }

  async selectWidgetTypeDlg(type?: string): Promise<string | null> {
    const selectWidget = this.$refs.selectWidgetType as any
    return selectWidget.open(this.widgetTypes, type)
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

  async textInputDlg(title: string, message: string, value: string): Promise<string | null> {
    const textInput = this.$refs.textInput as DlgTextInput
    return textInput.openWithValidator(title, message, value, (x) => '')
  }
}
</script>
