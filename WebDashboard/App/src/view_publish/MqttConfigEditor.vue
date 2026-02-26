<template>
  <div class="d-flex flex-column overflow-hidden">
    <v-tabs
      v-model="selectedTab"
      class="flex-shrink-0"
      style="margin-top: 8px"
      density="compact"
    >
      <v-tab value="General">General</v-tab>
      <v-tab value="VarPublish">Variable Publishing</v-tab>
    </v-tabs>

    <div
      v-if="selectedTab === 'General'"
      class="flex-grow-1 overflow-y-auto"
    >
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
          :tooltip="mqttEndpointTooltip"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.UseTLS"
          :enum-values="tlsUsageModes"
          name="Use TLS"
          :tooltip="mqttUseTLSTooltip"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-model="model.ClientIDPrefix"
          name="Client ID Prefix"
          :optional="false"
          type="String"
        />
        <member-row
          v-if="model.UseTLS !== 'Never'"
          v-model="model.CertFileCA"
          name="Cert File CA"
          :optional="false"
          type="String"
        />
        <member-row
          v-if="model.UseTLS !== 'Never'"
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
          v-if="model.UseTLS !== 'Never'"
          v-model="model.NoCertificateValidation"
          name="No Certificate Validation"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-if="model.UseTLS !== 'Never'"
          v-model="model.IgnoreCertificateRevocationErrors"
          name="Ignore Cert Revocation Errors"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-if="model.UseTLS !== 'Never'"
          v-model="model.IgnoreCertificateChainErrors"
          name="Ignore Cert Chain Errors"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-if="model.UseTLS !== 'Never'"
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
          :tooltip="mqttTopicRootTooltip"
          :optional="false"
          type="String"
        />
      </table>
    </div>

    <div
      v-if="selectedTab === 'VarPublish'"
      class="flex-grow-1 overflow-y-auto"
    >
      <table cellspacing="10">
        <member-row
          v-model="model.VarPublish.Enabled"
          name="Enabled"
          :optional="false"
          type="Boolean"
        />
        <member-row
          v-model="model.VarPublish.Mode"
          :enum-values="topicModes"
          name="Topic Mode"
          :optional="false"
          type="Enum"
        />
        <member-row
          v-if="model.VarPublish.Mode === 'Bulk'"
          v-model="model.VarPublish.Topic"
          name="Topic"
          :tooltip="mqttVarTopicTooltip"
          :optional="false"
          type="String"
        />
        <member-row
          v-if="model.VarPublish.Mode === 'TopicPerVariable'"
          v-model="model.VarPublish.TopicTemplate"
          name="Topic Template"
          :tooltip="mqttVarTopicTemplateTooltip"
          :optional="false"
          type="String"
        />
        <member-row
          v-model="model.VarPublish.TopicRegistration"
          name="Topic Registration"
          :tooltip="mqttVarTopicRegistrationTooltip"
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
          v-if="model.VarPublish.Mode === 'Bulk'"
          v-model="model.VarPublish.PubFormat"
          :enum-values="pubVarFormats"
          name="Bulk Pub Format"
          :tooltip="mqttBulkPubFormatTooltip"
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
import {
  mqttBulkPubFormatTooltip,
  mqttEndpointTooltip,
  mqttTopicRootTooltip,
  mqttUseTLSTooltip,
  mqttVarTopicRegistrationTooltip,
  mqttVarTopicTemplateTooltip,
  mqttVarTopicTooltip,
  publishModeTooltip
} from './tooltips'

defineProps<{
  modules: ModuleInfo[]
}>()

const model = defineModel<MqttConfig>({ required: true })

const selectedTab = ref('General')

const tlsUsageModes = ['Auto', 'Always', 'Never']
const pubVarFormats = ['Array', 'Object']
const topicModes = ['Bulk', 'TopicPerVariable']
const nanHandlings = ['Keep', 'ConvertToNull', 'ConvertToString', 'Remove']
const publishModes = ['Cyclic', 'OnVarValueUpdate', 'OnVarHistoryUpdate']
</script>
