<template>
  <div>

    <v-toolbar>
      <v-btn flat @click.stop="startAckOrReset(true)"  :disabled="!ackEnabled">Acknowledge</v-btn>
      <v-btn flat @click.stop="startAckOrReset(false)" :disabled="!resetEnabled">Reset</v-btn>
      <v-spacer></v-spacer>
      <v-text-field append-icon="search" label="Search" single-line hide-details v-model="search"></v-text-field>
    </v-toolbar>

    <v-data-table :headers="headers" :items="alarms" :rows-per-page-items="rowsPerPageItems"
                  :pagination.sync="pagination" :search="search" :custom-filter="customFilter"
                  no-data-text="No active warnings or alarms" class="elevation-4 mt-2"
                  v-model="selected" item-key="T" select-all must-sort>
      <template slot="items" slot-scope="props">
        <tr>
          <td class="pad9"><v-checkbox primary hide-details v-model="props.selected"></v-checkbox></td>
          <td v-bind:class="classObject(props.item)">{{ props.item.TimeFirstLocal }}</td>
          <td v-bind:class="classObject(props.item)">{{ props.item.Msg  }}</td>
          <td v-bind:class="classObject(props.item)">{{ props.item.Source }}</td>
          <td v-bind:class="classObject(props.item)">{{ props.item.State  }}</td>
          <td v-bind:class="classObject(props.item)" class="text-xs-right">{{ props.item.Count }}</td>
          <td class="pad9"><v-btn flat small class="small" @click="showDetails(props.item)"><v-icon>more_horiz</v-icon></v-btn></td>
        </tr>
      </template>
      <template slot="pageText" slot-scope="{ pageStart, pageStop }">
          From {{ pageStart }} to {{ pageStop }}
      </template>
    </v-data-table>

    <v-dialog v-model="details" max-width="800px" @keydown="editKeydown">
      <v-card>
          <v-card-title>
            <span class="headline">{{ detailItem.Severity }} Details</span>
          </v-card-title>
          <v-card-text>
            <table class="dataTable">
                <tr><td class="dataHead">Time&nbsp;First</td><td>&nbsp;</td>
                    <td class="dataBody">{{detailItem.TimeFirstLocal}}</td>
                </tr>
                <tr v-if="multi"><td class="dataHead">Time&nbsp;Last</td><td>&nbsp;</td>
                    <td class="dataBody">{{detailItem.TimeLastLocal + ' (total count: ' + detailItem.Count + ')'}}</td>
                </tr>
                <tr><td class="dataHead">Source</td><td>&nbsp;</td>
                  <td class="dataBody">{{detailItem.Source}}</td>
                </tr>
                <tr><td class="dataHead">Type</td><td>&nbsp;</td>
                  <td class="dataBody">{{detailItem.Type}}</td>
                </tr>
                <tr v-if="initiator.length > 0"><td class="dataHead">Inititator</td><td>&nbsp;</td>
                  <td class="dataBody">{{initiator}}</td>
                </tr>
                <tr><td class="dataHead">Message</td><td>&nbsp;</td>
                    <td class="dataBody">{{detailItem.Message}}</td>
                </tr>
                <tr><td class="dataHead">State</td><td>&nbsp;</td>
                  <td class="dataBody" style="white-space: pre-wrap;">{{state}}</td>
                </tr>
                <tr v-if="hasDetails"><td class="dataHead">Details</td><td>&nbsp;</td>
                    <td class="dataBody" style="white-space: pre-wrap;">{{detailItem.Details}}</td>
                </tr>
                <tr v-if="hasObjects"><td class="dataHead">Affected&nbsp;Objects</td><td>&nbsp;</td>
                  <td class="dataBody">{{objects}}</td>
              </tr>
            </table>
          </v-card-text>
          <v-card-actions>
            <v-spacer></v-spacer>
            <v-btn color="blue darken-1" flat @click.native="details = false">Close</v-btn>
          </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="showAckReset" max-width="600px" @keydown="editKeydown">
      <v-card>
          <v-card-title>
            <span class="headline">{{ ackResetTitel }}</span>
          </v-card-title>
          <v-card-text>
            <v-text-field label="Comment" ref="txtComment" v-model="comment"></v-text-field>
          </v-card-text>
          <v-card-actions>
            <v-spacer></v-spacer>
            <v-btn color="blue darken-1" flat @click.native="commitAckOrReset">OK</v-btn>
            <v-btn color="blue darken-1" flat @click.native="showAckReset = false">Cancel</v-btn>
          </v-card-actions>
      </v-card>
    </v-dialog>

  </div>
