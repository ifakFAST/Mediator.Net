<template>
  <v-dialog v-model="value" scrollable max-width="790px" @keydown="(e) => { if (e.keyCode === 27) { close(); }}">
    <v-card>

        <v-card-title>
          <span class="headline">Select object</span>
        </v-card-title>

        <v-card-text>

          <v-toolbar >
              <v-select class="ml-4 mr-4" style="max-width: 12em" hide-details v-bind:items="modules" v-model="currModuleID" item-text="Name" item-value="ID"
                      @change="refreshObjects" label="Module" single-line menu-props="bottom"></v-select>
              <v-spacer></v-spacer>
              <v-text-field append-icon="search" label="Search" single-line hide-details v-model="search"></v-text-field>
          </v-toolbar>

          <v-data-table dense no-data-text="No relevant objects in selected module" :headers="headers" :items="items"
                        :search="search" :custom-filter="customObjectFilter" class="elevation-4 mt-2"
                        show-select single-select :footer-props="footer" item-key="ID" v-model="selected">

              <template #[`item.Type`]="{ item }">
                {{ getShortTypeName(item.Type) }}
              </template>

          </v-data-table>

        </v-card-text>

        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn color="grey darken-1"  text @click.native="close">Cancel</v-btn>
          <v-btn color="primary darken-1" text @click.native="selectObject_OK" :disabled="!isSelectObjectOK">OK</v-btn>
        </v-card-actions>

    </v-card>
  </v-dialog>
</template>


<script lang="ts">

import { Component, Vue, Watch, Prop } from 'vue-property-decorator'

interface ModuleInfo {
  ID: string
  Name: string
}

interface Obj {
  Type: string
  ID: string
  Name: string
  Variables?: string[]
  Members?: string[]
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
  @Prop({ type: String, default() { return 'Float64'       } }) type: string
  @Prop({ type: String, default() { return 'WithVariables' } }) filter: string

  currModuleID: string = ''
  selected: Obj[] = []
  search = ''
  items: Obj[] = []
  footer = {
    showFirstLastPage: true,
    itemsPerPageOptions: [10, 50, 100, 500, { text: 'All', value: -1 }],
  }
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
    const para = {
      ModuleID: modID,
      ForType: this.type,
      Filter: this.filter,
    }
    window.parent['dashboardApp'].sendViewRequest('ReadModuleObjects', para, (strResponse) => {
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

  customObjectFilter(value: any, search: string | null, item: Obj): boolean {
    if (search === null ) { return true }
    search = search.toLowerCase()
    const words = search.split(' ').filter((w) => w !== '')
    const valLower = (item.Type + ' ' + item.Name + ' ' + item.ID).toLowerCase()
    return words.every((word) => valLower.indexOf(word) !== -1)
  }

}

</script>