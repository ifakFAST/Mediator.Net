<template>
  <v-container
    fluid
    class="pa-3"
  >
    <v-row>
      <v-col>
        <v-tabs 
          v-model="currentTab" 
          density="compact">
          <v-tab :value="0">Active Alarms</v-tab>
          <v-tab :value="1">Event Log</v-tab>
        </v-tabs>

        <active-alarms
          v-if="currentTab === 0"
          :alarms="alarms"
          class="px-1 py-1"
          @ackreset="onAckResetAlarms"
        />

        <event-log
          v-if="currentTab === 1"
          class="px-1 py-1"
          :events="events"
          :time-range="timeRange"
        />
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import type { Alarm } from './types'
import ActiveAlarms from './ActiveAlarms.vue'
import EventLog from './EventLog.vue'

const currentTab = ref(0)
const alarms = ref<Alarm[]>([])
const events = ref<any[]>([])
const timeRange = ref({
  type: 'Last',
  lastCount: 7,
  lastUnit: 'Days',
  rangeStart: '',
  rangeEnd: '',
})

const load = (): void => {
  const para = timeRange.value
  // @ts-ignore
  window.parent['dashboardApp'].sendViewRequest('Load', para, (strResponse: string) => {
    const response = JSON.parse(strResponse)
    alarms.value = response.Alarms.map(attachSelected)
    events.value = response.Events
  })
}

const onAckResetAlarms = (para: any): void => {
  para.TimeRange = timeRange.value
  // @ts-ignore
  window.parent['dashboardApp'].sendViewRequest('AckReset', para, (strResponse: string) => {
    const response = JSON.parse(strResponse)
    alarms.value = response.Alarms.map(attachSelected)
    events.value = response.Events
  })
}

const attachSelected = (alarm: Alarm): Alarm => {
  alarm.selected = false
  return alarm
}

watch(currentTab, (newTab: number) => {
  // @ts-ignore
  window.parent['dashboardApp'].showTimeRangeSelector(newTab === 1)
})

onMounted(() => {
  // @ts-ignore
  timeRange.value = window.parent['dashboardApp'].getCurrentTimeRange()
  load()

  // @ts-ignore
  window.parent['dashboardApp'].registerTimeRangeListener((newTimeRange: any) => {
    timeRange.value = newTimeRange
    load()
  })

  // @ts-ignore
  window.parent['dashboardApp'].registerViewEventListener((eventName: string, eventPayload: any) => {
    if (eventName === 'Event') {
      const fUpdate = (list: any[], newOrChanged: any[]): void => {
        const len = newOrChanged.length
        for (let i = 0; i < len; i++) {
          const entry = JSON.parse(JSON.stringify(newOrChanged[i]))
          const t = entry.T
          const idx = list.findIndex((it) => it.T === t)
          if (idx < 0) {
            list.push(entry)
          } else {
            list[idx] = entry
          }
        }
      }

      fUpdate(alarms.value, eventPayload.Alarms)
      fUpdate(events.value, eventPayload.Events)

      for (const removedAlarm of eventPayload.RemovedAlarms) {
        const idx = alarms.value.findIndex((it) => it.T === removedAlarm)
        if (idx >= 0) {
          console.info('Removed alarm >>' + alarms.value[idx].Message + '<< at index ' + idx)
          alarms.value.splice(idx, 1)
        }
      }
    }
  })
})
</script>

<style>
html {
  font-size: 16px;
}
.v-table > .v-table__wrapper > table > thead > tr > th {
  font-size: 16px;
  font-weight: bold;
  padding-left: 8px !important;
  padding-right: 8px !important;
}
.v-table > .v-table__wrapper > table > tbody > tr > td {
  font-size: 16px;
  height: auto;
  padding-top: 8px !important;
  padding-bottom: 8px !important;
  padding-left: 8px !important;
  padding-right: 8px !important;
}
.container {
  padding: 16px 0px !important;
}
.bold {
  font-weight: bold !important;
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
  font-weight: bold !important;
  vertical-align: top;
}
.dataBody {
  vertical-align: top;
}
</style>
