<template>
  <v-app>
    <v-main>
      <v-container>

        <div v-if="loading">
          <p class="text-center">Loading ...</p>
        </div>

        <div v-else>
          <v-toolbar>
              <v-toolbar-title>Module</v-toolbar-title>
              <v-select class="ml-4 mr-4" style="max-width: 20em" solo hide-details v-bind:items="modules" v-model="selectedModuleID" item-text="Name" item-value="ID"
                @change='refreshVariables' label="Module" single-line menu-props="bottom"></v-select>
              <v-btn @click.stop="refresh">Refresh</v-btn>
              <v-spacer></v-spacer>
              <v-text-field class="ml-4" append-icon="search" label="Search" single-line hide-details v-model="search"></v-text-field>
          </v-toolbar>

          <locations v-if="locations.length > 0" v-model="selectedLocations" :locations="locations"></locations>

          <v-data-table :no-data-text="noDataText" :headers="headers" :items="locFilteredItems" :search="search"
                        :custom-filter="customFilter" class="elevation-4 mt-2" :footer-props="footer"
                        show-expand :expanded.sync="expanded" :single-expand="false" item-key="ID">

              <template v-slot:item.V="{ item }">
                <div style="min-width: 12em; word-break: break-all;">
                  <a v-if="item.Writable" @click.stop="edit(item)">{{ limitText(item) }}</a>
                  <div v-else>{{ limitText(item) }}</div>
                </div>
              </template>

              <template v-slot:item.Q="{ item }">
                <div v-bind:style="{ color: qualityColor(item.Q) }">{{ item.Q }}</div>
              </template>

              <template v-slot:item.data-table-expand="{ item, isExpanded, expand  }">
                <v-icon v-if="!isExpanded && itemNeedsExapnd(item)" @click="expand(true)" >mdi-chevron-down</v-icon>
                <v-icon v-if="isExpanded  && itemNeedsExapnd(item)" @click="expand(false)">mdi-chevron-up</v-icon>
              </template>

              <template v-slot:item.sync-read="{ item }">
                <v-icon v-if="item.SyncReadable" @click="syncRead(item)">mdi-refresh</v-icon>
              </template>

              <template v-slot:expanded-item="{ headers, item }">
                <td :colspan="headers.length">
                  <struct-view v-if="item.Type==='Struct'" style="float: right;" :value="item.V" :vertical="item.Dimension !== 1"></struct-view>
                  <div v-else style="word-break: break-all;">
                    {{ item.V }}
                  </div>
                </td>
              </template>

          </v-data-table>

          <v-dialog v-model="editDialog" max-width="290" @keydown="editKeydown">
              <v-card>
                <v-card-title class="headline">Write Variable Value</v-card-title>
                <v-card-text>
                <v-text-field ref="editTextInput" label="Edit"  v-model="editTmp" single-line autofocus clearable :rules="[validateVariableValue]"></v-text-field>
                </v-card-text>
                <v-card-actions>
                  <v-spacer></v-spacer>
                  <v-btn color="grey darken-1" text @click.stop="editDialog = false">Cancel</v-btn>
                  <v-btn color="primary darken-1" text @click.stop="editWrite">Write</v-btn>
                </v-card-actions>
              </v-card>
            </v-dialog>
        </div>

      </v-container>
    </v-main>
  </v-app>
</template>

<script lang="ts">

import { Component, Vue } from 'vue-property-decorator'
import * as fast from '../fast_types'
import StructView from '../components/StructView.vue'
import Locations from '../components/Locations.vue'

interface VarEntry {
  ID: string
  ObjID: string
  Obj: string
  Loc: string
  Var: string
  Type: fast.DataType
  Dimension: number
  Writable: boolean
  SyncReadable: boolean
  V: string
  T: fast.Timestamp
  Q: fast.Quality
}

@Component({
  components: {
    StructView,
    Locations,
  },
})
export default class ViewVariables extends Vue {

  expanded: string[] = []
  loading = true
  search = ''
  modules = []
  selectedModuleID = ''
  items: VarEntry[] = []
  noDataText = 'No variables in selected module'
  footer = {
    showFirstLastPage: true,
    itemsPerPageOptions: [50, 100, 500, 1000, { text: 'All', value: -1 }],
  }
  editTmp = ''
  editDialog = false
  editItem: VarEntry = { ID: '', ObjID: '', Obj: '', Loc: '', Var: '', Type: 'JSON',
                         Dimension: 1, V: '', T: '', Q: 'Bad', Writable: false, SyncReadable: false }
  headers = [
      { text: 'Object Name', align: 'left',  sortable: false, filterable: true,  value: 'Obj' },
      { text: 'Variable',    align: 'left',  sortable: false, filterable: false, value: 'Var' },
      { text: 'Value',       align: 'right', sortable: false, filterable: false, value: 'V' },
      { text: 'Quality',     align: 'right', sortable: false, filterable: false, value: 'Q' },
      { text: 'Timestamp',   align: 'right', sortable: false, filterable: false, value: 'T' },
      { text: '', value: 'data-table-expand' },
      { text: '', value: 'sync-read' },
  ]

  locations: fast.LocationInfo[] = []
  mapLocations: Map<string, fast.LocationInfo> = new Map()
  selectedLocations: string[][] = [ [], [], [], [], [], [] ]

  maxValueLen = 60

  limitText(item: VarEntry): string {
    const str = item.V
    const MaxLen = this.maxValueLen
    if (str.length > MaxLen) {
      return str.substring(0, MaxLen) + '\u00A0...'
    }
    else {
      return str
    }
  }

