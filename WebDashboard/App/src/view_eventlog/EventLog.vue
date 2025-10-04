<template>
  <div>
    <v-toolbar>
      <v-checkbox
        v-model="includeAlarms"
        class="mr-4 ml-2"
        label="Alarms"
      />
      <v-checkbox
        v-model="includeWarnings"
        class="mr-4"
        label="Warnings"
      />
      <v-checkbox
        v-model="includeInfos"
        class="mr-4"
        label="Info"
      />
      <v-spacer />
      <v-text-field
        v-model="search"
        class="mr-2"
        append-inner-icon="mdi-magnify"
        label="Search"
        single-line
      />
    </v-toolbar>

    <v-data-table
      class="elevation-4 mt-2"
      :custom-filter="customFilter"
      :headers="headers"
      v-model:items-per-page="perPage"
      :items-per-page-options="[50, 100, 500, 1000, { value: -1, title: 'All' }]"
      item-value="T"
      :items="filteredEvents"
      must-sort
      no-data-text="No events"
      :search="search"
      :sort-by="[{ key: 'T', order: 'desc' }]"
    >
      <template #item="{ item }">
        <tr>
          <td
            :class="classObject(item)"
            style="white-space: nowrap"
          >
            {{ item.TimeFirstLocal }}
          </td>
          <td :class="classObject(item)">{{ item.Severity }}</td>
          <td :class="classObject(item)">{{ item.Msg }}</td>
          <td :class="classObject(item)">{{ item.Source }}</td>
          <td :class="classObject(item)">{{ stateCol(item) }}</td>
          <td class="text-center">
            <v-btn
              icon="mdi-dots-horizontal"
              size="small"
              variant="text"
              @click="showDetails(item)"
            />
          </td>
        </tr>
      </template>
    </v-data-table>

    <v-dialog
      v-if="detailItem"
      v-model="details"
      max-width="800"
      @keydown="editKeydown"
    >
      <v-card>
        <v-card-title>
          <span class="text-h5">{{ detailItem.Severity }} Details</span>
        </v-card-title>
        <v-card-text>
          <table class="dataTable">
            <tbody>
              <tr>
                <td class="dataHead">{{ timeTitle }}</td>
                <td>&nbsp;</td>
                <td class="dataBody">{{ detailItem.TimeFirstLocal }}</td>
              </tr>
              <tr v-if="multi">
                <td class="dataHead">Time&nbsp;Last</td>
                <td>&nbsp;</td>
                <td class="dataBody">{{ detailItem.TimeLastLocal + ' (total count: ' + detailItem.Count + ')' }}</td>
              </tr>
              <tr>
                <td class="dataHead">Source</td>
                <td>&nbsp;</td>
                <td class="dataBody">{{ detailItem.Source }}</td>
              </tr>
              <tr>
                <td class="dataHead">Type</td>
                <td>&nbsp;</td>
                <td class="dataBody">{{ detailItem.Type }}</td>
              </tr>
              <tr v-if="initiator.length > 0">
                <td class="dataHead">Inititator</td>
                <td>&nbsp;</td>
                <td class="dataBody">{{ initiator }}</td>
              </tr>
              <tr>
                <td class="dataHead">Message</td>
                <td>&nbsp;</td>
                <td class="dataBody">{{ detailItem.Message }}</td>
              </tr>
              <tr v-if="showState">
                <td class="dataHead">State</td>
                <td>&nbsp;</td>
                <td
                  class="dataBody"
                  style="white-space: pre-wrap"
                >
                  {{ state }}
                </td>
              </tr>
              <tr v-if="hasDetails">
                <td class="dataHead">Details</td>
                <td>&nbsp;</td>
                <td
                  class="dataBody"
                  style="white-space: pre-wrap"
                >
                  {{ detailItem.Details }}
                </td>
              </tr>
              <tr v-if="hasObjects">
                <td class="dataHead">Affected&nbsp;Objects</td>
                <td>&nbsp;</td>
                <td class="dataBody">{{ objects }}</td>
              </tr>
            </tbody>
          </table>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn
            color="primary"
            variant="text"
            @click="details = false"
          >
            Close
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { Alarm } from './types'
import type { DataTableHeader } from '@/utils'

const props = defineProps<{
  events: Alarm[]
  timeRange: any
}>()

const search = ref('')
const includeAlarms = ref(true)
const includeWarnings = ref(true)
const includeInfos = ref(true)
const perPage = ref(50)

