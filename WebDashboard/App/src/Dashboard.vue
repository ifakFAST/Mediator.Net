<template>
  <v-app>
    <v-app-bar app>
      <v-app-bar-nav-icon @click.stop="drawer = !drawer"></v-app-bar-nav-icon>
      <v-btn
        icon
        @click.stop="miniVariant = !miniVariant"
      >
        <v-icon>{{ miniVariant ? 'mdi-chevron-right' : 'mdi-chevron-left' }}</v-icon>
      </v-btn>

      <div class="my-toolbar-title">{{ title }}</div>

      <v-progress-circular
        v-if="showSpinner"
        class="mx-5"
        indeterminate
        color="primary"
      ></v-progress-circular>

      <v-spacer></v-spacer>

      <v-alert
        v-if="connectionState > 0"
        class="mx-4 my-0"
        variant="outlined"
        icon="mdi-cloud-off"
        style="max-width: 250px"
        :color="connectionColor"
        density="compact"
      >
        {{ connectionText }}
      </v-alert>

      <v-menu
        v-if="showTime"
        offset-y
        :close-on-content-click="false"
        v-model="showTimeEdit"
      >
        <template v-slot:activator="{ props }">
          <v-btn
            v-bind="props"
            variant="text"
            color="primary"
            class="pl-1 pr-1"
            @click="timeRangePrepare"
            >{{ timeRangeString }}</v-btn
          >
        </template>
        <v-list>
          <v-list-item
            v-for="item in predefinedTimeRanges"
            :key="item.title"
            @click="predefinedTimeRangeSelected(item)"
          >
            <v-list-item-title>{{ item.title }}</v-list-item-title>
          </v-list-item>
          <v-list-item>
            <table>
              <tbody>
                <tr>
                  <td>
                    <v-text-field
                      style="width: 60px; margin-right: 12px"
                      type="number"
                      v-model.number="timeRangeEdit.lastCount"
                    ></v-text-field>
                  </td>
                  <td>
                    <v-select
                      style="width: 110px"
                      v-model="timeRangeEdit.lastUnit"
                      :items="['Minutes', 'Hours', 'Days', 'Weeks', 'Months', 'Years']"
                    ></v-select>
                  </td>
                  <td>
                    <v-btn
                      style="min-width: 40px; width: 40px; margin-right: 0px"
                      color="primary"
                      :disabled="!isValidTimeLast"
                      variant="text"
                      @click="timeRangeApply"
                    >
                      <v-icon>mdi-check</v-icon>
                    </v-btn>
                  </td>
                </tr>
              </tbody>
            </table>
          </v-list-item>
          <v-list-item @click="customTimeRangeSelected">
            <v-list-item-title>Custom range...</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>

      <v-btn
        variant="text"
        icon
        v-show="showRangeStepButtonLeft"
        @mousedown="onRangeStep(-1)"
        class="mr-n2"
      >
        <v-icon>mdi-chevron-left</v-icon>
      </v-btn>

      <v-btn
        variant="text"
        icon
        v-show="showRangeStepButtonRight"
        @mousedown="onRangeStep(+1)"
        class="ml-n2"
      >
        <v-icon>mdi-chevron-right</v-icon>
      </v-btn>

      <v-menu
        v-if="showTime"
        offset-y
        :close-on-content-click="false"
        v-model="showStepSizeMenu"
      >
        <template v-slot:activator="{ props }">
          <v-tooltip
            location="bottom"
            :open-delay="1000"
          >
            <template v-slot:activator="{ props: tooltipProps }">
              <v-btn
                variant="text"
                icon
                v-bind="{ ...tooltipProps, ...props }"
              >
                <v-icon>mdi-tune</v-icon>
              </v-btn>
            </template>
            <span>Step size...</span>
          </v-tooltip>
        </template>
        <v-list>
          <v-list-item
            @click="autoStepSizeSelected"
            :class="stepSize === 0 ? 'text-primary' : ''"
          >
            <v-list-item-title>Auto</v-list-item-title>
          </v-list-item>
          <v-list-item
            v-for="item in predefinedStepSizes"
            :key="item.title"
            @click="predefinedStepSizeSelected(item)"
            :class="stepSize === millisecondsFromCountAndUnit(item.count, item.unit) ? 'text-primary' : ''"
          >
            <v-list-item-title>{{ item.title }}</v-list-item-title>
          </v-list-item>
          <v-list-item>
            <table>
              <tbody>
                <tr>
                  <td>
                    <v-text-field
                      style="width: 50px; margin-right: 12px"
                      type="number"
                      v-model.number="stepSizeEdit.lastCount"
                    ></v-text-field>
                  </td>
                  <td>
                    <v-select
                      style="width: 95px"
                      v-model="stepSizeEdit.lastUnit"
                      :items="['Minutes', 'Hours', 'Days']"
                    ></v-select>
                  </td>
                  <td>
                    <v-btn
                      style="min-width: 40px; width: 40px; margin-right: 0px"
                      color="primary"
                      :disabled="!isValidStepSizeCount"
                      variant="text"
                      @click="stepSizeApply"
                    >
                      <v-icon>mdi-check</v-icon>
                    </v-btn>
                  </td>
                </tr>
              </tbody>
            </table>
          </v-list-item>
        </v-list>
      </v-menu>

      <v-dialog
        v-model="showCustomTimeRangeSelector"
        max-width="640px"
        @keydown="editKeydown"
      >
        <v-card>
          <v-card-text>
            <div style="display: flex; gap: 16px; flex-direction: column">
              <div style="display: flex; gap: 16px">
                <v-text-field
                  label="From Date"
                  v-model="customRangeStartDate"
                  style="width: 120px"
                ></v-text-field>
                <v-text-field
                  label="From Time"
                  v-model="customRangeStartTime"
                  style="width: 80px"
                ></v-text-field>
                <v-text-field
                  label="To Date"
                  v-model="customRangeEndDate"
                  style="width: 120px"
                ></v-text-field>
                <v-text-field
                  label="To Time"
                  v-model="customRangeEndTime"
                  style="width: 80px"
                ></v-text-field>
              </div>
              <div style="display: flex; gap: 16px">
                <v-date-picker
                  width="300"
                  v-model="customRangeStartDatePicker"
                  elevation="4"
                  hide-header
                ></v-date-picker>
                <v-date-picker
                  width="300"
                  v-model="customRangeEndDatePicker"
                  elevation="4"
                  hide-header
                ></v-date-picker>
              </div>
            </div>
          </v-card-text>
          <v-card-actions>
            <v-spacer></v-spacer>
            <v-btn
              color="primary"
              :disabled="!isValidTimeRange"
              variant="text"
              @click="timeRangeApply2"
              >Apply</v-btn
            >
          </v-card-actions>
        </v-card>
      </v-dialog>

      <v-tooltip
        location="bottom"
        :open-delay="1000"
      >
        <template v-slot:activator="{ props }">
          <v-btn
            variant="text"
            icon
            v-bind="props"
            @click="toggleTheme"
          >
            <v-icon>{{ theme.global.name.value === 'dark' ? 'mdi-weather-sunny' : 'mdi-weather-night' }}</v-icon>
          </v-btn>
        </template>
        <span>{{ theme.global.name.value === 'dark' ? 'Switch to light mode' : 'Switch to dark mode' }}</span>
      </v-tooltip>

      <v-tooltip
        location="bottom"
        :open-delay="1000"
      >
        <template v-slot:activator="{ props }">
          <v-chip
            class="mt-1 mb-1 ml-4 mr-4"
            variant="outlined"
            @click.stop="logout"
            v-bind="props"
          >
            <v-icon start>mdi-account-outline</v-icon>
            {{ user }}
          </v-chip>
        </template>
        <span>Logout...</span>
      </v-tooltip>
    </v-app-bar>

    <v-navigation-drawer
      v-model="drawer"
      app
      :rail="miniVariant"
      width="220"
    >
      <v-list
        nav
        density="compact"
        class="mt-4"
      >
        <v-list-item
          v-for="(view, i) in views"
          :key="view.viewID"
          :value="view.viewID"
          :active="currViewID === view.viewID"
          @click="activateView(view.viewID)"
          @contextmenu="(e: MouseEvent) => onContextMenuViewEntry(e, view, i)"
        >
          <template v-slot:prepend>
            <v-icon :color="iconColorFromView(view)">{{ iconFromView(view) }}</v-icon>
          </template>
          <v-list-item-title style="font-size: 15px">{{ view.viewName }}</v-list-item-title>
        </v-list-item>
      </v-list>

      <v-menu
        v-if="canUpdateViews"
        v-model="contextMenuViewEntry.show"
        :location="'bottom'"
        :style="{
          position: 'absolute',
          left: contextMenuViewEntry.clientX + 'px',
          top: contextMenuViewEntry.clientY + 'px',
        }"
      >
        <v-list>
          <v-list-item @click="onContextViewRename">
            <v-list-item-title>Rename</v-list-item-title>
          </v-list-item>
          <v-list-item @click="onContextViewDuplicate">
            <v-list-item-title>Duplicate</v-list-item-title>
          </v-list-item>
          <v-list-item
            @click="onContextViewDuplicateConvert"
            v-if="contextMenuViewEntry.view.viewType === 'HistoryPlots'"
          >
            <v-list-item-title>Duplicate Convert</v-list-item-title>
          </v-list-item>
          <v-list-item
            @click="onContextViewToggleHeader"
            v-if="contextMenuViewEntry.view.viewType === 'Pages'"
          >
            <v-list-item-title>Toggle Header</v-list-item-title>
          </v-list-item>
          <v-list-item
            v-if="contextMenuViewEntry.canMoveUp"
            @click="onContextViewMoveUp"
          >
            <v-list-item-title>Move Up</v-list-item-title>
          </v-list-item>
          <v-list-item
            v-if="contextMenuViewEntry.canMoveDown"
            @click="onContextViewMoveDown"
          >
            <v-list-item-title>Move Down</v-list-item-title>
          </v-list-item>
          <v-list-item @click="onContextViewDelete">
            <v-list-item-title>Delete</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>

      <v-dialog
        v-model="renameDlg.show"
        max-width="350"
        @keydown.esc="renameDlgCancel"
      >
        <v-card>
          <v-card-title>
            <span class="text-h6">Rename View</span>
          </v-card-title>
          <v-card-text>
            Enter the new name for the view.
            <v-text-field
              v-model="renameDlg.text"
              ref="editText"
            ></v-text-field>
          </v-card-text>
          <v-card-actions class="pt-0">
            <v-spacer></v-spacer>
            <v-btn
              color="grey-darken-1"
              variant="text"
              @click="renameDlgCancel"
              >Cancel</v-btn
            >
            <v-btn
              color="primary-darken-1"
              :disabled="renameDlg.text.length === 0"
              variant="text"
              @click="renameDlgOK"
              >OK</v-btn
            >
          </v-card-actions>
        </v-card>
      </v-dialog>

      <v-dialog
        v-model="confirmDlg.show"
        max-width="250"
        @keydown.esc="() => (confirmDlg.show = false)"
      >
        <v-card>
          <v-toolbar
            color="red"
            density="compact"
          >
            <v-toolbar-title class="text-white">{{ confirmDlg.title }}</v-toolbar-title>
          </v-toolbar>
          <v-card-text class="mt-3">{{ confirmDlg.message }}</v-card-text>
          <v-card-actions class="pt-0">
            <v-spacer></v-spacer>
            <v-btn
              color="grey-darken-1"
              variant="text"
              @click="() => (confirmDlg.show = false)"
              >Cancel</v-btn
            >
            <v-btn
              color="primary-darken-1"
              variant="text"
              @click="confirmDlg.onOK"
              >OK</v-btn
            >
          </v-card-actions>
        </v-card>
      </v-dialog>
    </v-navigation-drawer>

    <v-main>
      <component
        v-if="currentViewComponent !== null"
        :is="currentViewComponent"
        :key="currViewID"
      />
      <iframe
        v-else
        :src="currViewSrc"
        style="border: 0; width: 100%; height: 100%"
      ></iframe>
    </v-main>
  </v-app>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick, watch } from 'vue'