  itemNeedsExapnd(item: VarEntry): boolean {
    return item.Type === 'Struct' || item.V.length > this.maxValueLen
  }

  get locFilteredItems(): VarEntry[] {
    if (this.locations.length === 0) { return this.items }
    if (this.selectedLocations[1].length === 0) { return this.items }
    const set = this.setOfSelectedLocationIDs
    return this.items.filter((it) => set.has(it.Loc))
  }

  get setOfSelectedLocationIDs(): Set<string> {
    const locations = this.locations
    const res = new Set<string>()
    const addTree = (rootID: string) => {
      const set = new Set<string>()
      set.add(rootID)
      res.add(rootID)
      while (true) {
        const newChildren = locations.filter((loc) => set.has(loc.Parent) && !set.has(loc.ID))
        if (newChildren.length === 0) { break }
        for (const ch of newChildren) {
          set.add(ch.ID)
          res.add(ch.ID)
        }
      }
    }
    const selectedLocations: string[][] = this.selectedLocations
    const mapLocations = this.mapLocations
    for (let level = 0; level < selectedLocations.length; ++level) {
      const potentialChildrenIDs = (level < selectedLocations.length - 1 ? selectedLocations[level + 1] : [])
      const potentialChildren = potentialChildrenIDs.filter((id) => mapLocations.has(id)).map((id) => mapLocations.get(id)!)
      for (const locID of selectedLocations[level]) {
        if (potentialChildren.every((child) => child.Parent !== locID)) {
          addTree(locID)
        }
      }
    }
    return res
  }

  customFilter(value: any, search: string | null, item: VarEntry) {
    if (search === null ) { return true }
    search = search.toLowerCase()
    const words = search.split(' ').filter((w) => w !== '')
    const valLower = (item.Obj + ' ' + item.Var + ' ' + item.V).toLowerCase()
    return words.every((word) => valLower.indexOf(word) !== -1)
  }

  validateVariableValue(v: any) {
    if (v === '') { return 'No empty value allowed.' }
    try {
      JSON.parse(v)
      return true
    }
    catch (error) {
      return 'Not valid JSON'
    }
  }

  edit(item: VarEntry) {
    this.editItem = item
    this.editTmp = item.V
    this.editDialog = true
    const context = this
    const doFocus = () => {
      const txt: any = context.$refs.editTextInput
      txt.focus()
    }
    setTimeout(doFocus, 100)
  }

  editWrite() {
    this.editDialog = false
    this.writeVariable(this.editItem.ObjID, this.editItem.Var, this.editTmp)
  }

  syncRead(item: VarEntry) {
    const params = {
      ObjID: item.ObjID,
      Var: item.Var,
    }
    const context = this
    window.parent['dashboardApp'].sendViewRequest('SyncRead', params, (strResponse: string) => {
      const response = JSON.parse(strResponse)
      const variable = context.items[response.N]
      variable.V = response.V
      variable.T = response.T
      variable.Q = response.Q
    })
  }

  editKeydown(e: any) {
      if (e.keyCode === 27) {
        this.editDialog = false
      }
      else if (e.keyCode === 13) {
        this.editWrite()
      }
  }

  refresh(event: any) {
      this.refreshVariables(this.selectedModuleID)
  }

  refreshVariables(moduleID: string) {
      const context = this
      window.parent['dashboardApp'].sendViewRequest('ReadModuleVariables', { ModuleID: moduleID }, (strResponse: string) => {
        const response = JSON.parse(strResponse)
        context.modules = response.Modules
        context.selectedModuleID = response.ModuleID
        context.items = response.Variables
        context.locations = response.Locations
        context.loading = false
        if (context.selectedLocations[0].length === 0) {
          const locationRootID = context.locations.find((loc) => loc.Parent === '' || loc.Parent === null)?.ID || ''
          context.selectedLocations[0].push(locationRootID)
        }
        context.mapLocations.clear()
        context.locations.forEach((loc) => {
          context.mapLocations.set(loc.ID, loc)
        })
      })
  }

  qualityColor(q: string) {
      if (q === 'Good') { return 'green' }
      if (q === 'Uncertain') { return 'orange' }
      return 'red'
  }

  writeVariable(objID: any, varName: string, newVal: any) {
      try {
        JSON.parse(newVal)
        const params = { ObjID: objID, Var: varName, V: newVal }
        window.parent['dashboardApp'].sendViewRequest('WriteVariable', params, (strResponse: string) => { })
      }
      catch (error) {
        console.log('Bad JSON: ' + error)
      }
  }

  mounted() {
    this.refreshVariables('')
    const context = this
    window.parent['dashboardApp'].registerViewEventListener( (eventName: string, eventPayload: any) => {
        if (eventName === 'Change') {
          const len = eventPayload.length
          const items = context.items
          for (let i = 0; i < len; i++) {
              const entry = eventPayload[i]
              const variable = items[entry.N]
              variable.V = entry.V
              variable.T = entry.T
              variable.Q = entry.Q
          }
        }
    })
  }

}
</script>


<style>

  .v-data-table > .v-data-table__wrapper > table > thead > tr > th {
    font-size: 16px;
    font-weight: bold;
  }

  .v-data-table > .v-data-table__wrapper > table > tbody > tr > td {
    font-size: 16px;
    height: auto;
    padding-top: 9px !important;
    padding-bottom: 9px !important;
  }

</style>
