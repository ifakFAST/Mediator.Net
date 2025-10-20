<template>
  <v-container style="max-width: 98%; padding: 0px 0px !important">
    <v-row
      v-for="(row, i) in pageRows"
      :key="i"
      dense
      :style="styleRow"
    >
      <v-col
        v-for="(column, j) in row.Columns"
        :key="j"
        :cols="columnWidthMapping(column.Width)"
        :style="getStyleCol(j)"
      >
        <div
          v-if="editPage"
          style="display: flex"
        >
          <v-spacer></v-spacer>

          <v-menu location="bottom">
            <template #activator="{ props }">
              <v-btn
                v-bind="props"
                color="primary"
                style="padding-left: 0px; padding-right: 0px"
                variant="text"
              >
                {{ 'Row ' + (i + 1) }}</v-btn
              >
            </template>
            <v-list>
              <v-list-item @click="onInsertRow(i, false)">
                <v-list-item-title>Insert New Row Above</v-list-item-title>
              </v-list-item>
              <v-list-item @click="onInsertRow(i, true)">
                <v-list-item-title>Insert New Row Below</v-list-item-title>
              </v-list-item>
              <v-list-item
                v-if="isEnabledRowMoveUp(i)"
                @click="onMoveRow(i, false)"
              >
                <v-list-item-title>Move Up</v-list-item-title>
              </v-list-item>
              <v-list-item
                v-if="isEnabledRowMoveDown(i)"
                @click="onMoveRow(i, true)"
              >
                <v-list-item-title>Move Down</v-list-item-title>
              </v-list-item>
              <v-list-item
                v-if="isEnabledRowRemove(i)"
                @click="onRemoveRow(i)"
              >
                <v-list-item-title>Remove</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-menu>

          <v-menu location="bottom">
            <template #activator="{ props }">
              <v-btn
                v-bind="props"
                color="primary"
                style="padding-left: 0px; padding-right: 0px"
                variant="text"
              >
                {{ 'Col ' + (j + 1) }}</v-btn
              >
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
              <v-list-item
                v-if="isEnabledColumnMoveLeft(i, j)"
                @click="onMoveCol(i, j, false)"
              >
                <v-list-item-title>Move Left</v-list-item-title>
              </v-list-item>
              <v-list-item
                v-if="isEnabledColumnMoveRight(i, j)"
                @click="onMoveCol(i, j, true)"
              >
                <v-list-item-title>Move Right</v-list-item-title>
              </v-list-item>
              <v-list-item
                v-if="isEnabledColumnRemove(i, j)"
                @click="onRemoveCol(i, j)"
              >
                <v-list-item-title>Remove</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-menu>

          <v-spacer></v-spacer>
        </div>

        <div
          v-for="(widget, k) in column.Widgets"
          :key="pageID + '____' + widget.ID"
        >
          <p
            v-if="k !== 0"
            style="margin-top: 8px; margin-bottom: 0px"
          ></p>

          <div
            v-if="editPage"
            style="display: flex"
          >
            <v-spacer></v-spacer>

            <v-menu location="bottom">
              <template #activator="{ props }">
                <v-btn
                  v-bind="props"
                  color="primary"
                  style="padding-left: 0px; padding-right: 0px"
                  variant="text"
                >
                  {{ 'Widget ' + (k + 1) }}</v-btn
                >
              </template>

              <v-list>
                <v-list-item
                  v-if="editPage"
                  @click="onContextWidgetSetTitle(i, j, k)"
                >
                  <v-list-item-title>Widget Set Title</v-list-item-title>
                </v-list-item>
                <v-list-item
                  v-if="editPage"
                  @click="onContextWidgetSetWidth(i, j, k)"
                >
                  <v-list-item-title>Widget Set Width</v-list-item-title>
                </v-list-item>
                <v-list-item
                  v-if="editPage"
                  @click="onContextWidgetSetHeight(i, j, k)"
                >
                  <v-list-item-title>Widget Set Height</v-list-item-title>
                </v-list-item>
                <v-list-item
                  v-if="editPage"
                  @click="onContextWidgetSetPadding(i, j, k)"
                >
                  <v-list-item-title>Widget Set Padding</v-list-item-title>
                </v-list-item>
                <v-list-item
                  v-if="editPage && isEnabledWidgetMoveUp(i, j, k)"
                  @click="onContextWidgetMove(i, j, k, false)"
                >
                  <v-list-item-title>Widget Move Up</v-list-item-title>
                </v-list-item>
                <v-list-item
                  v-if="editPage && isEnabledWidgetMoveDown(i, j, k)"
                  @click="onContextWidgetMove(i, j, k, true)"
                >
                  <v-list-item-title>Widget Move Down</v-list-item-title>
                </v-list-item>
                <v-list-item
                  v-if="editPage"
                  @click="onContextWidgetMoveToPosition(i, j, k)"
                >
                  <v-list-item-title>Widget Move To Position...</v-list-item-title>
                </v-list-item>
                <v-list-item
                  v-if="editPage"
                  @click="onContextWidgetCopyToPosition(i, j, k)"
                >
                  <v-list-item-title>Widget Copy To Position...</v-list-item-title>
                </v-list-item>
                <v-list-item
                  v-if="editPage"
                  @click="onContextWidgetDelete(i, j, k)"
                >
                  <v-list-item-title>Widget Remove</v-list-item-title>
                </v-list-item>
              </v-list>
            </v-menu>

            <v-spacer></v-spacer>
          </div>

          <widget-wrapper
            :id="widget.ID"
            :backend-async="makeBackendAsync(widget.ID)"
            :config="widget.Config || {}"
            :config-variables="configVariables"
            :date-window="dateWindow"
            :event-name="widget.EventName || ''"
            :event-payload="widget.EventPayload || {}"
            :height="widget.Height || ''"
            :padding-override="widget.PaddingOverride || ''"
            :resize="resize"
            :set-config-variable-values="setConfigVariableValues"
            :time-range="timeRange"
            :title="widget.Title || ''"
            :type="widget.Type"
            :width="widget.Width || ''"
            @date-window-changed="onDateWindowChanged"
          ></widget-wrapper>
        </div>
      </v-col>
    </v-row>

    <confirm ref="confirm"></confirm>
    <dlg-set-col-width ref="setColWidth"></dlg-set-col-width>
    <dlg-widget-type ref="selectWidgetType"></dlg-widget-type>
    <dlg-text-input ref="textInput"></dlg-text-input>
    <dlg-move-widget ref="moveWidget"></dlg-move-widget>
  </v-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, type StyleValue } from 'vue'