import { useTheme } from 'vuetify'
import type { View, TimeRange } from './global'
import globalState from './global'
import ViewVariables from './view_variables/ViewVariables.vue'
import ViewEventLog from './view_eventlog/ViewEventLog.vue'
import ViewGeneric from './view_generic/ViewGeneric.vue'
import ViewCalc from './view_calc/ViewCalc.vue'
import ViewPages from './view_pages/ViewPages.vue'
import ViewTagMetaData from './view_tagmetadata/ViewTagMetaData.vue'
import ViewPublish from './view_publish/ViewPublish.vue'

interface Props {
  currViewID: string
  currViewSrc: string
  user: string
  views: View[]
  canUpdateViews: boolean
  busy: boolean
  connectionState: number
  showTime: boolean
  showEndTimeOnly: boolean
  timeRangeSelected: TimeRange
}

const props = defineProps<Props>()

const theme = useTheme()

function toggleTheme() {
  const next = theme.global.name.value === 'light' ? 'dark' : 'light'
  theme.global.name.value = next
  localStorage.setItem('theme', next)
}

const emit = defineEmits<{
  (e: 'logout'): void
  (e: 'activateView', viewID: string): void
  (e: 'timechange', range: TimeRange): void
  (e: 'duplicateView', viewID: string): void
  (e: 'renameView', viewID: string, newName: string): void
  (e: 'duplicateConvertView', viewID: string): void
  (e: 'toggleHeader', viewID: string): void
  (e: 'moveUp', viewID: string): void
  (e: 'moveDown', viewID: string): void
  (e: 'delete', viewID: string): void
}>()

