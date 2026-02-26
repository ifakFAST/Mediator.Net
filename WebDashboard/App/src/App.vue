<template>
  <div>
    <template v-if="loginRequired">
      <Login @loginSuccess="loginSuccess"></Login>
    </template>

    <template v-else>
      <Dashboard
        :user="user"
        :views="model.views"
        :busy="busy"
        :connectionState="connectionState"
        :currViewID="currentViewID"
        :currViewSrc="currentViewSource"
        :timeRangeSelected="timeRange"
        :showTime="showTimeRangeSelector"
        :showEndTimeOnly="showTimeRangeEndTimeOnly"
        :canUpdateViews="canUpdateViews"
        @logout="logout"
        @activateView="activateView"
        @timechange="timeSelectChanged"
        @duplicateView="duplicateView"
        @renameView="renameView"
        @duplicateConvertView="duplicateConvertView"
        @toggleHeader="toggleHeader"
        @moveUp="moveUpView"
        @moveDown="moveDownView"
        @delete="deleteView"
      ></Dashboard>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import axios from 'axios'
import Login from './Login.vue'
import Dashboard from './Dashboard.vue'
import globalState, { type TimeRange } from './global'

function withViewContext(viewID: string) {
  return {
    headers: {
      Authorization: 'Bearer ' + globalState.sessionID,
      'X-Dashboard-View-ID': viewID,
    },
  }
}

const loginRequired = computed(() => globalState.sessionID === '')

const currentViewSource = computed(() => {
  if (globalState.currentViewID === '') return ''
  const view = globalState.model.views.find((v) => v.viewID === globalState.currentViewID)
  if (!view) return ''
  return view.viewURL + '?viewID=' + globalState.currentViewID + '&counter=' + globalState.currentViewID_Counter
})

const user = computed(() => globalState.user)
const model = computed(() => globalState.model)
const busy = computed(() => globalState.busy)
const connectionState = computed(() => globalState.connectionState)
const currentViewID = computed(() => globalState.currentViewID)
const timeRange = computed(() => globalState.timeRange)
const showTimeRangeSelector = computed(() => globalState.showTimeRangeSelector)
const showTimeRangeEndTimeOnly = computed(() => globalState.showTimeRangeEndTimeOnly)
const canUpdateViews = computed(() => globalState.canUpdateViews)

function loginSuccess(event: {
  session: string
  user: string
  pass: string
  model: any
  canUpdateViews: boolean
  initialTimeRange: any
  initialStepSizeMS: number
  viewID: string
}) {
  console.info('Login success')
  globalState.sessionID = event.session
  globalState.user = event.user
  globalState.model = event.model
  globalState.canUpdateViews = event.canUpdateViews

  globalState.timeRange.type = event.initialTimeRange.Type
  globalState.timeRange.lastCount = event.initialTimeRange.LastCount
  globalState.timeRange.lastUnit = event.initialTimeRange.LastUnit
  globalState.timeRange.rangeStart = event.initialTimeRange.RangeStart
  globalState.timeRange.rangeEnd = event.initialTimeRange.RangeEnd

  globalState.diffStepSizeMS = event.initialStepSizeMS

  openWebSocket(event.user, event.pass)
  const viewIdx = globalState.model.views.findIndex((v) => v.viewID === event.viewID)
  if (viewIdx >= 0) {
    activateView(event.viewID)
  } else if (globalState.model.views.length > 0) {
    activateView(globalState.model.views[0].viewID)
  }
}

function logout() {
  if (globalState.intervalVar !== 0) {
    clearInterval(globalState.intervalVar)
    globalState.intervalVar = 0
  }
  axios
    .post('/logout', null, {
      headers: {
        Authorization: 'Bearer ' + globalState.sessionID,
      },
    })
    .catch(() => {})
  globalState.sessionID = ''
  globalState.user = ''
  globalState.model = { views: [] }
  globalState.currentViewID = ''
  globalState.loggedOut = true
  globalState.eventListener = () => {}
  try {
    globalState.eventSocket?.close()
  } catch (error) {
    console.log('Error closing websocket: ' + error)
  }
}

function openWebSocket(user: string, pass: string) {
  if (typeof WebSocket !== 'undefined') {
    const isHTTPS = window.location.protocol === 'https:'
    const websocketProtocol = isHTTPS ? 'wss://' : 'ws://'
    const socket = new WebSocket(websocketProtocol + window.location.host + '/websocket/')
    globalState.eventSocket = socket

    socket.onopen = () => {
      globalState.connectionState = 0
      socket.send(globalState.sessionID)
      const doKeepAlive = () => {
        socket.send('KA')
      }
      globalState.intervalVar = window.setInterval(doKeepAlive, 5000)
    }

    socket.onmessage = (wsEvent) => {
      const eventStartTime = new Date().getTime()
      globalState.connectionState = 0
      const parsedData = JSON.parse(wsEvent.data)
      if (parsedData.event === 'NaviAugmentation') {
        handleViewAugmentation(parsedData.payload)
      } else {
        globalState.eventListener(parsedData.event, parsedData.payload)
      }
      const doACK = () => {
        globalState.eventACKTime = new Date().getTime()
        socket.send('OK')
      }

      globalState.eventACKCounter += 1
      if (globalState.eventACKCounter < globalState.eventBurstCount) {
        doACK()
      } else {
        globalState.eventACKCounter = 0
        const diff = eventStartTime - globalState.eventACKTime
        const minDist = 500
        const waitTime = minDist - diff
        if (waitTime <= 5) {
          doACK()
        } else {
          setTimeout(doACK, waitTime)
        }
      }
    }

    socket.onclose = (ev) => {
      globalState.connectionState = ev.wasClean ? 2 : 1
      if (globalState.intervalVar !== 0) {
        clearInterval(globalState.intervalVar)
        globalState.intervalVar = 0
      }
      if (!ev.wasClean) {
        const reconnect = () => {
          openWebSocket(user, pass)
        }
        setTimeout(reconnect, 3000)
      }
      if (ev.wasClean && globalState.sessionID !== '') {
        console.info('Will try to relogin in 3 seconds...')
        const relogin = () => {
          tryReLogin(user, pass)
        }
        setTimeout(relogin, 3000)
      }
    }
  }
}

