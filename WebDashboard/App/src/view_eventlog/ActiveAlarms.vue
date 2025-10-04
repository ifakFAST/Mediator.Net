<template>
  <div>
    <v-toolbar>
      <v-btn
        :disabled="!ackEnabled"
        variant="text"
        @click="startAckOrReset(true)"
      >
        Acknowledge
      </v-btn>
      <v-btn
        :disabled="!resetEnabled"
        variant="text"
        @click="startAckOrReset(false)"
      >
        Reset
      </v-btn>
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
      v-model="selected"
      class="elevation-4 mt-2"
      :custom-filter="customFilter"
      :headers="headers"
      v-model:items-per-page="perPage"
      :items-per-page-options="[50, 100, 500, 1000, { value: -1, title: 'All' }]"
      item-value="T"
      :items="alarms"
      must-sort
      no-data-text="No active warnings or alarms"
      :search="search"
      show-select
      :sort-by="[{ key: 'T', order: 'desc' }]"
    >
      <template #item.TimeFirstLocal="{ item }">
        <span :class="classObject(item)">{{ item.TimeFirstLocal }}</span>
      </template>
      <template #item.TimeLastLocal="{ item }">
        <span :class="classObject(item)">{{ item.TimeLastLocal }}</span>
      </template>
      <template #item.Msg="{ item }">
        <span :class="classObject(item)">{{ item.Msg }}</span>
      </template>
      <template #item.Details="{ item }">
        <v-btn
          icon="mdi-dots-horizontal"
          size="small"
          variant="text"
          @click="showDetails(item)"
        />
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
                <td class="dataHead">Time&nbsp;First</td>
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
              <tr>
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

    <v-dialog
      v-model="showAckReset"
      max-width="600"
      @keydown="editKeydown"
    >
      <v-card>
        <v-card-title>
          <span class="text-h5">{{ ackResetTitel }}</span>
        </v-card-title>
        <v-card-text>
          <v-text-field
            v-model="comment"
            autofocus
            label="Comment"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn
            color="grey-darken-1"
            variant="text"
            @click="showAckReset = false"
          >
            Cancel
          </v-btn>
          <v-btn
            color="primary"
            variant="text"
            @click="commitAckOrReset"
          >
            OK
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
  alarms: Alarm[]
}>()

const emit = defineEmits<{
  (e: 'ackreset', data: { Ack: boolean; Comment: string; Timestamps: number[] }): void
}>()

const search = ref('')
const selected = ref<any[]>([])
const details = ref(false)
const detailItem = ref<Alarm | null>(null)
const showAckReset = ref(false)
const isACK = ref(false)
const comment = ref('')
const perPage = ref(50)

const headers: DataTableHeader[] = [
  { title: 'First', align: 'start', sortable: true, key: 'TimeFirstLocal' },
  { title: 'Last', align: 'start', sortable: true, key: 'TimeLastLocal' },
  { title: 'Message', align: 'start', sortable: true, key: 'Msg' },
  { title: 'Source', align: 'start', sortable: true, key: 'Source' },
  { title: 'State', align: 'start', sortable: true, key: 'State' },
  { title: 'Count', align: 'center', sortable: true, key: 'Count', width: '94' },
  { title: 'Details', align: 'center', sortable: false, key: 'Details' },
]

const classObject = (item: Alarm): Record<string, boolean> => {
  return {
    bold: item.State === 'New',
    ErrWarning: item.Severity === 'Warning' && item.RTN === false,
    ErrAlarm: item.Severity === 'Alarm' && item.RTN === false,
  }
}

const showDetails = (item: Alarm): void => {
  detailItem.value = item
  details.value = true
}

const editKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    details.value = false
    showAckReset.value = false
  }
}

const startAckOrReset = (ack: boolean): void => {
  comment.value = ''
  isACK.value = ack
  showAckReset.value = true
}

const commitAckOrReset = (): void => {
  emit('ackreset', {
    Ack: isACK.value,
    Comment: comment.value,
    Timestamps: selected.value.map((it: Alarm) => it.T),
  })
  showAckReset.value = false
  selected.value = []
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

const ackResetTitel = computed((): string => {
  const res = isACK.value ? 'Acknowledge ' : 'Reset '
  const alarms = selected.value.filter((it) => it.Severity === 'Alarm').length
  const warns = selected.value.filter((it) => it.Severity === 'Warning').length
  let txtAlarm = ''
  if (alarms === 1) txtAlarm = '1 Alarm'
  if (alarms > 1) txtAlarm = `${alarms} Alarms`
  let txtWarn = ''
  if (warns === 1) txtWarn = '1 Warning'
  if (warns > 1) txtWarn = `${warns} Warnings`
  return res + txtAlarm + (alarms > 0 && warns > 0 ? ' and ' : '') + txtWarn
})

const ackEnabled = computed((): boolean => {
  return selected.value.length > 0 && selected.value.every((it) => it.State === 'New')
})

const resetEnabled = computed((): boolean => {
  return selected.value.length > 0
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
  if (ini === undefined || ini === null) return ''
  return ini.Type + ' ' + ini.Name
})

const state = computed((): string => {
  if (!detailItem.value) return ''
  const item = detailItem.value
  const s = item.State
  const ack = item.InfoACK
  if (s === undefined) return ''
  if (s === 'Ack' && ack) {
    let res = 'Acknowledged by ' + ack.UserName + ' at ' + item.TimeAckLocal
    if (ack.Comment.length > 0) {
      res = res + '\r\nComment: ' + ack.Comment
    }
    return res
  }
  if (item.RTN) {
    return 'Returned to normal at ' + item.TimeRTNLocal
  }
  return s
})

const hasObjects = computed((): boolean => {
  if (!detailItem.value) return false
  const objs = detailItem.value.Objects
  return objs !== undefined && objs.length > 0
})

const objects = computed((): string => {
  if (!detailItem.value) return ''
  const objs = detailItem.value.Objects
  if (objs === undefined) return ''
  return objs.join(', ')
})
</script>
