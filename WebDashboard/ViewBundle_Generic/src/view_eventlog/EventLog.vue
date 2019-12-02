<template>
  <div>

    <v-toolbar>
      <v-checkbox label="Alarms"   hide-details class="mr-4" v-model="includeAlarms"></v-checkbox>
      <v-checkbox label="Warnings" hide-details class="mr-4" v-model="includeWarnings"></v-checkbox>
      <v-checkbox label="Info"     hide-details class="mr-4" v-model="includeInfos"></v-checkbox>
      <v-spacer></v-spacer>
      <v-text-field append-icon="search" label="Search" single-line hide-details v-model="search"></v-text-field>
    </v-toolbar>

    <v-data-table :headers="headers" :items="filteredEvents" :footer-props="footer" :search="search"
                  :custom-filter="customFilter" no-data-text="No events" class="elevation-4 mt-2"
                  sort-by="T" :sort-desc="true"
                  item-key="T" must-sort>
        <template v-slot:item="{ item }">
          <tr>
            <td v-bind:class="classObject(item)" style="white-space: nowrap">{{ item.TimeFirstLocal }}</td>
            <td v-bind:class="classObject(item)">{{ item.Severity  }}</td>
            <td v-bind:class="classObject(item)">{{ item.Msg  }}</td>
            <td v-bind:class="classObject(item)">{{ item.Source }}</td>
            <td v-bind:class="classObject(item)">{{ stateCol(item) }}</td>
            <td class="text-center"><v-btn text small class="small" @click="showDetails(item)"><v-icon>more_horiz</v-icon></v-btn></td>
          </tr>
        </template>
    </v-data-table>

    <v-dialog v-model="details" max-width="800px" @keydown="editKeydown">
        <v-card>
          <v-card-title>
            <span class="headline">{{ detailItem.Severity }} Details</span>
          </v-card-title>
          <v-card-text>
            <table class="dataTable">
              <tr><td class="dataHead">{{timeTitle}}</td><td>&nbsp;</td>
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
              <tr v-if="showState"><td class="dataHead">State</td><td>&nbsp;</td>
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
  </div>
</template>

<script lang="ts">

import { Alarm } from './types'
import { Component, Vue, Watch, Prop } from 'vue-property-decorator'

@Component
export default class EventLog extends Vue {

  @Prop(Array) events!: Alarm[]
  @Prop(Object) timeRange!: any

  search = ''
  includeAlarms = true
  includeWarnings = true
  includeInfos = true
  footer = {
    showFirstLastPage: true,
    itemsPerPageOptions: [50, 100, 500, 1000, { text: 'All', value: -1 }],
  }
  headers = [
    { text: 'Time (Local)', align: 'left',   sortable: true,  filterable: false,  value: 'T'        },
    { text: 'Type',         align: 'left',   sortable: true,  filterable: false,  value: 'Severity' },
    { text: 'Message',      align: 'left',   sortable: true,  filterable: true,   value: 'Msg'      },
    { text: 'Source',       align: 'left',   sortable: true,  filterable: false,  value: 'Source'   },
    { text: 'State',        align: 'left',   sortable: true,  filterable: false,  value: 'State'    },
    { text: 'Details',      align: 'center', sortable: false, filterable: false                     },
  ]
  details = false
  detailItem: any = {}

  classObject(item: Alarm) {
    return {
      bold:       item.State === 'New' && (item.Severity === 'Warning' || item.Severity === 'Alarm'),
      ErrWarning: item.Severity === 'Warning' && item.State !== 'Reset',
      ErrAlarm:   item.Severity === 'Alarm' && item.State !== 'Reset',
    }
  }

  showDetails(item) {
    this.detailItem = item
    this.details = true
  }

  editKeydown(e) {
    if (e.keyCode === 27) {
      this.details = false
    }
  }

  customFilter(value: any, search: string | null, item: Alarm) {
    if (search === null ) { return true }
    search = search.toLowerCase()
    const words = search.split(' ').filter((w) => w !== '')
    const valLower = (item.Message + ' ' + item.Source).toLowerCase()
    return words.every((word) => valLower.indexOf(word) !== -1)
  }

  stateCol(it) {
    if (it.Severity === 'Info') { return '' }
    return it.State
  }

  get filteredEvents() {
    const alarm = this.includeAlarms
    const warn = this.includeWarnings
    const info = this.includeInfos
    if (alarm && warn && info) {
      return this.events
    }
    return this.events.filter((it) => {
      const s = it.Severity
      return ((alarm && s === 'Alarm') || (warn && s === 'Warning') || (info && s === 'Info'))
    })
  }

  get timeTitle() {
    const count = this.detailItem.Count
    if (count === undefined || count === 1) { return 'Time' }
    return 'Time\u00A0First'
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

  get showState() {
    const s = this.detailItem.Severity
    if (s === undefined || s === 'Info') { return false }
    return true
  }

  get state() {
    const s = this.detailItem.State
    const ack = this.detailItem.InfoACK
    const reset = this.detailItem.InfoReset
    if (s === undefined) { return '' }
    if (s === 'Ack' && ack !== undefined && ack !== null) {
        let res = 'Acknowledged by ' + ack.UserName + ' at ' + this.detailItem.TimeAckLocal
        if (ack.Comment.length > 0) {
          res = res + '\r\nComment: ' + ack.Comment
        }
        return res
    }
    if (s === 'Reset' && reset !== undefined && reset !== null) {
        let res = 'Reset by ' + reset.UserName + ' at ' + this.detailItem.TimeResetLocal
        if (reset.Comment.length > 0) {
          res = res + '\r\nComment: ' + reset.Comment
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
