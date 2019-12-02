<template>
  <v-app>
    <v-content>
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

          <v-data-table :no-data-text="noDataText" :headers="headers" :items="items" :search="search"
                        :custom-filter="customFilter" class="elevation-4 mt-2" :footer-props="footer" >

              <template v-slot:item="{ item }">
                <tr>
                  <td>{{ item.Obj }}</td>
                  <td>{{ item.Var }}</td>
                  <td class="text-right" style="min-width: 12em">
                    <a @click.stop="edit(item)">{{ item.V }}</a>
                  </td>
                  <td v-bind:style="{ color: qualityColor(item.Q) }" class="text-right">{{ item.Q }}</td>
                  <td class="text-right">{{ item.T }}</td>
                </tr>
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
    </v-content>
  </v-app>
</template>

<script lang="ts">

import { Component, Vue } from 'vue-property-decorator'

interface VarEntry {
  ObjID: string
  Obj: string
  Var: string
  V: string
}

@Component
export default class ViewVariables extends Vue {

  loading = true
  search = ''
  modules = []
  selectedModuleID = ''
  items = []
  noDataText = 'No variables in selected module'
  footer = {
    showFirstLastPage: true,
    itemsPerPageOptions: [50, 100, 500, 1000, { text: 'All', value: -1 }],
  }
  editTmp = ''
  editDialog = false
  editItem: VarEntry = { ObjID: '', Obj: '', Var: '', V: ''}
  headers = [
      { text: 'Object Name', align: 'left',  sortable: false, filterable: true,  value: 'Obj' },
      { text: 'Variable',    align: 'left',  sortable: false, filterable: false, value: 'Var' },
      { text: 'Value',       align: 'right', sortable: false, filterable: false, value: 'V' },
      { text: 'Quality',     align: 'right', sortable: false, filterable: false, value: 'Q' },
      { text: 'Timestamp',   align: 'right', sortable: false, filterable: false, value: 'T' },
  ]

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

  edit(item: any) {
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
        context.loading = false
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

  .v-data-table-header th {
    font-size: 16px;
    font-weight: bold;
  }

  .v-data-table tbody td {
    font-size: 16px;
    height: auto;
    padding-top: 9px !important;
    padding-bottom: 9px !important;
  }

</style>
