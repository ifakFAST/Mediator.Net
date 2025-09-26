let theSessionID = ''
let viewReady = false
let eventSocket: WebSocket | null = null
let connectionState = 0
let intervalVar: any = null
let eventListener = (eventName: string, eventPayload: any) => {}
const backendServer = 'localhost:8082'

interface HasError {
  error: string
}

export function setupDashboardEnv(theViewID: string, isRelogin?: boolean): void {
  const backendURL = 'http://' + backendServer
  const user = 'ifak'
  const pass = 'fast'

  postRequest(
    'text',
    backendURL + '/login',
    { user, pass },
    (strResponse: string) => {
      if (isRelogin === true) {
        console.info('Relogin success.')
      }

      const response = JSON.parse(strResponse)
      theSessionID = response.sessionID
      openWebSocket(theViewID)
      postRequest(
        'text',
        backendURL + '/activateView?' + theSessionID + '_' + theViewID,
        '',
        (resp) => {
          viewReady = true
        },
        (error: any) => {
          reportErrorConsole(error, 'Failed to activate view.')
        },
      )
    },
    (error: any) => {
      reportErrorConsole(error, 'Connect error.')
      if (isRelogin === true) {
        console.info('Relogin failed! Will try again in 2 seconds...')
        const relogin = () => {
          setupDashboardEnv(theViewID, true)
        }
        setTimeout(relogin, 2000)
      }
    },
  )
  ;(window as any)['dashboardApp'] = {
    canUpdateViewConfig() {
      return true
    },

    getBackendUrl(): string {
      return backendURL
    },

    getViewRequestUrl(request: string): string | null {
      if (viewReady === false) {
        console.log('View not yet ready')
        return null
      }

      const ctx = theSessionID + '_' + theViewID
      // console.log('sendViewRequest: ' + request)

      return backendURL + '/viewRequest/' + request + '?' + ctx
    },

    sendViewRequest(request: string, payload: any, successHandler: (arg0: string) => void) {
      if (viewReady === false) {
        console.log('View not yet ready')
        setTimeout(() => {
          ;(window as any)['dashboardApp'].sendViewRequest(request, payload, successHandler)
        }, 750)
        return
      }

      const ctx = theSessionID + '_' + theViewID
      // console.log('sendViewRequest: ' + request)

      postRequest(
        'text',
        backendURL + '/viewRequest/' + request + '?' + ctx,
        payload,
        (strResponse: string) => {
          successHandler(strResponse)
        },
        (error: any) => {
          reportError(error, 'Connect error.')
        },
      )
    },
    sendViewRequestBlob(request: string, payload: any, successHandler: (arg0: any) => void) {
      if (viewReady === false) {
        console.log('View not yet ready')
        setTimeout(() => {
          ;(window as any)['dashboardApp'].sendViewRequestBlob(request, payload, successHandler)
        }, 750)
        return
      }

      const ctx = theSessionID + '_' + theViewID
      // console.log('sendViewRequestBlob: ' + request)

      postRequest(
        'blob',
        backendURL + '/viewRequest/' + request + '?' + ctx,
        payload,
        (blobResponse: any) => {
          successHandler(blobResponse)
        },
        (error: any) => {
          reportError(error, 'Connect error.')
        },
      )
    },
    sendViewRequestAsync(request: string, payload: object | string, responseType?: 'text' | 'blob') {
      const rspType = responseType || 'text'
      if (viewReady === false) {
        console.log('View not yet ready')
        return new Promise((resolve, reject) => {
          setTimeout(() => {
            ;(window as any)['dashboardApp'].sendViewRequestAsync(request, payload, responseType).then(resolve).catch(reject)
          }, 750)
        })
      }

      const ctx = theSessionID + '_' + theViewID
      // console.log('sendViewRequestAsync: ' + request)

      const promise = new Promise((resolve, reject) => {
        postRequest(
          rspType,
          backendURL + '/viewRequest/' + request + '?' + ctx,
          payload,
          (strResponse: string) => {
            if (strResponse && strResponse !== '') {
              try {
                if (rspType === 'text') {
                  resolve(JSON.parse(strResponse))
                } else {
                  resolve(strResponse)
                }
              } catch (e) {
                reject(e)
              }
            } else {
              resolve('')
            }
          },
          (error) => {
            if (error && error.error) {
              reject(new Error(error.error))
            } else {
              reject(new Error('Connect error.'))
            }
          },
        )
      })

      return promise
    },
    registerViewEventListener(listener: (eventName: string, eventPayload: any) => void) {
      eventListener = listener
      // console.log('registerViewEventListener')
    },
    registerResizeListener(listener: any) {},
    showTimeRangeSelector(show: boolean) {
      // console.log('showTimeRangeSelector: ' + show)
    },
    getCurrentTimeRange() {
      return {
        type: 'Last',
        lastCount: 1,
        lastUnit: 'Hours',
        rangeStart: '',
        rangeEnd: '',
      }
    },
    registerTimeRangeListener(listener: any) {
      // console.log('registerTimeRangeListener')
    },
    showTimeRangeEndTimeOnly(show: boolean) {
      // console.log('showTimeRangeEndTimeOnly: ' + show)
    },
    setEventBurstCount(count: number) {},
  }
}