const OneSecond = 1000
const OneMinute = 60 * OneSecond
const OneHour = 60 * OneMinute
const OneDay = 24 * OneHour

const drawer = ref(true)
const miniVariant = ref(false)
const title = ref('Dashboard')
const showTimeEdit = ref(false)
const showStepSizeMenu = ref(false)
const customRangeStartDate = ref('')
const customRangeStartTime = ref('00:00')
const customRangeEndDate = ref('')
const customRangeEndTime = ref('00:00')
const customRangeStartDatePicker = ref<Date | null>(null)
const customRangeEndDatePicker = ref<Date | null>(null)
const timeRangeEdit = ref<TimeRange>({
  type: 'Last',
  lastCount: 7,
  lastUnit: 'Days',
  rangeStart: '',
  rangeEnd: '',
})
const stepSizeEdit = ref({
  lastCount: 7,
  lastUnit: 'Days',
})
const predefinedTimeRanges = [
  { title: 'Last 60 minutes', count: 60, unit: 'Minutes' },
  { title: 'Last 6 hours', count: 6, unit: 'Hours' },
  { title: 'Last 24 hours', count: 24, unit: 'Hours' },
  { title: 'Last 7 days', count: 7, unit: 'Days' },
  { title: 'Last 30 days', count: 30, unit: 'Days' },
  { title: 'Last 12 months', count: 12, unit: 'Months' },
]
const predefinedStepSizes = [
  { title: '5 minutes', count: 5, unit: 'Minutes' },
  { title: '15 minutes', count: 15, unit: 'Minutes' },
  { title: '1 hour', count: 1, unit: 'Hours' },
  { title: '1 day', count: 1, unit: 'Days' },
]
const showCustomTimeRangeSelector = ref(false)
const contextMenuViewEntry = ref({
  show: false,
  clientX: 0,
  clientY: 0,
  view: {} as View,
  canMoveUp: false,
  canMoveDown: false,
})
const renameDlg = ref({
  show: false,
  text: '',
})
const confirmDlg = ref({
  show: false,
  title: '',
  message: '',
  onOK: () => {},
})

