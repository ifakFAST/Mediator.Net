
let theSessionID = ''
let viewReady = false

export function setupDashboardEnv(theViewID: string): void {

  const backendURL = 'http://localhost:8082'

  postRequest(backendURL + '/login', { user: 'ifak', pass: 'fast' }, (strResponse) => {

    const response = JSON.parse(strResponse)
    theSessionID = response.sessionID

    postRequest(backendURL + '/activateView?' + theSessionID + '_' + theViewID, '', (resp) => {
      viewReady = true
    }, (error) => {
      reportError(error, 'Failed to activate view.')
    })

  }, (error) => {
    reportError(error, 'Connect error.')
  })

  window['dashboardApp'] = {

    sendViewRequest(request: string, payload, successHandler) {

      if (viewReady === false) {
        console.log('View not yet ready')
        setTimeout(() => {
          window['dashboardApp'].sendViewRequest(request, payload, successHandler)
        }, 750)
        return
      }

      const ctx = theSessionID + '_' + theViewID
      console.log('sendViewRequest: ' + request)

      postRequest(backendURL + '/viewRequest/' + request + '?' + ctx, payload, (strResponse) => {
        successHandler(strResponse)
      }, (error) => {
        reportError(error, 'Connect error.')
      })

    },
    registerViewEventListener(listener) {
      // console.log('registerViewEventListener')
    },
    showTimeRangeSelector(show: boolean) {
      // console.log('showTimeRangeSelector: ' + show)
    },
    getCurrentTimeRange() {
      return {
        type: 'Last',
        lastCount: 1,
        lastUnit: 'Days',
        rangeStart: '',
        rangeEnd: '',
      }
    },
    registerTimeRangeListener(listener) {
      // console.log('registerTimeRangeListener')
    },
  }
}

function reportError(error, fallBack: string): void {
  if (error && error.error) {
    alert(error.error)
  }
  else {
    alert(fallBack)
  }
}

function postRequest(url: string, content, callback, errHandler) {

  let strContent: string = ''

  if (typeof content === 'string') {
    strContent = content
  }
  else {
    strContent = JSON.stringify(content)
  }

  const request = new XMLHttpRequest()
  request.onreadystatechange = function() {

    if (this.readyState === 4) {
      if (this.status === 200) {
        callback(this.responseText)
      }
      else {
        let errObj = {}
        try {
          errObj = JSON.parse(this.response)
        }
        catch {}
        errHandler(errObj)
      }
    }
  }
  request.open('POST', url, true)
  request.send(strContent)
}
