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
          v-model="model.Endpoint"
          name="Endpoint"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.ClientIDPrefix"
          name="Client ID Prefix"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.CertFileCA"
          name="Cert File CA"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.CertFileClient"
          name="Cert File Client"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.User"
          name="User"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.Pass"
          name="Pass"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.NoCertificateValidation"
          name="No Certificate Validation"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.IgnoreCertificateRevocationErrors"
          name="Ignore Cert Revocation Errors"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.IgnoreCertificateChainErrors"
          name="Ignore Cert Chain Errors"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.AllowUntrustedCertificates"
          name="Allow Untrusted Certificates"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.MaxPayloadSize"
          name="Max Payload Size"
          :optional="false"
          type="Number"
        />
        <member-row
          v-model="model.TopicRoot"
          name="Topic Root"
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
        <member-row
          v-model="model.VarPublish.Topic"
          name="Topic"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.VarPublish.TopicRegistration"
          name="Topic Registration"
          :optional="false"
          type="String"
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
          v-model="model.VarPublish.PubFormat"
          :enum-values="pubVarFormats"
          name="Pub Format"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-model="model.VarPublish.PubFormatReg"
          :enum-values="pubVarFormats"
          name="Pub Format Reg"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-model="model.VarPublish.Mode"
          :enum-values="topicModes"
          name="Topic Mode"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-if="model.VarPublish.Mode === 'TopicPerVariable'"
          v-model="model.VarPublish.TopicTemplate"
          name="Topic Template"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.VarPublish.PrintPayload"
          name="Print Payload"
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
          v-model="model.VarPublish.TimeAsUnixMilliseconds"
          name="Time As Unix Milliseconds"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.VarPublish.QualityNumeric"
          name="Quality Numeric"
          :optional="false"
          type="Boolean"
        />
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { MqttConfig, ModuleInfo } from './model'
import MemberRow from '../view_calc/util/MemberRow.vue'
import RootObjectsEditor from './RootObjectsEditor.vue'

defineProps<{
  modules: ModuleInfo[]
}>()

const model = defineModel<MqttConfig>({ required: true })

const selectedTab = ref('General')

const pubVarFormats = ['Array', 'Object']
const topicModes = ['Bulk', 'TopicPerVariable']
const nanHandlings = ['Keep', 'ConvertToNull', 'ConvertToString', 'Remove']
const publishModes = ['Cyclic', 'OnVarValueUpdate', 'OnVarHistoryUpdate']
const publishModeTooltip = `Cyclic: If PublishInterval = 0, publish on variable value update with an additional 1 min cycle. ELSE: Cyclic publish based on PublishInterval & PublishOffset

OnVarValueUpdate: Publish on variable value update (+ additional cycle publish if PublishInterval > 0).

OnVarHistoryUpdate: Publish on variable history update (+ additional cycle publish if PublishInterval > 0).`
</script>