const showSpinner = ref(false)
let busyTimer: ReturnType<typeof setTimeout> | null = null

watch(
  () => props.busy,
  (newBusy) => {
    if (newBusy) {
      // Start timer to show spinner after 500ms
      busyTimer = setTimeout(() => {
        showSpinner.value = true
      }, 500)
    } else {
      // Hide immediately when busy becomes false
      if (busyTimer) {
        clearTimeout(busyTimer)
        busyTimer = null
      }
      showSpinner.value = false
    }
  },
)

onMounted(() => {
  title.value = (window as any).TheDashboardHeader || 'Dashboard'
})

watch([miniVariant, drawer], () => {
  nextTick(() => {
    globalState.resizeListener()
  })
})

watch(customRangeStartDatePicker, (newDate) => {
  if (newDate) {
    const year = newDate.getFullYear()
    const month = String(newDate.getMonth() + 1).padStart(2, '0')
    const day = String(newDate.getDate()).padStart(2, '0')
    customRangeStartDate.value = `${year}-${month}-${day}`
  }
})

watch(customRangeEndDatePicker, (newDate) => {
  if (newDate) {
    const year = newDate.getFullYear()
    const month = String(newDate.getMonth() + 1).padStart(2, '0')
    const day = String(newDate.getDate()).padStart(2, '0')
    customRangeEndDate.value = `${year}-${month}-${day}`
  }
})

function logout() {
  emit('logout')
}

