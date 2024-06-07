<template>
  <v-app>
    <v-main>
      <v-container fluid>

        <v-tabs v-model="currentTab">

          <v-tab>Active Alarms</v-tab>
          <v-tab-item>
            <active-alarms class="px-1 py-1" :alarms="alarms" @ackreset="onAckResetAlarms"></active-alarms>
          </v-tab-item>

          <v-tab>Event Log</v-tab>
          <v-tab-item>
            <event-log class="px-1 py-1" :events="events" :time-range="timeRange"></event-log>
          </v-tab-item>

        </v-tabs>

      </v-container>
    </v-main>
  </v-app>
</template>

<script lang="ts">

import ActiveAlarms from './ActiveAlarms.vue'
import EventLog from './EventLog.vue'
import { Component, Vue, Watch } from 'vue-property-decorator'

import { Alarm } from './types'

@Component({
  components: {
    ActiveAlarms,
    EventLog,
  },
})
export default class ViewAlarms extends Vue {

  currentTab = 0
  alarms: Alarm[] = []
  events = []
  timeRange = {
      type: 'Last',
      lastCount: 7,
      lastUnit: 'Days',
      rangeStart: '',
      rangeEnd: '',
  }

  load() {
    const context = this
    const para = this.timeRange
    window.parent['dashboardApp'].sendViewRequest('Load', para, (strResponse) => {
      const response = JSON.parse(strResponse)
      context.alarms = response.Alarms.map(this.attachSelected)
      context.events = response.Events
    })
  }

  onAckResetAlarms(para) {
    const context = this
    para.TimeRange = this.timeRange
    window.parent['dashboardApp'].sendViewRequest('AckReset', para, (strResponse) => {
      const response = JSON.parse(strResponse)
      context.alarms = response.Alarms.map(this.attachSelected)
      context.events = response.Events
    })
  }

  attachSelected(alarm: Alarm): Alarm {
    alarm.selected = false
    return alarm
  }

  @Watch('currentTab')
  watch_currentTab(newTab: number, oldTab) { // newTab is string!
    window.parent['dashboardApp'].showTimeRangeSelector(newTab === 1)
  }

  mounted() {
    this.timeRange = window.parent['dashboardApp'].getCurrentTimeRange()
    this.load()

    const context = this
    window.parent['dashboardApp'].registerTimeRangeListener((timeRange) => {
      context.timeRange = timeRange
      context.load()
    })

    window.parent['dashboardApp'].registerViewEventListener((eventName, eventPayload) => {

      if (eventName === 'Event') {

        const fUpdate = (list, newOrChanged) => {

          const len = newOrChanged.length
          for (let i = 0; i < len; i++) {
            const entry = JSON.parse(JSON.stringify(newOrChanged[i]))
            const t = entry.T
            const idx = list.findIndex((it) => it.T === t)
            if (idx < 0) {
              list.push(entry)
            }
            else {
              Vue.set(list, idx, entry)
            }
          }
        }

        fUpdate(context.alarms, eventPayload.Alarms)
        fUpdate(context.events, eventPayload.Events)

        for (const removedAlarm of eventPayload.RemovedAlarms) {
          const idx = context.alarms.findIndex((it) => it.T === removedAlarm)
          if (idx >= 0) {
            console.info('Removed alarm >>' + context.alarms[idx].Message + '<< at index ' + idx)
            context.alarms.splice(idx, 1)
          }
        }

      }
    })
  }

}

</script>

<style>
 html {
    font-size: 16px;
  }
  .v-data-table > .v-data-table__wrapper > table > thead > tr > th {
    font-size: 16px;
    font-weight: bold;
    padding-left: 8px !important;
    padding-right: 8px !important;
  }
  .v-data-table > .v-data-table__wrapper > table > tbody > tr > td {
    font-size: 16px;
    height: auto;
    padding-top: 8px !important;
    padding-bottom: 8px !important;
    padding-left: 8px !important;
    padding-right: 8px !important;
  }
  .container {
    padding: 16px 0px!important;
  }
  .bold {
    font-weight: bold!important;
  }
  .ErrWarning {
    color: orange;
  }
  .ErrAlarm {
    color: red;
  }
  .small {
    min-width: 42px;
    width: 42px;
  }
  .dataTable {
    border-collapse: separate;
    border-spacing: 5px;
  }
  .dataHead {
    font-weight: bold!important;
    vertical-align: top;
  }
  .dataBody {
    vertical-align: top;
  }
</style>
