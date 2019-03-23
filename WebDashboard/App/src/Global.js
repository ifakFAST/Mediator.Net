export default {
    sessionID: "",
    model: {},
    currentViewID: "",
    intervalVar: 0,
    eventSocket: {},
    busy: false,
    eventListener: function(eventName, eventPayload) {},
    showTimeRangeSelector: false,
    timeRange: {
      type: 'Last',
      lastCount: 7,
      lastUnit: 'Days',
      rangeStart: '',
      rangeEnd: ''
   },
   timeRangeListener: function(timeRange) {},
}