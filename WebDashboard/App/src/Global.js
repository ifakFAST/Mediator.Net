export default {
  sessionID: "",
  user: "",
  canUpdateViews: false,
  model: {},
  currentViewID: "",
  currentViewID_Counter: 0,
  canUpdateViewConfig: false,
  intervalVar: 0,
  eventSocket: {},
  busy: false,
  loggedOut: false,
  connectionState: 0, // 0 = ok, 1 = trying to reconnect, 2 = connection lost
  eventListener: function(eventName, eventPayload) {},
  resizeListener: function() {},
  showTimeRangeSelector: false,
  showTimeRangeEndTimeOnly: false,
  diffStepSizeMS: 0, // 0 means auto
  timeRange: {
    type: 'Last',
    lastCount: 6,
    lastUnit: 'Hours',
    rangeStart: '',
    rangeEnd: ''
  },
  timeRangeListener: function(timeRange) {},
  eventBurstCount: 1,
  eventACKCounter: 0,
  eventACKTime: 0
}