function activateView(viewID: string) {
  emit('activateView', viewID)
}

function timeRangePrepare() {
  Object.assign(timeRangeEdit.value, props.timeRangeSelected)
}

function timeRangeApply() {
  showTimeEdit.value = false
  timeRangeEdit.value.type = 'Last'
  const range = Object.assign({}, timeRangeEdit.value)
  emit('timechange', range)
}

function timeRangeApply2() {
  showCustomTimeRangeSelector.value = false
  timeRangeEdit.value.type = 'Range'
  timeRangeEdit.value.rangeStart = customRangeStartDate.value + 'T' + customRangeStartTime.value
  timeRangeEdit.value.rangeEnd = customRangeEndDate.value + 'T' + customRangeEndTime.value
  const range = Object.assign({}, timeRangeEdit.value)
  emit('timechange', range)
}

function predefinedTimeRangeSelected(timeRange: { title: string; count: number; unit: string }) {
  showTimeEdit.value = false
  timeRangeEdit.value.type = 'Last'
  timeRangeEdit.value.lastCount = timeRange.count
  timeRangeEdit.value.lastUnit = timeRange.unit
  const range = Object.assign({}, timeRangeEdit.value)
  emit('timechange', range)
}

function customTimeRangeSelected() {
  showTimeEdit.value = false
  showCustomTimeRangeSelector.value = true
  const now = new Date()
  const tomorrow = new Date(now.getTime() + OneDay)
  const todayAsStringWithoutTime = getDatePartOfISOString(date2LocalStr(now), '')
  const tomorrowAsStringWithoutTime = getDatePartOfISOString(date2LocalStr(tomorrow), '')
  customRangeStartDate.value = getDatePartOfISOString(timeRangeEdit.value.rangeStart, todayAsStringWithoutTime)
  customRangeStartTime.value = getTimePartOfISOString(timeRangeEdit.value.rangeStart, '00:00')
  customRangeEndDate.value = getDatePartOfISOString(timeRangeEdit.value.rangeEnd, tomorrowAsStringWithoutTime)
  customRangeEndTime.value = getTimePartOfISOString(timeRangeEdit.value.rangeEnd, '00:00')

  // Initialize date pickers
  customRangeStartDatePicker.value = new Date(customRangeStartDate.value)
  customRangeEndDatePicker.value = new Date(customRangeEndDate.value)
}

function stepSizeApply() {
  showStepSizeMenu.value = false
  globalState.diffStepSizeMS = millisecondsFromCountAndUnit(stepSizeEdit.value.lastCount, stepSizeEdit.value.lastUnit)
}

function predefinedStepSizeSelected(stepSize: { title: string; count: number; unit: string }) {
  showStepSizeMenu.value = false
  globalState.diffStepSizeMS = millisecondsFromCountAndUnit(stepSize.count, stepSize.unit)
}

function autoStepSizeSelected() {
  showStepSizeMenu.value = false
  globalState.diffStepSizeMS = 0
}

function millisecondsFromCountAndUnit(count: number, unit: string): number {
  switch (unit) {
    case 'Minutes':
      return count * OneMinute
    case 'Hours':
      return count * OneHour
    case 'Days':
      return count * OneDay
    case 'Weeks':
      return count * 7 * OneDay
    case 'Months':
      return count * 30 * OneDay
    case 'Years':
      return count * 365 * OneDay
  }
  return 0
}

function editKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    showCustomTimeRangeSelector.value = false
  }
}

