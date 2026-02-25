<template>
  <v-container
    fluid
    class="pa-2"
  >
    <v-row>
      <v-col class="pa-2">
        <v-toolbar
          v-if="!hideHeader"
          class="mb-3"
          density="compact"
          flat
        >
          <a
            v-for="page in pages"
            :key="page.ID"
            :class="classObject(page.ID)"
            @click="switchPage(page.ID)"
          >
            {{ page.Name }}
          </a>
          <v-spacer></v-spacer>
          <v-btn
            v-if="editPage"
            icon="mdi-check"
            @click="editPage = false"
          ></v-btn>
          <v-menu
            v-if="canUpdateConfig"
            location="bottom left"
            offset="8"
          >
            <template #activator="{ props }">
              <v-btn
                v-bind="props"
                icon="mdi-dots-vertical"
              ></v-btn>
            </template>
            <v-list>
              <v-list-item @click="page_Add">
                <v-list-item-title>Add new Page</v-list-item-title>
              </v-list-item>
              <v-list-item
                v-if="hasPage"
                @click="page_Rename"
              >
                <v-list-item-title>Rename Page</v-list-item-title>
              </v-list-item>
              <v-list-item
                v-if="hasPage"
                @click="page_Duplicate"
              >
                <v-list-item-title>Duplicate Page</v-list-item-title>
              </v-list-item>
              <v-list-item
                v-if="canPageMoveLeft"
                @click="page_MoveLeft"
              >
                <v-list-item-title>Move Left</v-list-item-title>
              </v-list-item>
              <v-list-item
                v-if="canPageMoveRight"
                @click="page_MoveRight"
              >
                <v-list-item-title>Move Right</v-list-item-title>
              </v-list-item>
              <v-list-item
                v-if="hasPage"
                @click="page_Delete"
              >
                <v-list-item-title>Delete Page</v-list-item-title>
              </v-list-item>
              <v-list-item
                v-if="hasPage"
                @click="editPage = !editPage"
              >
                <v-list-item-title>{{ editPage ? 'Fix Page Layout' : 'Edit Page Layout' }}</v-list-item-title>
              </v-list-item>
              <v-list-item @click="editConfigVariables">
                <v-list-item-title>Edit Config Variables</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-menu>
        </v-toolbar>

        <div
          v-if="hideHeader"
          class="mb-2"
        ></div>

        <page
          v-if="currentPage"
          :config-variables="configVariableValues"
          :date-window="dateWindow"
          :edit-page="editPage"
          :page="currentPage"
          :set-config-variable-values="setConfigVariableValues"
          :widget-types="widgetTypes"
          @configchanged="onPageConfigChanged"
          @date-window-changed="onDateWindowChanged"
        ></page>
      </v-col>
    </v-row>
  </v-container>

  <confirm ref="confirm"></confirm>
  <dlg-text-input ref="textInput"></dlg-text-input>
  <dlg-config-variables ref="dlgConfigVariables"></dlg-config-variables>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import DlgTextInput from './DlgTextInput.vue'
import DlgConfigVariables from './DlgConfigVariables.vue'
import Confirm from '../components/Confirm.vue'
import Page from './Page.vue'
import * as model from './model'
import * as utils from '../utils'

const configVariableValues = ref<model.ConfigVariableValues>({ VarDefs: [], VarValues: {} })
const hideHeader = ref(false)
const pages = ref<model.Page[]>([])
const currentPageID = ref('')
const widgetTypes = ref<string[]>([])
const widgetMap = ref(new Map<string, model.Widget>())
const editPage = ref(false)
const dateWindow = ref<number[] | null>(null)
const canUpdateConfig = ref(false)

const confirm = ref()
const textInput = ref()
const dlgConfigVariables = ref()

onMounted(() => {
  ;(window.parent as any).dashboardApp.registerViewEventListener((eventName: string, eventPayload: any) => {
    if (eventName === 'WidgetEvent') {
      const pageID: string = eventPayload.PageID
      if (pageID === currentPageID.value) {
        const widgetID: string = eventPayload.WidgetID
        const event: string = eventPayload.EventName
        const content: object = eventPayload.Content
        const widget = widgetMap.value.get(widgetID)
        if (widget) {
          widget.EventName = event
          widget.EventPayload = content
        }
      }
    } else if (eventName === 'WidgetConfigChanged') {
      const pageID: string = eventPayload.PageID
      if (pageID === currentPageID.value) {
        const widgetID: string = eventPayload.WidgetID
        const config: object = eventPayload.Config
        const widget = widgetMap.value.get(widgetID)
        if (widget) {
          widget.Config = config
        }
      }
    }
  })
  canUpdateConfig.value = (window.parent as any).dashboardApp.canUpdateViewConfig()
  loadAllPages()
})

