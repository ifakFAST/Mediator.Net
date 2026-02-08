<template>
  <div
    class="ma-4"
    style="height: calc(100vh - 100px); overflow-y: auto"
  >
    <v-tabs
      v-model="activeProtocol"
      density="compact"
    >
      <v-tab value="MQTT">MQTT</v-tab>
      <v-tab value="SQL">SQL</v-tab>
      <v-tab value="OPC_UA">OPC UA</v-tab>
    </v-tabs>

    <Splitter
      :default-percent="20"
      style="height: calc(100vh - 160px); overflow-y: auto"
    >
      <template #left-pane>
        <div
          class="pr-4"
          style="min-width: 200px"
        >
          <v-list
            density="compact"
            nav
          >
            <v-list-item
              v-for="config in currentConfigs"
              :key="config.ID"
              :active="selectedConfigId === config.ID"
              :prepend-icon="protocolIcon"
              :title="config.Name || config.ID"
              @click="selectConfig(config.ID)"
            />
          </v-list>
          <v-btn
            block
            class="mt-2"
            size="small"
            variant="text"
            @click="prepareAddConfig"
          >
            <v-icon start>mdi-plus</v-icon>
            Add
          </v-btn>
        </div>
      </template>

      <template #right-pane>
        <div class="pl-4">
          <v-toolbar
            v-if="editConfig !== null"
            :elevation="4"
            density="compact"
          >
            <div class="my-toolbar-title">
              {{ configTitle }}
            </div>

            <v-spacer />
            <v-btn
              :disabled="!isObjectDirty"
              variant="text"
              @click="saveConfig"
            >
              Save
            </v-btn>
            <v-btn
              variant="text"
              @click="deleteConfig"
            >
              Delete
            </v-btn>

            <v-btn
              :disabled="isFirst"
              icon
              @click="moveConfig(true)"
            >
              <v-icon>mdi-arrow-up</v-icon>
            </v-btn>
            <v-btn
              :disabled="isLast"
              icon
              @click="moveConfig(false)"
            >
              <v-icon>mdi-arrow-down</v-icon>
            </v-btn>
          </v-toolbar>

          <v-toolbar
            v-if="editConfig === null"
            :elevation="4"
            density="compact"
          >
            <div class="my-toolbar-title">
              No configuration selected
            </div>
          </v-toolbar>

          <MqttConfigEditor
            v-if="activeProtocol === 'MQTT' && editConfig !== null"
            v-model="editConfig as MqttConfig"
            :modules="modules"
          />
          <SqlConfigEditor
            v-if="activeProtocol === 'SQL' && editConfig !== null"
            v-model="editConfig as SQLConfig"
            :modules="modules"
          />
          <OpcUaConfigEditor
            v-if="activeProtocol === 'OPC_UA' && editConfig !== null"
            v-model="editConfig as OpcUaConfig"
            :modules="modules"
          />
        </div>
      </template>
    </Splitter>
  </div>

  <v-dialog
    v-model="addDialog.show"
    max-width="350px"
    persistent
    @keydown="onAddDialogKeydown"
  >
    <v-card>
      <v-card-title>
        <span class="text-h5">Add new {{ addDialog.typeName }} config</span>
      </v-card-title>
      <v-card-text>
        <v-text-field
          v-model="addDialog.newID"
          hint="The unique and immutable identifier"
          label="ID"
        />
        <v-text-field
          v-model="addDialog.newName"
          autofocus
          label="Name"
        />
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn
          color="grey-darken-1"
          variant="text"
          @click="addDialog.show = false"
        >
          Cancel
        </v-btn>
        <v-btn
          color="primary-darken-1"
          variant="text"
          @click="onAddNewConfig"
        >
          Add
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <Confirm ref="dlgConfirm" />
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import type { PublishModel, MqttConfig, SQLConfig, OpcUaConfig, ModuleInfo } from './model'
import * as utils from '../utils'
import MqttConfigEditor from './MqttConfigEditor.vue'
import SqlConfigEditor from './SqlConfigEditor.vue'
import OpcUaConfigEditor from './OpcUaConfigEditor.vue'

type ConfigItem = MqttConfig | SQLConfig | OpcUaConfig

const activeProtocol = ref<'MQTT' | 'SQL' | 'OPC_UA'>('MQTT')
const model = ref<PublishModel | null>(null)
const modules = ref<ModuleInfo[]>([])
const selectedConfigId = ref<string | null>(null)
const editConfig = ref<ConfigItem | null>(null)
const editConfigOriginal = ref('')
const dlgConfirm = ref(null)

const addDialog = ref({
  show: false,
  typeName: '',
  parentMember: '',
  newID: '',
  newName: '',
})

const currentConfigs = computed((): ConfigItem[] => {
  if (!model.value) return []
  switch (activeProtocol.value) {
    case 'MQTT':
      return model.value.MQTT ?? []
    case 'SQL':
      return model.value.SQL ?? []
    case 'OPC_UA':
      return model.value.OPC_UA ?? []
  }
})

const protocolIcon = computed((): string => {
  switch (activeProtocol.value) {
    case 'MQTT':
      return 'mdi-access-point'
    case 'SQL':
      return 'mdi-database'
    case 'OPC_UA':
      return 'mdi-server'
  }
})

const configTitle = computed((): string => {
  if (!editConfig.value) return ''
  const name = editConfig.value.Name
  return name
})

