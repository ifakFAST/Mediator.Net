import { createApp } from 'vue'
import App from './App.vue'
import { registerPlugins } from './plugins'
import axios from 'axios'
import globalState from './global'

const app = createApp(App)

registerPlugins(app)

app.mount('#app')

// Expose dashboardApp API on window object for iframe communication
declare global {
  interface Window {
    dashboardApp: any
  }
}

window.dashboardApp = {
  sendViewRequest(request: string, payload: any, successHandler: (data: string) => void) {
    doSendViewRequest(request, payload, successHandler, 'text')
  },
  sendViewRequestBlob(request: string, payload: any, successHandler: (data: any) => void) {
    doSendViewRequest(request, payload, successHandler, 'blob')
  },
  sendViewRequestAsync(request: string, payload: any, responseType?: 'text' | 'blob') {
    const rspType = responseType || 'text'
    const viewID = getDashboardViewContext()
    const config = {
      transformResponse: [(data: any) => data],
      responseType: rspType,
      headers: getDashboardHeaders(viewID),
    }
    globalState.busy = true
    return axios
      .post('/viewRequest/' + request, payload, config as any)
      .then((response) => {
        globalState.busy = false
        if (response.data && response.data !== '' && rspType === 'text') {
          return JSON.parse(response.data)
        }
        return response.data
      })
      .catch((error) => {
        globalState.busy = false
        if (error.response && error.response.data) {
          if (error.response.data instanceof Blob) {
            return new Promise((resolve, reject) => {
              const reader = new FileReader()
              reader.onload = () => {
                try {
                  const data = JSON.parse(reader.result as string)
                  if (data.error) {
                    reject(new Error(data.error))
                  } else {
                    reject(new Error(reader.result as string))
                  }
                } catch (e) {
                  reject(e)
                }
              }
              reader.readAsText(error.response.data)
            })
          }

          if (typeof error.response.data !== 'string') {
            throw new Error(error.response.statusText)
          }
          const data = JSON.parse(error.response.data)
          if (data.error) {
            throw new Error(data.error)
          } else {
            throw new Error(error.response.data)
          }
        } else {
          throw error
        }
      })
  },
  getBackendUrl() {
    return ''
  },
  getDashboardViewContext() {
    return getDashboardViewContext()
  },
  registerViewEventListener(listener: (eventName: string, eventPayload: any) => void) {
    globalState.eventListener = listener
  },
  registerResizeListener(listener: () => void) {
    globalState.resizeListener = listener
  },
  showTimeRangeSelector(show: boolean) {
    if (show) {
      globalState.showTimeRangeSelector = true
    } else {
      globalState.showTimeRangeSelector = false
    }
  },
  showTimeRangeEndTimeOnly(endTimeOnly: boolean) {
    globalState.showTimeRangeEndTimeOnly = endTimeOnly
  },
  getCurrentTimeRange() {
    return Object.assign({}, globalState.timeRange)
  },
  registerTimeRangeListener(listener: (timeRange: any) => void) {
    globalState.timeRangeListener = listener
  },
  setEventBurstCount(count: number) {
    globalState.eventBurstCount = count
  },
  canUpdateViewConfig() {
    return globalState.canUpdateViewConfig
  },
}

function getDashboardHeaders(viewID: string) {
  return {
    Authorization: 'Bearer ' + globalState.sessionID,
    'X-Dashboard-View-ID': viewID,
  }
}

function doSendViewRequest(request: string, payload: any, successHandler: (data: any) => void, responseType: 'text' | 'blob') {
  const viewID = getDashboardViewContext()
  const config = {
    transformResponse: [(data: any) => data],
    responseType,
    headers: getDashboardHeaders(viewID),
  }
  globalState.busy = true
  axios
    .post('/viewRequest/' + request, payload, config as any)
    .then((response) => {
      globalState.busy = false
      successHandler(response.data)
    })
    .catch((error) => {
      globalState.busy = false
      if (error.response && error.response.data) {
        try {
          const data = JSON.parse(error.response.data)
          if (data.error) {
            alert('Request failed: ' + data.error)
          } else {
            alert('Request failed: ' + error.response.data)
          }
        } catch (e) {
          alert('Request failed: ' + error.response.data)
        }
      } else {
        alert('Connection error: ' + error)
      }
    })
}

function getDashboardViewContext(): string {
  return globalState.currentViewID
}