import Confirm from '../components/Confirm.vue'
import DlgSetColWidth from './DlgSetColWidth.vue'
import DlgWidgetType from './DlgWidgetType.vue'
import DlgTextInput from './DlgTextInput.vue'
import DlgMoveWidget from './DlgMoveWidget.vue'
import WidgetWrapper from './WidgetWrapper.vue'
import * as model from './model'
import * as utils from '../utils'

const props = defineProps<{
  page: model.Page | null
  widgetTypes: string[]
  editPage: boolean
  dateWindow: number[] | null
  configVariables: model.ConfigVariableValues
  setConfigVariableValues: (variableValues: Record<string, string>) => void
}>()

const emit = defineEmits<{
  configchanged: [page: model.Page]
  'date-window-changed': [window: number[] | null]
}>()

const timeRange = ref<utils.TimeRange>({
  type: 'Last',
  lastCount: 1,
  lastUnit: 'Hours',
  rangeStart: '',
  rangeEnd: '',
})
const resize = ref(0)

const confirm = ref()
const setColWidth = ref()
const selectWidgetType = ref()
const textInput = ref()
const moveWidget = ref()

onMounted(() => {
  const dashboard = (window.parent as any).dashboardApp
  dashboard.registerResizeListener(() => {
    resize.value = new Date().getTime()
  })

  dashboard.showTimeRangeSelector(true)
  timeRange.value = dashboard.getCurrentTimeRange()

  dashboard.registerTimeRangeListener((timeRangeValue: any) => {
    timeRange.value = timeRangeValue
  })
})

watch(
  () => props.page,
  (newVal: model.Page | null, old: model.Page | null) => {
    const dashboard = (window.parent as any).dashboardApp
    const widgets = getAllWidgetIDs()
    dashboard.setEventBurstCount(Math.max(1, widgets.size))
    const anyHistoryPlotWidget =
      props.page === null ||
      props.page.Rows.some((row) => {
        return row.Columns.some((col) => {
          return col.Widgets.some((widget) => {
            return widget.Type === 'HistoryPlot' || widget.Type === 'xyPlot'
          })
        })
      })
    dashboard.showTimeRangeEndTimeOnly(!anyHistoryPlotWidget)
  },
)

