export default {
    sessionID: "",
    model: {},
    currentViewID: "",
    intervalVar: 0,
    eventSocket: {},
    busy: false,
    connectionState: 0, // 0 = ok, 1 = trying to reconnect, 2 = connection lost
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