function handleViewAugmentation(eventPayload: { viewID: string; iconColor: string }) {
  const viewID = eventPayload.viewID
  const view = globalState.model.views.find((v) => v.viewID === viewID)
  if (view) {
    view.viewIconColor = eventPayload.iconColor
  }
}

function tryReLogin(user: string, pass: string) {
  if (globalState.sessionID === '') {
    console.info('Abort relogin because of logout.')
    return
  }
  console.info('Trying to relogin...')
  axios
    .post('/login', { user, pass })
    .then((response) => {
      console.info('Relogin success.')
      const viewID = globalState.currentViewID
      globalState.currentViewID = ''
      globalState.sessionID = response.data.sessionID
      globalState.model = response.data.model
      openWebSocket(user, pass)
      doActivateView(viewID)
    })
    .catch(() => {
      console.info('Relogin failed! Will try again in 2 seconds...')
      const relogin = () => {
        tryReLogin(user, pass)
      }
      setTimeout(relogin, 2000)
    })
}

function activateView(viewID: string) {
  if (globalState.currentViewID === viewID) return
  doActivateView(viewID)
}

function doActivateView(viewID: string) {
  const previousEventListener = globalState.eventListener
  globalState.eventListener = () => {}
  axios
    .post('/activateView', null, withViewContext(viewID))
    .then((response) => {
      globalState.eventBurstCount = 1
      globalState.currentViewID = viewID
      globalState.canUpdateViewConfig = response.data.canUpdateViewConfig
      globalState.showTimeRangeSelector = false
    })
    .catch((error) => {
      globalState.eventListener = previousEventListener
      if (error.response && error.response.data && error.response.data.error) {
        alert(error.response.data.error)
      } else {
        alert('Failed to activate view.')
      }
    })
}

function timeSelectChanged(timeRange: TimeRange) {
  globalState.timeRange = Object.assign({}, timeRange)
  globalState.timeRangeListener(Object.assign({}, timeRange))
}

function duplicateView(viewID: string) {
  axios
    .post('/duplicateView', null, withViewContext(viewID))
    .then((response) => {
      console.info('duplicateView success.')
      const newViewID = response.data.newViewID
      globalState.model = response.data.model
      doActivateView(newViewID)
    })
    .catch(handleError)
}

function duplicateConvertView(viewID: string) {
  axios
    .post('/duplicateConvertView', null, withViewContext(viewID))
    .then((response) => {
      console.info('duplicateConvertView success.')
      const newViewID = response.data.newViewID
      globalState.model = response.data.model
      doActivateView(newViewID)
    })
    .catch(handleError)
}

function toggleHeader(viewID: string) {
  axios
    .post('/toggleHeader', null, withViewContext(viewID))
    .then(() => {
      console.info('toggleHeader success.')
      globalState.currentViewID_Counter = globalState.currentViewID_Counter + 1
    })
    .catch(handleError)
}

function renameView(viewID: string, newName: string) {
  axios
    .post('/renameView', { newViewName: newName }, withViewContext(viewID))
    .then((response) => {
      console.info('renameView success.')
      globalState.model = response.data.model
    })
    .catch(handleError)
}

function moveUpView(viewID: string) {
  axios
    .post('/moveView', { up: true }, withViewContext(viewID))
    .then((response) => {
      console.info('moveUpView success.')
      globalState.model = response.data.model
    })
    .catch(handleError)
}

function moveDownView(viewID: string) {
  axios
    .post('/moveView', { up: false }, withViewContext(viewID))
    .then((response) => {
      console.info('moveDownView success.')
      globalState.model = response.data.model
    })
    .catch(handleError)
}

function deleteView(viewID: string) {
  axios
    .post('/deleteView', null, withViewContext(viewID))
    .then((response) => {
      console.info('deleteView success.')
      globalState.model = response.data.model
      if (globalState.currentViewID === viewID) {
        globalState.currentViewID = ''
      }
    })
    .catch(handleError)
}

function handleError(error: any) {
  if (error.response && error.response.data && error.response.data.error) {
    alert(error.response.data.error)
  } else {
    alert(error.message)
  }
}
</script>