</template>

<script lang="ts">

import { Alarm } from './types'
import { Component, Vue, Watch, Prop } from 'vue-property-decorator'

@Component
export default class ActiveAlarms extends Vue {

  @Prop(Array) alarms!: Alarm[]

  search = ''
  selected = []
  rowsPerPageItems = [50, 100, 500, 1000, { text: 'Show All', value: -1 }]
  pagination = {
    sortBy: 'T',
    descending: true,
  }
  headers = [
    { text: 'Time (Local)', align: 'left',   sortable: true,  value: 'T'                    },
    { text: 'Message',      align: 'left',   sortable: true,  value: 'Msg'                  },
    { text: 'Source',       align: 'left',   sortable: true,  value: 'Source'               },
    { text: 'State',        align: 'left',   sortable: true,  value: 'State'                },
    { text: 'Count',        align: 'right',  sortable: true,  value: 'Count', width: '65px' },
    { text: 'Details',      align: 'center', sortable: false                                },
  ]
  details = false
  detailItem: any = {}
  showAckReset = false
  isACK = false
  comment = ''

  classObject(item) {
    return {
      bold:       item.State === 'New',
      ErrWarning: item.Severity === 'Warning',
      ErrAlarm:   item.Severity === 'Alarm',
    }
  }

  showDetails(item) {
    this.detailItem = item
    this.details = true
  }

  editKeydown(e) {
    if (e.keyCode === 27) {
      this.details = false
      this.showAckReset = false
    }
  }

  startAckOrReset(ack) {
    this.comment = ''
    this.isACK = ack
    this.showAckReset = true
    const context = this
    setTimeout(() => {
      const txt: any = context.$refs.txtComment
      txt.focus()
    }, 100)
  }

  commitAckOrReset() {
    const para = {
      Ack: this.isACK,
      Comment: this.comment,
      Timestamps: this.selected.map((it) => it.T),
    }
    this.$emit('ackreset', para)
    this.showAckReset = false
    this.selected = []
  }

  customFilter(items, search, filter, headers) {
    search = search.toString().toLowerCase()
    if (search.trim() === '') { return items }
    const words = search.split(' ').filter((w) => w !== '')
    const isFilterMatch = (val) => {
      const valLower = val.toLowerCase()
      return words.every((word) => valLower.indexOf(word) !== -1)
    }
    return items.filter((item) => isFilterMatch(item.Message + ' ' + item.Source))
  }

  get ackResetTitel() {
    const res = this.isACK ? 'Acknowledge ' : 'Reset '
    const alarms = this.selected.filter((it) => it.Severity === 'Alarm').length
    const warns  = this.selected.filter((it) => it.Severity === 'Warning').length
    let txtAlarm = ''
    if (alarms === 1) {
      txtAlarm = '1 Alarm'
    }
    if (alarms > 1) {
      txtAlarm = alarms + ' Alarms'
    }
    let txtWarn = ''
    if (warns === 1) {
      txtWarn = '1 Warning'
    }
    if (warns > 1) {
      txtWarn = warns + ' Warnings'
    }
    return res + txtAlarm + (alarms > 0 && warns > 0 ? ' and ' : '') + txtWarn
  }

  get ackEnabled() {
    return this.selected.length > 0 && this.selected.every((it) => it.State === 'New')
  }

  get resetEnabled() {
    return this.selected.length > 0
  }

  get hasDetails() {
    const details = this.detailItem.Details
    return details !== undefined && details.length > 0
  }

  get multi() {
    const count = this.detailItem.Count
    return count !== undefined && count > 1
  }

  get initiator() {
    const ini = this.detailItem.Inititator
    if (ini === undefined || ini === null) { return '' }
    return ini.Type + ' ' + ini.Name
  }

  get state() {
    const s = this.detailItem.State
    const ack = this.detailItem.InfoACK
    if (s === undefined) { return '' }
    if (s === 'Ack' && ack !== undefined && ack !== null) {
      let res = 'Acknowledged by ' + ack.UserName + ' at ' + this.detailItem.TimeAckLocal
      if (ack.Comment.length > 0) {
        res = res + '\r\nComment: ' + ack.Comment
      }
      return res
    }
    return s
  }

  get hasObjects() {
    const objs = this.detailItem.Objects
    return objs !== undefined && objs.length > 0
  }

  get objects() {
    const objs = this.detailItem.Objects
    if (objs === undefined) { return '' }
    return objs.join(', ')
  }
}

</script>

