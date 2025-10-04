import { reactive } from 'vue'

export interface TimeRange {
  type: 'Last' | 'Range'
  lastCount: number
  lastUnit: string
  rangeStart: string
  rangeEnd: string
}

export interface View {
  viewID: string
  viewName: string
  viewIcon?: string
  viewIconColor?: string
  viewURL: string
  viewType: string
}

export interface Model {
  views: View[]
}

export interface GlobalState {
  sessionID: string
  user: string
  canUpdateViews: boolean
  model: Model
  currentViewID: string
  currentViewID_Counter: number
  canUpdateViewConfig: boolean
  intervalVar: number
  eventSocket: WebSocket | null
  busy: boolean
  loggedOut: boolean
  connectionState: number // 0 = ok, 1 = trying to reconnect, 2 = connection lost
  eventListener: (eventName: string, eventPayload: any) => void
  resizeListener: () => void
  showTimeRangeSelector: boolean
  showTimeRangeEndTimeOnly: boolean
  diffStepSizeMS: number // 0 means auto
  timeRange: TimeRange
  timeRangeListener: (timeRange: TimeRange) => void
  eventBurstCount: number
  eventACKCounter: number
  eventACKTime: number
}

export const globalState = reactive<GlobalState>({
  sessionID: '',
  user: '',
  canUpdateViews: false,
  model: { views: [] },
  currentViewID: '',
  currentViewID_Counter: 0,
  canUpdateViewConfig: false,
  intervalVar: 0,
  eventSocket: null,
  busy: false,
  loggedOut: false,
  connectionState: 0,
  eventListener: () => {},
  resizeListener: () => {},
  showTimeRangeSelector: false,
  showTimeRangeEndTimeOnly: false,
  diffStepSizeMS: 0,
  timeRange: {
    type: 'Last',
    lastCount: 6,
    lastUnit: 'Hours',
    rangeStart: '',
    rangeEnd: '',
  },
  timeRangeListener: () => {},
  eventBurstCount: 1,
  eventACKCounter: 0,
  eventACKTime: 0,
})

export default globalState