const setConfigVariableValues = (newValues: Record<string, string>): void => {
  const updatedKeys = Object.keys(newValues).filter((key) => {
    const oldValue: string = configVariableValues.value.VarValues[key]
    return oldValue !== undefined && oldValue !== newValues[key]
  })

  if (updatedKeys.length === 0) {
    console.info('No config variable values changed')
    return
  }

  updatedKeys.forEach((key) => {
    console.info(`Config variable value changed: ${key} = ${newValues[key]}`)
    configVariableValues.value.VarValues[key] = newValues[key]
  })
  configVariableValues.value = {
    VarDefs: configVariableValues.value.VarDefs,
    VarValues: configVariableValues.value.VarValues,
  }
}

const loadAllPages = (): void => {
  ;(window.parent as any).dashboardApp.sendViewRequest('Init', {}, (strResponse: string) => {
    const response = JSON.parse(strResponse)
    pages.value = response.configuration.Pages
    configVariableValues.value.VarDefs = response.configuration.ConfigVariables
    hideHeader.value = response.configuration.HideHeader
    initVarValuesFromVarDefs()
    widgetTypes.value = response.widgetTypes
    currentPageID.value = pages.value.length > 0 ? pages.value[0].ID : ''
    updateWidgetMap()
  })
}

const switchPage = (newPageID: string): void => {
  ;(window.parent as any).dashboardApp.sendViewRequest('SwitchToPage', { pageID: newPageID }, (strResponse: string) => {
    currentPageID.value = newPageID
    updateWidgetMap()
  })
}

const onPageConfigChanged = (page: model.Page): void => {
  const pageID = page.ID
  if (pageID !== currentPageID.value) {
    return
  }
  const i = pages.value.findIndex((p) => p.ID === pageID)
  if (i < 0) {
    return
  }
  pages.value[i] = page
  updateWidgetMap()
}

const onDateWindowChanged = (window: number[] | null): void => {
  if (window === null && dateWindow.value === null) {
    return
  }
  if (window !== null && dateWindow.value !== null && window[0] === dateWindow.value[0] && window[1] === dateWindow.value[1]) {
    return
  }

  console.info('ViewPages.onDateWindowChanged !!!')
  dateWindow.value = window
}

const updateWidgetMap = (): void => {
  widgetMap.value.clear()
  const page = currentPage.value
  if (page === null) {
    return
  }
  for (const row of page.Rows) {
    for (const col of row.Columns) {
      for (const w of col.Widgets) {
        widgetMap.value.set(w.ID, w)
        w.EventName = ''
        w.EventPayload = {}
      }
    }
  }
}

const classObject = (pageID: string) => {
  const sel = currentPageID.value === pageID
  return {
    selectedpage: sel,
    nonselectedpage: !sel,
  }
}

const page_Add = async (): Promise<void> => {
  const title = await textInputDlg('New Page Name', 'Define the name of the new page', 'Page')
  if (title === null) {
    return
  }
  const para = {
    pageID: utils.findUniqueID('P', 4, getAllPageIDs()),
    title,
  }
  try {
    const pagesResponse: model.Page[] = await (window.parent as any).dashboardApp.sendViewRequestAsync('ConfigPageAdd', para)
    pages.value = pagesResponse
    editPage.value = true
    switchPage(para.pageID)
  } catch (err) {
    const exp = err as Error
    alert(exp.message)
  }
}

const page_Rename = async (): Promise<void> => {
  const page = currentPage.value
  if (page === null) {
    return
  }
  const title = await textInputDlg('Rename Page', 'Define the new name of the page', page.Name)
  if (title === null) {
    return
  }
  const para = {
    pageID: page.ID,
    title,
  }
  try {
    await (window.parent as any).dashboardApp.sendViewRequestAsync('ConfigPageRename', para)
    page.Name = title
  } catch (err) {
    const exp = err as Error
    alert(exp.message)
  }
}

const page_MoveLeft = async (): Promise<void> => {
  const page = currentPage.value
  if (page === null) {
    return
  }
  const pageIdx = pages.value.findIndex((p) => p.ID === page.ID)
  if (pageIdx < 0) {
    return
  }

  try {
    await (window.parent as any).dashboardApp.sendViewRequestAsync('ConfigPageMoveLeft', { pageID: page.ID })

    const arr = pages.value
    const tmp = arr[pageIdx]
    arr[pageIdx] = arr[pageIdx - 1]
    arr[pageIdx - 1] = tmp
  } catch (err) {
    const exp = err as Error
    alert(exp.message)
  }
}

