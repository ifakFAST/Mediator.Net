<template>
  <div>
    <v-tabs
      v-model="selectedTab"
      style="margin-top: 8px"
      density="compact"
    >
      <v-tab value="General">General</v-tab>
      <v-tab value="VarPublish">Variable Publishing</v-tab>
    </v-tabs>

    <div v-if="selectedTab === 'General'">
      <table cellspacing="10">
        <member-row
          v-model="model.Name"
          name="Name"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.DatabaseType"
          :enum-values="databaseTypes"
          name="Database Type"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-model="model.ConnectionString"
          name="Connection String"
          :tooltip="sqlConnectionStringTooltip"
          :optional="false"
          type="Text"
        />
      </table>
    </div>

    <div v-if="selectedTab === 'VarPublish'">
      <table cellspacing="10">
        <member-row
          v-model="model.VarPublish.Enabled"
          name="Enabled"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.VarPublish.QueryTagID2Identifier"
          name="Query TagID to Identifier"
          :tooltip="sqlQueryTagID2IdentifierTooltip"
          :optional="false"
          type="Text"
        />
        <member-row
          v-model="model.VarPublish.QueryRegisterTag"
          name="Query Register Tag"
          :tooltip="sqlQueryRegisterTagTooltip"
          :optional="false"
          type="Text"
        />
        <member-row
          v-model="model.VarPublish.QueryPublish"
          name="Query Publish"
          :tooltip="sqlQueryPublishTooltip"
          :optional="false"
          type="Text"
        />
        <root-objects-editor
          v-model="model.VarPublish.RootObjects"
          :modules="modules"
        />
        <member-row
          v-model="model.VarPublish.PublishMode"
          :enum-values="publishModes"
          name="Publish Mode"
          :tooltip="publishModeTooltip"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-model="model.VarPublish.PublishInterval"
          name="Publish Interval"
          :optional="false"
          type="Duration"
        />
        <member-row
          v-model="model.VarPublish.PublishOffset"
          name="Publish Offset"
          :optional="false"
          type="Duration"
        />
        <member-row
          v-model="model.VarPublish.BufferIfOffline"
          name="Buffer If Offline"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.VarPublish.LogWrites"
          name="Log Writes"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.VarPublish.SimpleTagsOnly"
          name="Simple Tags Only"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.VarPublish.NumericTagsOnly"
          name="Numeric Tags Only"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.VarPublish.SendTagsWithNull"
          name="Send Tags With Null"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.VarPublish.NaN_Handling"
          :enum-values="nanHandlings"
          name="NaN Handling"
          :optional="false"
          type="Enum"
        />
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { SQLConfig, ModuleInfo } from './model'
import MemberRow from '../view_calc/util/MemberRow.vue'
import RootObjectsEditor from './RootObjectsEditor.vue'
import {
  publishModeTooltip,
  sqlConnectionStringTooltip,
  sqlQueryPublishTooltip,
  sqlQueryRegisterTagTooltip,
  sqlQueryTagID2IdentifierTooltip
} from './tooltips'

defineProps<{
  modules: ModuleInfo[]
}>()

const model = defineModel<SQLConfig>({ required: true })

const selectedTab = ref('General')

const databaseTypes = ['PostgreSQL']
const nanHandlings = ['Keep', 'ConvertToNull', 'ConvertToString', 'Remove']
const publishModes = ['Cyclic', 'OnVarValueUpdate', 'OnVarHistoryUpdate']
</script>
