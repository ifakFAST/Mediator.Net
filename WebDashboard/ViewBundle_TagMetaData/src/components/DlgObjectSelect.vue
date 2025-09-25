<template>
  <v-dialog
    v-model="value"
    max-width="790px"
    scrollable
    @keydown="(e: any) => e.key === 'Escape' && close()"
  >
    <v-card>
      <v-card-title>
        <span class="text-h5">Select object</span>
      </v-card-title>

      <v-card-text>
        <v-toolbar class="mt-1">
          <div style="display: flex; flex-wrap: wrap">
            <v-btn
              v-for="module in modules"
              :key="module.ID"
              :class="{ 'text-primary': currModuleID === module.ID }"
              :size="modules.length >= 4 ? 'small' : 'default'"
              variant="text"
              @click="refreshObjects(module.ID)"
            >
              {{ module.Name }}
            </v-btn>
          </div>

          <v-spacer />
          <v-text-field
            v-model="search"
            append-inner-icon="mdi-magnify"
            label="Search"
            single-line
          />
        </v-toolbar>

        <v-data-table
          v-model:model-value="selected"
          class="elevation-4 mt-2 mb-1"
          :custom-filter="customObjectFilter"
          density="compact"
          :footer-props="footer"
          :headers="headers"
          item-value="ID"
          :items="items"
          no-data-text="No relevant objects in selected module"
          :search="search"
          show-select
          single-select
        >
          <template #item.Type="{ item }">
            {{ getShortTypeName(item.Type) }}
          </template>
        </v-data-table>
      </v-card-text>

      <v-card-actions>
        <v-text-field
          v-if="allowConfigVariables && selected.length === 0"
          v-model="objIdWithVars"
          class="mt-0 pt-0 ml-3"
          label="Object ID with variables, e.g. IO:Tank_${Tank}"
        />
        <v-spacer />
        <v-btn
          color="grey-darken-1"
          variant="text"
          @click="close"
        >
          Cancel
        </v-btn>
        <v-btn
          color="primary-darken-1"
          :disabled="!isOK"
          variant="text"
          @click="selectObject_OK"
        >
          OK
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import type { DataTableHeader } from '@/utils'

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

const value = defineModel<boolean>({ required: true })

const props = defineProps<{
  objectId: string
  moduleId: string
  modules: ModuleInfo[]
  allowConfigVariables?: boolean
  type?: string
  filter?: string
}>()

const emit = defineEmits<{
  (e: 'update:value', value: boolean): void
  (e: 'onselected', obj: Obj): void
}>()

const currModuleID = ref(props.moduleId)
const selected = ref<Obj[]>([])
const search = ref('')
const items = ref<Obj[]>([])
const footer = {
  showFirstLastPage: true,
  itemsPerPageOptions: [10, 50, 100, 500, { text: 'All', value: -1 }],
}
const headers: Array<DataTableHeader> = [
  { title: 'Type', align: 'start', sortable: true, key: 'Type' },
  { title: 'Name', align: 'start', sortable: true, key: 'Name' },
  { title: 'ID', align: 'start', sortable: true, key: 'ID' },
]

const objIdWithVars = ref('')

const isOK = computed((): boolean => {
  return selected.value.length === 1 || (props.allowConfigVariables && objIdWithVars.value !== '' && objIdWithVars.value.trim().includes(':', 1))
})

const close = (): void => {
  currModuleID.value = props.moduleId
  value.value = false
}

const getShortTypeName = (fullTypeName: string): string => {
  if (!fullTypeName) return ''
  const i = fullTypeName.lastIndexOf('.')
  return i > 0 ? fullTypeName.substring(i + 1) : fullTypeName
}

watch(
  () => props.moduleId,
  (val) => {
    currModuleID.value = val
  },
)

watch(
  () => value,
  (val) => {
    if (val) {
      selected.value = []
      search.value = ''
      items.value = []
      refreshObjectsWithSelection(props.moduleId, props.objectId)
      objIdWithVars.value = ''
    }
  },
)

const refreshObjects = (modID: string): void => {
  currModuleID.value = modID
  refreshObjectsWithSelection(modID, '')
}

const refreshObjectsWithSelection = (modID: string, selectID: string): void => {
  const para = {
    ModuleID: modID,
    ForType: props.type,
    Filter: props.filter,
  }
  // @ts-ignore
  window.parent['dashboardApp'].sendViewRequest('ReadModuleObjects', para, (strResponse: string) => {
    const response = JSON.parse(strResponse)
    items.value = response.Items
    selected.value = response.Items.filter((it: Obj) => it.ID === selectID)
    if (selected.value.length === 0 && props.allowConfigVariables && selectID !== '') {
      objIdWithVars.value = selectID
      // setTimeout(() => {
      //   const txtObjIdWithVars = document.getElementById('txtObjIdWithVars') as HTMLInputElement
      //   if (txtObjIdWithVars) txtObjIdWithVars.focus()
      // }, 0)
    } else {
      // setTimeout(() => {
      //   const txtSearch = document.getElementById('txtSearch') as HTMLInputElement
      //   if (txtSearch) txtSearch.focus()
      // }, 0)
    }
  })
}

const selectObject_OK = (): void => {
  close()
  if (selected.value.length === 1) {
    emit('onselected', selected.value[0])
  } else if (props.allowConfigVariables && objIdWithVars.value !== '') {
    const obj: Obj = {
      Type: 'Object',
      ID: objIdWithVars.value,
      Name: objIdWithVars.value,
      Variables: ['Value'],
      Members: [],
    }
    emit('onselected', obj)
  }
  objIdWithVars.value = ''
}

const customObjectFilter = (value: string, search: string | null, item?: any): boolean => {
  if (search === null) return true
  if (item === undefined) return true
  const it: Obj = item.raw
  search = search.toLowerCase()
  const words = search.split(' ').filter((w) => w !== '')
  const valLower = (it.Type + ' ' + it.Name + ' ' + it.ID).toLowerCase()
  return words.every((word) => valLower.indexOf(word) !== -1)
}
</script>