const page_MoveRight = async (): Promise<void> => {
  const page = currentPage.value
  if (page === null) {
    return
  }
  const pageIdx = pages.value.findIndex((p) => p.ID === page.ID)
  if (pageIdx < 0) {
    return
  }

  try {
    await (window.parent as any).dashboardApp.sendViewRequestAsync('ConfigPageMoveRight', { pageID: page.ID })

    const arr = pages.value
    const tmp = arr[pageIdx]
    arr[pageIdx] = arr[pageIdx + 1]
    arr[pageIdx + 1] = tmp
  } catch (err) {
    const exp = err as Error
    alert(exp.message)
  }
}

const page_Duplicate = async (): Promise<void> => {
  const page = currentPage.value
  if (page === null) {
    return
  }
  const para = {
    pageID: page.ID,
    newPageID: utils.findUniqueID('P', 4, getAllPageIDs()),
    title: 'Copy of ' + page.Name,
  }
  try {
    const pagesResponse: model.Page[] = await (window.parent as any).dashboardApp.sendViewRequestAsync('ConfigPageDuplicate', para)
    pages.value = pagesResponse
    switchPage(para.pageID)
  } catch (err) {
    const exp = err as Error
    alert(exp.message)
  }
}

const page_Delete = async (): Promise<void> => {
  const page = currentPage.value
  if (page === null) {
    return
  }
  const pageIdx = pages.value.findIndex((p) => p.ID === page.ID)
  if (pageIdx < 0) {
    return
  }
  const pageCount = pages.value.length

  if (!(await confirmDelete('Delete Page?', `Do you really want to delete the page ${page.Name}?`))) {
    return
  }

  try {
    await (window.parent as any).dashboardApp.sendViewRequestAsync('ConfigPageDelete', { pageID: page.ID })

    pages.value.splice(pageIdx, 1)

    let newPageID = ''
    if (pageCount === 1) {
      newPageID = ''
    } else if (pageIdx === pageCount - 1) {
      newPageID = pages.value[pageIdx - 1].ID
    } else {
      newPageID = pages.value[pageIdx].ID
    }
    switchPage(newPageID)
  } catch (err) {
    const exp = err as Error
    alert(exp.message)
  }
}

const getAllPageIDs = (): Set<string> => {
  const set = new Set<string>()
  for (const page of pages.value) {
    set.add(page.ID)
  }
  return set
}

const textInputDlg = async (title: string, message: string, value: string): Promise<string | null> => {
  return textInput.value.open(title, message, value)
}

const currentPage = computed((): model.Page | null => {
  const i = pages.value.findIndex((p) => p.ID === currentPageID.value)
  if (i < 0) {
    return null
  }
  return pages.value[i]
})

const hasPage = computed((): boolean => {
  return pages.value.length > 0
})

const canPageMoveLeft = computed((): boolean => {
  if (pages.value.length === 0) {
    return false
  }
  const i = pages.value.findIndex((p) => p.ID === currentPageID.value)
  return i > 0
})

const canPageMoveRight = computed((): boolean => {
  if (pages.value.length === 0) {
    return false
  }
  const i = pages.value.findIndex((p) => p.ID === currentPageID.value)
  return i < pages.value.length - 1
})

const confirmBox = async (title: string, message: string, color?: string): Promise<boolean> => {
  if (await confirm.value.open(title, message, { color, hasCancel: true })) {
    return true
  }
  return false
}

const confirmDelete = async (title: string, message: string): Promise<boolean> => {
  if (await confirm.value.open(title, message, { color: 'red', hasCancel: true })) {
    return true
  }
  return false
}

const editConfigVariables = async (): Promise<void> => {
  const newConfigVariables = await dlgConfigVariables.value.open(configVariableValues.value.VarDefs)
  if (newConfigVariables !== null) {
    configVariableValues.value.VarDefs = newConfigVariables
    initVarValuesFromVarDefs()
  }
}

const initVarValuesFromVarDefs = (): void => {
  for (const varDef of configVariableValues.value.VarDefs) {
    configVariableValues.value.VarValues[varDef.ID] = varDef.DefaultValue
  }
}
</script>

<style>
.selectedpage {
  font-weight: bold;
  color: rgb(var(--v-theme-on-surface)) !important;
  margin: 12px !important;
  cursor: pointer;
}

.nonselectedpage {
  font-weight: normal;
  color: rgba(var(--v-theme-on-surface), 0.7) !important;
  margin: 12px !important;
  cursor: pointer;
}
</style>
