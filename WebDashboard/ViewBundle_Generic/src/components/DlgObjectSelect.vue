<template>
  <v-dialog v-model="value" scrollable max-width="790px" @keydown="(e) => { if (e.keyCode === 27) { close(); }}">
    <v-card>

        <v-card-title>
          <span class="headline">Select object</span>
        </v-card-title>

        <v-card-text>

          <v-toolbar class="mt-1">

            <div style="display: flex; flex-wrap: wrap;">
                <v-btn :small="modules.length >= 4" v-for="module in modules" text :key="module.ID" @click="currModuleID = module.ID; refreshObjects(module.ID)" :class="{ 'primary--text': currModuleID === module.ID }">
                {{ module.Name }}
                </v-btn>
            </div>

            <v-spacer></v-spacer>
            <v-text-field ref="txtSearch" append-icon="search" label="Search" single-line hide-details v-model="search"></v-text-field>
          </v-toolbar>

          <v-data-table dense no-data-text="No relevant objects in selected module" :headers="headers" :items="items"
                        :search="search" :custom-filter="customObjectFilter" class="elevation-4 mt-2 mb-1"
                        show-select single-select :footer-props="footer" item-key="ID" v-model="selected">

              <template #[`item.Type`]="{ item }">
                {{ getShortTypeName(item.Type) }}
              </template>

          </v-data-table>

        </v-card-text>

        <v-card-actions>
          <v-text-field ref="txtObjIdWithVars" class="mt-0 pt-0 ml-3" v-if="allowConfigVariables && selected.length === 0" v-model="objIdWithVars" label="Object ID with variables, e.g. IO:Tank_${Tank}" hide-details></v-text-field>
          <v-spacer></v-spacer>
          <v-btn color="grey darken-1"  text @click.native="close">Cancel</v-btn>
          <v-btn color="primary darken-1" text @click.native="selectObject_OK" :disabled="!isOK">OK</v-btn>
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
  @Prop({ type: Boolean, default() { return false           } }) allowConfigVariables: boolean
  @Prop({ type: String,  default() { return 'Float64'       } }) type: string
  @Prop({ type: String,  default() { return 'WithVariables' } }) filter: string

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

  objIdWithVars: string = ''

  @Watch('moduleId')
  watch_moduleId(val: string, oldVal: string) {
    this.currModuleID = val
  }

  get isOK() {
    return this.selected.length === 1 || 
      (this.allowConfigVariables && this.objIdWithVars !== '' && this.objIdWithVars.trim().includes(':', 1))
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
      this.objIdWithVars = ''
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
      if (context.selected.length === 0 && context.allowConfigVariables && selectID !== '') {
        context.objIdWithVars = selectID
        context.$nextTick(() => {
          if (this.$refs.txtObjIdWithVars) {
            (this.$refs.txtObjIdWithVars as HTMLInputElement).focus()
          }
        })
      }
      else {
        context.$nextTick(() => {
          if (this.$refs.txtSearch) {
            (this.$refs.txtSearch as HTMLInputElement).focus()
          }
        })
      }
    })
  }

  selectObject_OK() {
    this.close()
    const selected: Obj[] = this.selected
    if (selected.length === 1) {
      const obj: Obj = selected[0]
      this.$emit('onselected', obj)
    }
    else if (this.allowConfigVariables && this.objIdWithVars !== '') {
      const objIdWithVars = this.objIdWithVars
      const obj: Obj = {
        Type: 'Object',
        ID: objIdWithVars,
        Name: objIdWithVars,
        Variables: ["Value"],
        Members: [],
      }
      this.$emit('onselected', obj)
    }
    this.objIdWithVars = ''
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