function onRangeStep(count: number) {
  timeRangePrepare()

  if (timeRangeEdit.value.type === 'Last') {
    const dateNow = new Date()
    dateNow.setMilliseconds(0)
    dateNow.setSeconds(0)

    let addMilliseconds: number
    switch (timeRangeEdit.value.lastUnit) {
      case 'Minutes':
        addMilliseconds = OneMinute
        break
      default:
        dateNow.setUTCMinutes(0)
        addMilliseconds = OneHour
        break
    }

    const dateEnd = new Date(dateNow.getTime() + addMilliseconds)
    const dateStart = new Date(dateEnd.getTime() - millisecondsFromCountAndUnit(timeRangeEdit.value.lastCount, timeRangeEdit.value.lastUnit))
    timeRangeEdit.value.type = 'Range'
    timeRangeEdit.value.rangeStart = date2LocalStr(dateStart)
    timeRangeEdit.value.rangeEnd = date2LocalStr(dateEnd)
  }

  const dateStart = new Date(timeRangeEdit.value.rangeStart)
  const dateEnd = new Date(timeRangeEdit.value.rangeEnd)
  const diff = globalState.diffStepSizeMS === 0 ? dateEnd.getTime() - dateStart.getTime() : globalState.diffStepSizeMS

  const bothMidnightBeforeShift = isMidnight(timeRangeEdit.value.rangeStart) && isMidnight(timeRangeEdit.value.rangeEnd)

  if (isNaN(diff)) {
    return
  }

  const newDateStart = new Date(dateStart.getTime() + count * diff)
  const newDateEnd = new Date(dateEnd.getTime() + count * diff)

  timeRangeEdit.value.rangeStart = date2LocalStr(newDateStart)
  timeRangeEdit.value.rangeEnd = date2LocalStr(newDateEnd)

  if (bothMidnightBeforeShift && !isMidnight(timeRangeEdit.value.rangeStart)) {
    const minutes = time2Minutes(getTimePartOfISOString(timeRangeEdit.value.rangeStart, '00:00'))
    const offsetMilliseconds = (minutes < 12 * 60 ? -minutes : 24 * 60 - minutes) * OneMinute
    timeRangeEdit.value.rangeStart = date2LocalStr(new Date(newDateStart.getTime() + offsetMilliseconds))
  }

  if (bothMidnightBeforeShift && !isMidnight(timeRangeEdit.value.rangeEnd)) {
    const minutes = time2Minutes(getTimePartOfISOString(timeRangeEdit.value.rangeEnd, '00:00'))
    const offsetMilliseconds = (minutes < 12 * 60 ? -minutes : 24 * 60 - minutes) * OneMinute
    timeRangeEdit.value.rangeEnd = date2LocalStr(new Date(newDateEnd.getTime() + offsetMilliseconds))
  }

  const range = Object.assign({}, timeRangeEdit.value)
  emit('timechange', range)
}

function iconFromView(view: View): string {
  const icon = view.viewIcon
  if (icon === undefined || icon === '') return 'mdi-chart-bubble'
  return icon
}

function iconColorFromView(view: View): string {
  const color = view.viewIconColor
  if (color === undefined || color === '') return ''
  return color
}

function onContextMenuViewEntry(e: MouseEvent, view: View, viewIdx: number) {
  e.preventDefault()
  e.stopPropagation()
  contextMenuViewEntry.value.show = true
  contextMenuViewEntry.value.clientX = e.clientX
  contextMenuViewEntry.value.clientY = e.clientY
  contextMenuViewEntry.value.view = view
  contextMenuViewEntry.value.canMoveUp = viewIdx > 0
  contextMenuViewEntry.value.canMoveDown = viewIdx < props.views.length - 1
}

function onContextViewRename() {
  renameDlg.value.show = true
  renameDlg.value.text = contextMenuViewEntry.value.view.viewName
}

function renameDlgOK() {
  renameDlg.value.show = false
  emit('renameView', contextMenuViewEntry.value.view.viewID, renameDlg.value.text)
}

function renameDlgCancel() {
  renameDlg.value.show = false
}

function onContextViewDuplicate() {
  emit('duplicateView', contextMenuViewEntry.value.view.viewID)
}

function onContextViewDuplicateConvert() {
  emit('duplicateConvertView', contextMenuViewEntry.value.view.viewID)
}

function onContextViewToggleHeader() {
  emit('toggleHeader', contextMenuViewEntry.value.view.viewID)
}

function onContextViewMoveUp() {
  emit('moveUp', contextMenuViewEntry.value.view.viewID)
}

function onContextViewMoveDown() {
  emit('moveDown', contextMenuViewEntry.value.view.viewID)
}

function onContextViewDelete() {
  confirmDlg.value.show = true
  confirmDlg.value.title = 'Delete view?'
  confirmDlg.value.message = `Do you really want to delete view '${contextMenuViewEntry.value.view.viewName}'?`
  confirmDlg.value.onOK = onViewDeleteOK
}

function onViewDeleteOK() {
  confirmDlg.value.show = false
  emit('delete', contextMenuViewEntry.value.view.viewID)
}

// Helper functions
function time2Minutes(time: string): number {
  const bits = time.split(':')
  return parseInt(bits[0]) * 60 + parseInt(bits[1])
}