const pageID = computed((): string => {
  if (props.page === null) {
    return ''
  }
  return props.page.ID
})

const pageRows = computed((): model.Row[] => {
  if (props.page === null) {
    return []
  }
  return props.page.Rows
})

const styleRow = computed((): StyleValue => {
  if (props.editPage) {
    return {
      'min-height': '90px',
      'background-color': 'grey',
      'padding-bottom': '3px',
    }
  } else {
    return {
      'min-height': '90px',
      'padding-bottom': '3px',
    }
  }
})

const getStyleCol = (j: number): StyleValue => {
  if (props.editPage) {
    return {
      'background-color': j % 2 === 1 ? '#e6e6e6' : '#F8F8F8',
    }
  } else {
    return {}
  }
}

const makeBackendAsync = (widgetID: string): ((request: string, parameter: object, responseType?: 'text' | 'blob') => Promise<any>) => {
  return (request: string, parameter: object, responseType?: 'text' | 'blob') => {
    const para = {
      pageID: pageID.value,
      widgetID,
      request,
      parameter,
    }
    return (window.parent as any).dashboardApp.sendViewRequestAsync('RequestFromWidget', para, responseType)
  }
}

const onContextWidgetDelete = async (row: number, col: number, widget: number): Promise<void> => {
  const page = props.page
  if (page === null) {
    return
  }

  if (!(await confirmDelete('Delete Widget?', 'Do you really want to delete the widget?'))) {
    return
  }

  const column = page.Rows[row].Columns[col]
  const widgetID = column.Widgets[widget].ID
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigWidgetDelete', { pageID: page.ID, widgetID }, onGotNewPage)
}

