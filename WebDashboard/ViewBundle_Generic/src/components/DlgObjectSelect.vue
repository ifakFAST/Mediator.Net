<template>
  <v-dialog v-model="value" scrollable max-width="720px" @keydown="(e) => { if (e.keyCode === 27) { close(); }}">
    <v-card>

        <v-card-title>
          <span class="headline">Select object</span>
        </v-card-title>

        <v-card-text>

          <v-toolbar >
              <v-select class="ml-4 mr-4" solo hide-details v-bind:items="modules" v-model="currModuleID" item-text="Name" item-value="ID"
                      @change="refreshObjects" label="Module" single-line bottom></v-select>
              <v-spacer></v-spacer>
              <v-text-field append-icon="search" label="Search" single-line hide-details v-model="search"></v-text-field>
          </v-toolbar>

          <v-data-table no-data-text="No objects in selected module" :headers="headers" :items="items"
                          :search="search" :custom-filter="customObjectFilter" class="elevation-4 mt-2"
                          :rows-per-page-items="rowsPerPageItems" select-all
                          item-key="ID" v-model="selected">

              <template slot="items" slot-scope="props">
                <td><v-checkbox primary hide-details v-model="props.selected"></v-checkbox></td>
                <td>{{ getShortTypeName(props.item.Type) }}</td>
                <td>{{ props.item.Name }}</td>
                <td>{{ props.item.ID }}</td>
              </template>
              <template slot="pageText" slot-scope="{ pageStart, pageStop }">
                From {{ pageStart }} to {{ pageStop }}
              </template>
          </v-data-table>

        </v-card-text>

        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn color="red darken-1"  flat @click.native="close">Cancel</v-btn>
          <v-btn color="blue darken-1" flat @click.native="selectObject_OK" :disabled="!isSelectObjectOK">OK</v-btn>
        </v-card-actions>

    </v-card>
  </v-dialog>
</template>


<script lang="ts">

import DyGraph from '../components/DyGraph.vue'
import { Component, Vue, Watch, Prop } from 'vue-property-decorator'

interface ModuleInfo {
  ID: string
  Name: string
}

interface Obj {
  Type: string
  ID: string
  Name: string
  Variables: string[]
}

@Component({
  components: {
  },
})
export default class DlgObjectSelect extends Vue {

  @Prop(Boolean) value: boolean
  @Prop(String)  objectId: string
  @Prop(String)  moduleId: string
  @Prop(Array)   modules: ModuleInfo[]

  currModuleID: string = this.moduleId
  selected: Obj[] = []
  search = ''
  items: Obj[] = []

  pagination = {}
  rowsPerPageItems = [10, 50, 100, 500, { text: 'All Items', value: -1 }]
  headers = [
    { text: 'Type', align: 'left', sortable: true, value: 'Type' },
    { text: 'Name', align: 'left', sortable: true, value: 'Name' },
    { text: 'ID',   align: 'left', sortable: true, value: 'ID' },
  ]

  @Watch('moduleId')
  watch_moduleId(val: string, oldVal: string) {
    this.currModuleID = val
  }

  get isSelectObjectOK() {
    return this.selected.length === 1
  }

  close() {
    this.currModuleID = this.moduleId
    this.$emit('input', false)
  }

  getShortTypeName(fullTypeName: string) {
    if (fullTypeName === undefined) { return '' }
    const i = fullTypeName.lastIndexOf('.')
    if (i > 0) {
      return fullTypeName.substring(i + 1)
    }
    return fullTypeName
  }

  @Watch('value')
  watch_value(val: boolean, oldVal: boolean) {
    if (val && !oldVal) {
      this.selected = []
      this.search = ''
      this.items = []
      this.refreshObjectsWithSelection(this.moduleId, this.objectId)
    }
  }

  refreshObjects(modID: string) {
    this.refreshObjectsWithSelection(modID, '')
  }

  refreshObjectsWithSelection(modID: string, selectID: string) {
    const context = this
    window.parent['dashboardApp'].sendViewRequest('ReadModuleObjects', { ModuleID: modID }, (strResponse) => {
      const response = JSON.parse(strResponse)
      context.items = response.Items
      context.selected = response.Items.filter((it: Obj) => it.ID === selectID)
    })
  }

  selectObject_OK() {
    this.close()
    const selected: Obj[] = this.selected
    if (selected.length === 1) {
      const obj: Obj = selected[0]
      this.$emit('onselected', obj)
    }
  }

  @Watch('selected')
  watch_selectObject_selected(val: Obj[], oldVal: Obj[]) {

    if (val.length === 2 && oldVal.length === 1) {
      if (val[0] === oldVal[0]) {
        this.selected = [val[1]]
      }
      else {
        this.selected = [val[0]]
      }
    }
    else if (val.length > 1) {
      const last = val[val.length - 1]
      this.selected = [last]
    }
  }

  customObjectFilter(items: Obj[], search: string, filter, headers) {
    search = search.toLowerCase()
    if (search.trim() === '') { return items }
    const words: string[] = search.split(' ').filter((w) => w !== '')
    const isFilterMatch = (val: string) => {
      const valLower = val.toLowerCase()
      return words.every((word) => valLower.indexOf(word) !== -1)
    }
    return items.filter((item: Obj) => isFilterMatch(item.Type + ' ' + item.Name + ' ' + item.ID))
  }

}

</script>