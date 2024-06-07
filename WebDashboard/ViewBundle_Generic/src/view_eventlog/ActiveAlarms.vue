<template>
  <div>

    <v-toolbar>
      <v-btn text @click.stop="startAckOrReset(true)"  :disabled="!ackEnabled">Acknowledge</v-btn>
      <v-btn text @click.stop="startAckOrReset(false)" :disabled="!resetEnabled">Reset</v-btn>
      <v-spacer></v-spacer>
      <v-text-field append-icon="search" label="Search" single-line hide-details v-model="search"></v-text-field>
    </v-toolbar>

    <v-data-table :headers="headers" :items="alarms" :footer-props="footer"
                  sort-by="T" :sort-desc="true" :search="search" :custom-filter="customFilter"
                  no-data-text="No active warnings or alarms" class="elevation-4 mt-2"
                  v-model="selected" item-key="T" show-select must-sort>

      <template v-slot:item.TimeFirstLocal="{ item }">
        <span v-bind:class="classObject(item)">{{ item.TimeFirstLocal }}</span>
      </template>
      <template v-slot:item.TimeLastLocal="{ item }">
        <span v-bind:class="classObject(item)">{{ item.TimeLastLocal }}</span>
      </template>
      <template v-slot:item.Msg="{ item }">
        <span v-bind:class="classObject(item)">{{ item.Msg }}</span>
      </template>
      <template v-slot:item.Details="{ item }">
        <v-btn text small class="small" @click="showDetails(item)"><v-icon>more_horiz</v-icon></v-btn>
      </template>

    </v-data-table>

    <v-dialog v-model="details" v-if="detailItem" max-width="800px" @keydown="editKeydown">
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
            <v-btn color="blue darken-1" text @click.native="details = false">Close</v-btn>
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
          <v-btn color="grey darken-1" text @click.native="showAckReset = false">Cancel</v-btn>
          <v-btn color="primary darken-1" text @click.native="commitAckOrReset">OK</v-btn>
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
  footer = {
    showFirstLastPage: true,
    itemsPerPageOptions: [50, 100, 500, 1000, { text: 'All', value: -1 }],
  }
  headers = [
    { text: 'First',        align: 'left',   sortable: true,  filterable: false, value: 'TimeFirstLocal'        },
    { text: 'Last',         align: 'left',   sortable: true,  filterable: false, value: 'TimeLastLocal'         },
    { text: 'Message',      align: 'left',   sortable: true,  filterable: true,  value: 'Msg'                   },
    { text: 'Source',       align: 'left',   sortable: true,  filterable: false, value: 'Source'                },
    { text: 'State',        align: 'left',   sortable: true,  filterable: false, value: 'State'                 },
    { text: 'Count',        align: 'center', sortable: true,  filterable: false, value: 'Count', width: '94px'  },
    { text: 'Details',      align: 'center', sortable: false, filterable: false, value: 'Details'               },
  ]
  details = false
  detailItem: Alarm | null = null
  showAckReset = false
  isACK = false
  comment = ''

  classObject(item: Alarm) {
    return {
      bold:       item.State === 'New',
      ErrWarning: item.Severity === 'Warning' && item.RTN === false,
      ErrAlarm:   item.Severity === 'Alarm' && item.RTN === false,
    }
  }

  showDetails(item: Alarm) {
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

  customFilter(value: any, search: string | null, item: Alarm): boolean {
    if (search === null ) { return true }
    search = search.toLowerCase()
    const words = search.split(' ').filter((w) => w !== '')
    const valLower = (item.Message + ' ' + item.Source).toLowerCase()
    return words.every((word) => valLower.indexOf(word) !== -1)
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
    const ini = this.detailItem.Initiator
    if (ini === undefined || ini === null) { return '' }
    return ini.Type + ' ' + ini.Name
  }

  get state() {
    const s = this.detailItem.State
    const ack = this.detailItem.InfoACK
    if (s === undefined) { return '' }
    if (s === 'Ack' && ack) {
      let res = 'Acknowledged by ' + ack.UserName + ' at ' + this.detailItem.TimeAckLocal
      if (ack.Comment.length > 0) {
        res = res + '\r\nComment: ' + ack.Comment
      }
      return res
    }
    if (this.detailItem.RTN) {
      return 'Returned to normal at ' + this.detailItem.TimeRTNLocal
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