function isMidnight(dateString: string): boolean {
  try {
    const time = getTimePartOfISOString(dateString, '')
    return time === '00:00' || time === '00:00:00'
  } catch (e) {
    return false
  }
}

function getDatePartOfISOString(s: string, defaultDate: string): string {
  if (s.length < 10) {
    return defaultDate
  }
  return s.substring(0, 10)
}

function getTimePartOfISOString(s: string, defaultTime: string): string {
  if (s.length < 16) {
    return defaultTime
  }
  return s.substring(11)
}

function date2LocalStr(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  const hours = String(date.getHours()).padStart(2, '0')
  const minutes = String(date.getMinutes()).padStart(2, '0')
  const seconds = String(date.getSeconds()).padStart(2, '0')
  if (seconds !== '00') {
    return `${year}-${month}-${day}T${hours}:${minutes}:${seconds}`
  }
  return `${year}-${month}-${day}T${hours}:${minutes}`
}

const timeRangeString = computed(() => {
  if (props.timeRangeSelected.type === 'Last') {
    if (props.showEndTimeOnly) {
      return 'Now'
    }
    return `Last ${props.timeRangeSelected.lastCount} ${props.timeRangeSelected.lastUnit}`
  }
  if (props.timeRangeSelected.type === 'Range') {
    const s = props.timeRangeSelected.rangeStart
    const e = props.timeRangeSelected.rangeEnd
    const dateSeparator = ' – '

    if (props.showEndTimeOnly) {
      return e.replace('T', ' ')
    }

    const dateStartStr = getDatePartOfISOString(s, '')
    const dateEndStr = getDatePartOfISOString(e, '')

    if (isMidnight(s) && isMidnight(e)) {
      const dateEnd = new Date(e)
      const dateEndMinus1Day = new Date(dateEnd.getTime() - OneDay)
      const dateEndMinusOneDayStr = getDatePartOfISOString(date2LocalStr(dateEndMinus1Day), '')

      if (dateStartStr === dateEndMinusOneDayStr) {
        return dateStartStr
      }

      return dateStartStr + dateSeparator + dateEndMinusOneDayStr
    }

    if (dateStartStr === dateEndStr) {
      const timeEndStr = getTimePartOfISOString(e, '')
      if (timeEndStr !== '') {
        return s.replace('T', ' ') + dateSeparator + timeEndStr
      }
    }

    return s.replace('T', ' ') + dateSeparator + e.replace('T', ' ')
  }
  return ''
})

const stepSize = computed(() => globalState.diffStepSizeMS)

const showRangeStepButtonLeft = computed(() => props.showTime)
const showRangeStepButtonRight = computed(() => props.showTime)

const isValidTimeRange = computed(() => {
  const s = customRangeStartDate.value + 'T' + customRangeStartTime.value
  const e = customRangeEndDate.value + 'T' + customRangeEndTime.value
  try {
    const sDate = new Date(s)
    const eDate = new Date(e)
    return sDate < eDate
  } catch (e) {
    return false
  }
})

const isValidTimeLast = computed(() => {
  const num = timeRangeEdit.value.lastCount
  return Number.isInteger(num) && num > 0
})

const isValidStepSizeCount = computed(() => {
  const num = stepSizeEdit.value.lastCount
  return Number.isInteger(num) && num > 0
})

const connectionColor = computed(() => {
  if (props.connectionState === 1) {
    return 'warning'
  }
  return 'error'
})

const connectionText = computed(() => {
  if (props.connectionState === 1) {
    return 'Trying to reconnect...'
  }
  return 'No Connection!'
})

const currentView = computed(() => {
  return props.views.find((v) => v.viewID === props.currViewID)
})

const viewComponentMap: Record<string, any> = {
  ModuleVariables: ViewVariables,
  EventLog: ViewEventLog,
  GenericModuleConfig: ViewGeneric,
  Calc: ViewCalc,
  Publish: ViewPublish,
  Pages: ViewPages,
  TagMetaData: ViewTagMetaData,
}

const currentViewComponent = computed(() => {
  const view = currentView.value
  if (!view) return null
  return viewComponentMap[view.viewType] || null
})
</script>

<style scoped>
.my-toolbar-title {
  font-size: 1.25rem;
  font-weight: 400;
  letter-spacing: 0;
  line-height: 1.75rem;
  text-transform: none;
  margin-left: 15px;
  margin-right: 15px;
}
</style>
