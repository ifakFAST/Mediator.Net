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
          v-model="model.Host"
          name="Host"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.Port"
          name="Port"
          :optional="false"
          type="Number"
        />
        <member-row
          v-model="model.LogLevel"
          :enum-values="logLevels"
          name="Log Level"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-model="model.AllowAnonym"
          name="Allow Anonymous"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.LoginUser"
          name="Login User"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.LoginPass"
          name="Login Pass"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.ServerCertificateFile"
          name="Server Certificate File"
          :optional="false"
          type="String"
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
        <root-objects-editor
          v-model="model.VarPublish.RootObjects"
          :modules="modules"
        />
        <member-row
          v-model="model.VarPublish.LocalObjectIDsForVariables"
          name="Local Object IDs For Variables"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.VarPublish.BufferIfOffline"
          name="Buffer If Offline"
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
          v-model="model.VarPublish.PublishMode"
          :enum-values="publishModes"
          name="Publish Mode"
          :optional="false"
          type="Enum"
        />
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { OpcUaConfig, ModuleInfo } from './model'
import MemberRow from '../view_calc/util/MemberRow.vue'
import RootObjectsEditor from './RootObjectsEditor.vue'

defineProps<{
  modules: ModuleInfo[]
}>()

const model = defineModel<OpcUaConfig>({ required: true })

const selectedTab = ref('General')

const logLevels = ['Trace', 'Debug', 'Info', 'Warn', 'Error']
const nanHandlings = ['Keep', 'ConvertToNull', 'ConvertToString', 'Remove']
const publishModes = ['Cyclic', 'OnVarValueUpdate', 'OnVarHistoryUpdate']
</script>
