<template>
  <v-card-text>
    {{ id }} {{ eventName }} {{ result }}
    <v-btn @click="onButton">Read</v-btn>
  </v-card-text>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import * as fast from '../../fast_types'

interface Config {
  Variable?: fast.VariableRef
}

const props = defineProps<{
  id: string
  width: string
  height: string
  config: Config
  backendAsync: (request: string, parameters: object) => Promise<any>
  eventName: string
  eventPayload: any
  timeRange: object
  resize: number
}>()

const result = ref('')

onMounted(async () => {
  const response = await props.backendAsync('ReadVar', {})
  result.value = response
  console.info('mounted: ' + response)
})

const onButton = async (): Promise<void> => {
  const para = {
    param1: 'Hallo!',
  }
  const response = await props.backendAsync('Button', para)
  result.value = response

  console.info('Config: ' + JSON.stringify(props.config))
}

watch(
  () => props.eventPayload,
  (newVal: any, old: any) => {
    if (props.eventName === 'OnVarChanged') {
      console.info('VarChanged event! ' + JSON.stringify(newVal))
      result.value = newVal.NewVal
    }
  },
)
</script>