const headers: DataTableHeader[] = [
  { title: 'Time (Local)', align: 'start', sortable: true, key: 'T' },
  { title: 'Type', align: 'start', sortable: true, key: 'Severity' },
  { title: 'Message', align: 'start', sortable: true, key: 'Msg' },
  { title: 'Source', align: 'start', sortable: true, key: 'Source' },
  { title: 'State', align: 'start', sortable: true, key: 'State' },
  { title: 'Details', align: 'center', sortable: false, key: 'actions' },
]

const details = ref(false)
const detailItem = ref<Alarm | null>(null)

const classObject = (item: Alarm): Record<string, boolean> => {
  return {
    bold: item.State === 'New' && (item.Severity === 'Warning' || item.Severity === 'Alarm'),
    ErrWarning: item.Severity === 'Warning' && item.State !== 'Reset' && item.RTN === false,
    ErrAlarm: item.Severity === 'Alarm' && item.State !== 'Reset' && item.RTN === false,
  }
}

const showDetails = (item: Alarm): void => {
  detailItem.value = item
  details.value = true
}

const editKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    details.value = false
  }
}

const customFilter = (value: string, search: string | null, item?: any): boolean => {
  if (search === null) return true
  if (item === undefined) return true
  const it: Alarm = item.raw
  search = search.toLowerCase()
  const words = search.split(' ').filter((w) => w !== '')
  const valLower = (it.Message + ' ' + it.Source).toLowerCase()
  return words.every((word) => valLower.indexOf(word) !== -1)
}

const stateCol = (it: Alarm): string => {
  if (it.Severity === 'Info') {
    return ''
  }
  return it.State
}

const filteredEvents = computed((): Alarm[] => {
  const alarm = includeAlarms.value
  const warn = includeWarnings.value
  const info = includeInfos.value
  if (alarm && warn && info) {
    return props.events
  }
  return props.events.filter((it) => {
    const s = it.Severity
    return (alarm && s === 'Alarm') || (warn && s === 'Warning') || (info && s === 'Info')
  })
})

const timeTitle = computed((): string => {
  if (!detailItem.value) return 'Time'
  const count = detailItem.value.Count
  if (count === undefined || count === 1) {
    return 'Time'
  }
  return 'Time\u00A0First'
})

const hasDetails = computed((): boolean => {
  if (!detailItem.value) return false
  const details = detailItem.value.Details
  return details !== undefined && details.length > 0
})

const multi = computed((): boolean => {
  if (!detailItem.value) return false
  const count = detailItem.value.Count
  return count !== undefined && count > 1
})

const initiator = computed((): string => {
  if (!detailItem.value) return ''
  const ini = detailItem.value.Initiator
  if (ini === undefined || ini === null) {
    return ''
  }
  return ini.Type + ' ' + ini.Name
})

const showState = computed((): boolean => {
  if (!detailItem.value) return false
  const s = detailItem.value.Severity
  if (s === undefined || s === 'Info') {
    return false
  }
  return true
})

const state = computed((): string => {
  if (!detailItem.value) return ''
  const item = detailItem.value
  const s = item.State
  const ack = item.InfoACK
  const reset = item.InfoReset
  const rtn = item.RTN
  if (s === undefined) {
    return ''
  }

  let res = ''

  if (rtn) {
    res = 'Returned to normal at ' + item.TimeRTNLocal
  }

  if (ack) {
    if (res.length > 0) {
      res = res + '\r\n'
    }
    res = res + 'Acknowledged by ' + ack.UserName + ' at ' + item.TimeAckLocal
    if (ack.Comment.length > 0) {
      res = res + '\r\nACK Comment: ' + ack.Comment
    }
  }

  if (reset) {
    if (res.length > 0) {
      res = res + '\r\n'
    }
    res = res + 'Reset by ' + reset.UserName + ' at ' + item.TimeResetLocal
    if (reset.Comment.length > 0) {
      res = res + '\r\nReset Comment: ' + reset.Comment
    }
  }

  if (res.length === 0) {
    return s
  }
  return res
})

const hasObjects = computed((): boolean => {
  if (!detailItem.value) return false
  const objs = detailItem.value.Objects
  return objs !== undefined && objs.length > 0
})

const objects = computed((): string => {
  if (!detailItem.value) return ''
  const objs = detailItem.value.Objects
  if (objs === undefined) {
    return ''
  }
  return objs.join(', ')
})
</script>