const isObjectDirty = computed((): boolean => {
  if (!editConfig.value) return false
  return editConfigOriginal.value !== JSON.stringify(editConfig.value)
})

const currentIndex = computed((): number => {
  if (!selectedConfigId.value) return -1
  return currentConfigs.value.findIndex((c) => c.ID === selectedConfigId.value)
})

const isFirst = computed((): boolean => currentIndex.value <= 0)
const isLast = computed((): boolean => currentIndex.value < 0 || currentIndex.value >= currentConfigs.value.length - 1)

watch(activeProtocol, () => {
  selectedConfigId.value = null
  editConfig.value = null
  editConfigOriginal.value = ''
})

const selectConfig = (id: string): void => {
  selectedConfigId.value = id
  const config = currentConfigs.value.find((c) => c.ID === id)
  if (config) {
    const str = JSON.stringify(config)
    editConfigOriginal.value = str
    editConfig.value = JSON.parse(str)
  }
}

const saveConfig = (): void => {
  const id = editConfig.value?.ID
  if (!id) return
  const para = {
    ID: id,
    Obj: editConfig.value,
  }
  // @ts-ignore
  const dashboard = window.parent['dashboardApp']
  dashboard.sendViewRequest('Save', para, (strResponse: string) => {
    initModel(strResponse, id)
  })
}

const deleteConfig = async (): Promise<void> => {
  const confirm = dlgConfirm.value as any
  const name = configTitle.value
  if (await confirm.open('Confirm Delete', `Do you want to delete ${name}?`, { color: 'red' })) {
    const id = editConfig.value?.ID
    if (!id) return
    const configs = currentConfigs.value
    const idx = configs.findIndex((c) => c.ID === id)
    let nextSelectID = ''
    if (idx + 1 < configs.length) {
      nextSelectID = configs[idx + 1].ID
    } else if (idx - 1 >= 0) {
      nextSelectID = configs[idx - 1].ID
    }
    // @ts-ignore
    const dashboard = window.parent['dashboardApp']
    dashboard.sendViewRequest('Delete', JSON.stringify(id), (strResponse: string) => {
      initModel(strResponse, nextSelectID)
    })
  }
}

const moveConfig = (up: boolean): void => {
  const id = editConfig.value?.ID
  if (!id) return
  const info = {
    ObjID: id,
    Up: up,
  }
  // @ts-ignore
  const dashboard = window.parent['dashboardApp']
  dashboard.sendViewRequest('MoveConfig', info, (strResponse: string) => {
    initModel(strResponse, id)
  })
}

const prepareAddConfig = (): void => {
  let typeName: string
  let parentMember: string
  switch (activeProtocol.value) {
    case 'MQTT':
      typeName = 'MQTT'
      parentMember = 'MQTT'
      break
    case 'SQL':
      typeName = 'SQL'
      parentMember = 'SQL'
      break
    case 'OPC_UA':
      typeName = 'OPC UA'
      parentMember = 'OPC_UA'
      break
  }
  addDialog.value.typeName = typeName
  addDialog.value.parentMember = parentMember
  addDialog.value.newID = utils.findUniqueID(parentMember, 6, getAllIDs())
  addDialog.value.newName = ''
  addDialog.value.show = true
}

const onAddNewConfig = (): void => {
  if (!utils.isValidObjectNameOrID(addDialog.value.newID)) {
    alert('ID must not be empty and must not start or end with whitespace.')
    return
  }
  if (!utils.isValidObjectNameOrID(addDialog.value.newName)) {
    alert('Name must not be empty and must not start or end with whitespace.')
    return
  }
  addDialog.value.show = false
  const info = {
    ParentMember: addDialog.value.parentMember,
    NewID: addDialog.value.newID,
    NewName: addDialog.value.newName,
  }
  const newID = addDialog.value.newID
  // @ts-ignore
  const dashboard = window.parent['dashboardApp']
  dashboard.sendViewRequest('AddConfig', info, (strResponse: string) => {
    initModel(strResponse, newID)
  })
}

const onAddDialogKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    addDialog.value.show = false
  } else if (e.key === 'Enter') {
    onAddNewConfig()
  }
}

const initModel = (strResponse: string, activeItemID?: string): void => {
  const res = JSON.parse(strResponse)
  model.value = res.model
  modules.value = res.moduleInfos

  if (activeItemID) {
    const config = currentConfigs.value.find((c) => c.ID === activeItemID)
    if (config) {
      selectConfig(activeItemID)
    } else {
      selectedConfigId.value = null
      editConfig.value = null
      editConfigOriginal.value = ''
    }
  } else {
    selectedConfigId.value = null
    editConfig.value = null
    editConfigOriginal.value = ''
  }
}

const getAllIDs = (): Set<string> => {
  const set = new Set<string>()
  if (model.value) {
    for (const c of model.value.MQTT ?? []) set.add(c.ID)
    for (const c of model.value.SQL ?? []) set.add(c.ID)
    for (const c of model.value.OPC_UA ?? []) set.add(c.ID)
  }
  return set
}

onMounted(() => {
  // @ts-ignore
  const dashboard = window.parent['dashboardApp']
  dashboard.sendViewRequest('GetModel', {}, (strResponse: string) => {
    initModel(strResponse)
  })
})
</script>

<style scoped>
.my-toolbar-title {
  font-size: 1.25rem;
  font-weight: 400;
  letter-spacing: 0;
  line-height: 1.75rem;
  text-transform: none;
  margin-left: 15px;
  margin-right: 15px;
}
</style>