function reportErrorConsole(error: { error: any }, fallBack: string): void {
  if (error && error.error) {
    console.error(error.error)
  } else {
    console.error(fallBack)
  }
}

function reportError(error: { error: any }, fallBack: string): void {
  if (error && error.error) {
    alert(error.error)
  } else {
    alert(fallBack)
  }
}

function postRequest(
  responseType: 'text' | 'blob',
  url: string,
  content: string | object,
  callback: { (resp: any): void },
  errHandler: { (error: HasError): void },
) {
  let strContent: string = ''

  if (typeof content === 'string') {
    strContent = content
  } else {
    strContent = JSON.stringify(content)
  }

  const request = new XMLHttpRequest()
  request.responseType = responseType
  request.onreadystatechange = function () {
    if (this.readyState === 4) {
      if (this.status === 200) {
        if (responseType === 'text') {
          callback(this.responseText)
        } else {
          callback(this.response)
        }
      } else {
        let errObj: HasError = { error: 'Unknown error' }
        try {
          errObj = JSON.parse(this.response)
        } catch {}
        errHandler(errObj)
      }
    }
  }
  request.open('POST', url, true)
  request.send(strContent)
}

function openWebSocket(theViewID: string) {
  if (window.WebSocket) {
    const socket = new WebSocket('ws://' + backendServer + '/websocket/')
    eventSocket = socket

    socket.onopen = (openEvent) => {
      connectionState = 0
      socket.send(theSessionID)
      const doKeepAlive = () => {
        socket.send('KA')
      }
      intervalVar = setInterval(doKeepAlive, 5000)
    }

    socket.onmessage = (wsEvent) => {
      connectionState = 0
      const parsedData = JSON.parse(wsEvent.data)
      eventListener(parsedData.event, parsedData.payload)
      const doACK = () => {
        socket.send('OK')
      }
      setTimeout(doACK, 500)
    }

    socket.onclose = (ev) => {
      connectionState = ev.wasClean ? 2 : 1

      if (intervalVar !== null) {
        clearInterval(intervalVar)
        intervalVar = null
      }

      if (!ev.wasClean) {
        const reconnect = () => {
          openWebSocket(theViewID)
        }
        setTimeout(reconnect, 3000)
      }

      if (ev.wasClean && theSessionID !== '') {
        console.info('Will try to relogin in 3 seconds...')
        const relogin = () => {
          setupDashboardEnv(theViewID, true)
        }
        setTimeout(relogin, 3000)
      }
    }
  }
}