const onContextWidgetMove = (row: number, col: number, widget: number, down: boolean): void => {
  const page = props.page
  if (page === null) {
    return
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigWidgetMove', { pageID: page.ID, row, col, widget, down }, onGotNewPage)
}

const onContextWidgetMoveToPosition = async (sourceRow: number, sourceCol: number, widgetIndex: number): Promise<void> => {
  const page = props.page
  if (page === null) {
    return
  }

  const column = page.Rows[sourceRow].Columns[sourceCol]
  const widgetObj: model.Widget = column.Widgets[widgetIndex]

  const rowColOptions = getRowColumnOptions()

  const result = await moveWidget.value.open(`Move Widget in Row ${sourceRow + 1} Column ${sourceCol + 1} to...`, rowColOptions, sourceRow, sourceCol)

  if (result === null) {
    return
  }

  const { targetRow, targetCol } = result

  if (targetRow === sourceRow && targetCol === sourceCol) {
    return
  }

  const para = {
    pageID: page.ID,
    sourceRow,
    sourceCol,
    widgetIndex,
    targetRow,
    targetCol,
  }

  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigWidgetMoveToPosition', para, onGotNewPage)
}

const onContextWidgetCopyToPosition = async (sourceRow: number, sourceCol: number, widgetIndex: number): Promise<void> => {
  const page = props.page
  if (page === null) {
    return
  }

  const column = page.Rows[sourceRow].Columns[sourceCol]
  const widgetObj: model.Widget = column.Widgets[widgetIndex]

  const rowColOptions = getRowColumnOptions()

  const result = await moveWidget.value.open(`Copy Widget in Row ${sourceRow + 1} Column ${sourceCol + 1} to...`, rowColOptions, sourceRow, sourceCol)

  if (result === null) {
    return
  }

  const { targetRow, targetCol } = result

  if (targetRow === sourceRow && targetCol === sourceCol) {
    return
  }

  const para = {
    pageID: page.ID,
    sourceRow,
    sourceCol,
    widgetIndex,
    targetRow,
    targetCol,
    newWidgetID: utils.findUniqueID('W', 4, getAllWidgetIDs()),
  }

  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigWidgetCopyToPosition', para, onGotNewPage)
}

const getRowColumnOptions = (): { rows: { id: number; text: string }[]; columns: { [rowId: number]: { id: number; text: string }[] } } => {
  const page = props.page
  if (page === null) {
    return { rows: [], columns: {} }
  }

  const result = {
    rows: [] as { id: number; text: string }[],
    columns: {} as { [rowId: number]: { id: number; text: string }[] },
  }

  page.Rows.forEach((row, rowIndex) => {
    result.rows.push({
      id: rowIndex,
      text: `Row ${rowIndex + 1}`,
    })

    result.columns[rowIndex] = row.Columns.map((col, colIndex) => ({
      id: colIndex,
      text: `Column ${colIndex + 1}`,
    }))
  })

  return result
}

const onContextWidgetSetHeight = async (row: number, col: number, widget: number): Promise<void> => {
  const page = props.page
  if (page === null) {
    return
  }
  const column = page.Rows[row].Columns[col]
  const widgetObj: model.Widget = column.Widgets[widget]

  const msg = 'Define the new height of the widget, e.g. "auto", "100px".'
  const newHeight = await textInputDlg('Set Widget Height', msg, widgetObj.Height || '')
  if (newHeight === null) {
    return
  }
  const para = {
    pageID: page.ID,
    row,
    col,
    widget,
    newHeight,
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigWidgetSetHeight', para, onGotNewPage)
}

const onContextWidgetSetPadding = async (row: number, col: number, widget: number): Promise<void> => {
  const page = props.page
  if (page === null) {
    return
  }
  const column = page.Rows[row].Columns[col]
  const widgetObj: model.Widget = column.Widgets[widget]

  const msg = 'Define the new padding of the widget, e.g. "auto", "100px".'
  const newPadding = await textInputDlg('Set Widget Padding', msg, widgetObj.PaddingOverride || '')
  if (newPadding === null) {
    return
  }
  const para = {
    pageID: page.ID,
    row,
    col,
    widget,
    newPadding,
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigWidgetSetPadding', para, onGotNewPage)
}

const onContextWidgetSetTitle = async (row: number, col: number, widget: number): Promise<void> => {
  const page = props.page
  if (page === null) {
    return
  }
  const column = page.Rows[row].Columns[col]
  const widgetObj: model.Widget = column.Widgets[widget]

  const msg = 'Define the new title of the widget.'
  const newTitle = await textInputDlg('Set Widget Title', msg, widgetObj.Title || '')
  if (newTitle === null) {
    return
  }
  const para = {
    pageID: page.ID,
    row,
    col,
    widget,
    newTitle,
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigWidgetSetTitle', para, onGotNewPage)
}

const onContextWidgetSetWidth = async (row: number, col: number, widget: number): Promise<void> => {
  const page = props.page
  if (page === null) {
    return
  }
  const column = page.Rows[row].Columns[col]
  const widgetObj: model.Widget = column.Widgets[widget]

  const msg = 'Define the new width of the widget, e.g. "auto", "100px", "100%".'
  const newWidth = await textInputDlg('Set Widget Width', msg, widgetObj.Width || '')
  if (newWidth === null) {
    return
  }
  const para = {
    pageID: page.ID,
    row,
    col,
    widget,
    newWidth,
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigWidgetSetWidth', para, onGotNewPage)
}

const onWidgetAdd = async (row: number, col: number): Promise<void> => {
  const page = props.page
  if (page === null) {
    return
  }

  const type: string | null = await selectWidgetTypeDlg()
  if (type === null) {
    return
  }

  const para = {
    pageID: page.ID,
    row,
    col,
    type,
    id: utils.findUniqueID('W', 4, getAllWidgetIDs()),
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigWidgetAdd', para, onGotNewPage)
}

const getAllWidgetIDs = (): Set<string> => {
  const set = new Set<string>()
  const page = props.page
  if (page === null) {
    return set
  }
  for (const row of page.Rows) {
    for (const col of row.Columns) {
      for (const widget of col.Widgets) {
        set.add(widget.ID)
      }
    }
  }
  return set
}

const columnWidthMapping = (w: model.ColumnWidth): number | string | false => {
  switch (w) {
    case 'Fill':
      return false
    case 'Auto':
      return 'auto'
    case 'OneOfTwelve':
      return 1
    case 'TwoOfTwelve':
      return 2
    case 'ThreeOfTwelve':
      return 3
    case 'FourOfTwelve':
      return 4
    case 'FiveOfTwelve':
      return 5
    case 'SixOfTwelve':
      return 6
    case 'SevenOfTwelve':
      return 7
    case 'EightOfTwelve':
      return 8
    case 'NineOfTwelve':
      return 9
    case 'TenOfTwelve':
      return 10
    case 'ElevenOfTwelve':
      return 11
    case 'TwelveOfTwelve':
      return 12
  }
  return false
}

const isEnabledRowMoveUp = (row: number): boolean => {
  return row > 0
}

const isEnabledRowMoveDown = (row: number): boolean => {
  return row < pageRows.value.length - 1
}

const isEnabledRowRemove = (row: number): boolean => {
  return pageRows.value.length > 1
}

const isEnabledColumnMoveLeft = (row: number, col: number): boolean => {
  return col > 0
}

const isEnabledColumnMoveRight = (row: number, col: number): boolean => {
  const r = pageRows.value[row]
  return col < r.Columns.length - 1
}

const isEnabledColumnRemove = (row: number, col: number): boolean => {
  const r = pageRows.value[row]
  return r.Columns.length > 1
}

const isEnabledWidgetMoveUp = (row: number, col: number, widget: number): boolean => {
  return widget > 0
}

const isEnabledWidgetMoveDown = (row: number, col: number, widget: number): boolean => {
  if (pageRows.value.length === 0) {
    return false
  }
  const r = pageRows.value[row]
  const c = r.Columns[col]
  return widget < c.Widgets.length - 1
}

const onInsertRow = (row: number, below: boolean): void => {
  const page = props.page
  if (page === null) {
    return
  }
  const para = {
    pageID: page.ID,
    row,
    below,
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigInsertRow', para, onGotNewPage)
}

const onMoveRow = (row: number, down: boolean): void => {
  const page = props.page
  if (page === null) {
    return
  }
  const para = {
    pageID: page.ID,
    row,
    down,
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigMoveRow', para, onGotNewPage)
}

const onRemoveRow = async (row: number): Promise<void> => {
  const page = props.page
  if (page === null) {
    return
  }

  if (!(await confirmDelete('Delete Row?', `Do you really want to delete row ${row + 1}?`))) {
    return
  }

  const para = {
    pageID: page.ID,
    row,
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigRemoveRow', para, onGotNewPage)
}

const onInsertCol = (row: number, col: number, right: boolean): void => {
  const page = props.page
  if (page === null) {
    return
  }
  const para = {
    pageID: page.ID,
    row,
    col,
    right,
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigInsertCol', para, onGotNewPage)
}

const onMoveCol = (row: number, col: number, right: boolean): void => {
  const page = props.page
  if (page === null) {
    return
  }
  const para = {
    pageID: page.ID,
    row,
    col,
    right,
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigMoveCol', para, onGotNewPage)
}

const onRemoveCol = async (row: number, col: number): Promise<void> => {
  const page = props.page
  if (page === null) {
    return
  }

  if (!(await confirmDelete('Delete Column?', `Do you really want to delete column ${col + 1} in row ${row + 1}?`))) {
    return
  }

  const para = {
    pageID: page.ID,
    row,
    col,
  }
  ;(window.parent as any).dashboardApp.sendViewRequest('ConfigRemoveCol', para, onGotNewPage)
}

const onGotNewPage = (strResponse: string): void => {
  const response = JSON.parse(strResponse)
  const newPage: model.Page = response
  emit('configchanged', newPage)
}

const onDateWindowChanged = (window: number[] | null): void => {
  emit('date-window-changed', window)
}

const onSetColumnWidth = async (row: number, col: number): Promise<void> => {
  const page = props.page
  if (page === null) {
    return
  }
  const column = page.Rows[row].Columns[col]
  const newWidth = await setColWidthDlg(column.Width)
  if (newWidth !== column.Width) {
    const para = {
      pageID: page.ID,
      row,
      col,
      width: newWidth,
    }
    ;(window.parent as any).dashboardApp.sendViewRequest('ConfigSetColWidth', para, onGotNewPage)
  }
}

const setColWidthDlg = async (w: model.ColumnWidth): Promise<model.ColumnWidth> => {
  return setColWidth.value.open(w)
}

const selectWidgetTypeDlg = async (type?: string): Promise<string | null> => {
  return selectWidgetType.value.open(props.widgetTypes, type)
}

const confirmBox = async (title: string, message: string, color?: string): Promise<boolean> => {
  if (await confirm.value.open(title, message, { color, hasCancel: true })) {
    return true
  }
  return false
}

const confirmDelete = async (title: string, message: string): Promise<boolean> => {
  if (await confirm.value.open(title, message, { color: 'red', hasCancel: true })) {
    return true
  }
  return false
}

const textInputDlg = async (title: string, message: string, value: string): Promise<string | null> => {
  return textInput.value.openWithValidator(title, message, value, (x: string) => '')
}
</script>
