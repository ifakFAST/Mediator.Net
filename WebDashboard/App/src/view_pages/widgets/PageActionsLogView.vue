<template>
  <div @contextmenu="onContextMenu">
    <v-table
      density="compact"
      :height="theHeight"
    >
      <thead v-if="config.ShowHeader">
        <tr>
          <th
            class="text-left"
            style="font-size: 14px; height: 36px; min-width: 80px"
          >
            Who
          </th>
          <th
            class="text-left"
            style="font-size: 14px; height: 36px; min-width: 142px"
          >
            Time
          </th>
          <th
            class="text-left"
            style="font-size: 14px; height: 36px; min-width: 150px"
          >
            Change
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="item in items"
          :key="item.Timestamp"
        >
          <td
            class="text-left"
            style="font-size: 14px; height: 36px; min-width: 80px"
          >
            {{ item.User }}
          </td>
          <td
            class="text-left"
            style="font-size: 14px; height: 36px; min-width: 142px"
          >
            {{ item.Time }}
          </td>
          <td
            class="text-left"
            style="font-size: 14px; height: 36px; min-width: 150px"
          >
            {{ item.Action }}
          </td>
        </tr>
      </tbody>
    </v-table>

    <v-menu
      v-model="contextMenu.show"
      :target="[contextMenu.clientX, contextMenu.clientY]"
    >
      <v-list>
        <v-list-item @click="onToggleShowHeader">
          <v-list-item-title> {{ config.ShowHeader ? 'Hide Header' : 'Show Header' }}</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import type { TimeRange } from '../../utils'

interface Config {
  ShowHeader: boolean
}

interface LogEntry {
  Timestamp: number
  Time: string
  User: string
  Action: string
}

const props = defineProps<{
  id: string
  width: string
  height: string
  config: Config
  backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  eventName: string
  eventPayload: object
  timeRange: TimeRange
  resize: number
  dateWindow: number[] | null
}>()

const items = ref<LogEntry[]>([
  {
    Timestamp: 0,
    Time: '',
    User: '',
    Action: '',
  },
])

const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})

const canUpdateConfig = ref(false)

onMounted(() => {
  onLoadData()
  canUpdateConfig.value = (window.parent as any).dashboardApp.canUpdateViewConfig()
})

const onContextMenu = async (e: any): Promise<void> => {
  if (canUpdateConfig.value) {
    e.preventDefault()
    e.stopPropagation()
    contextMenu.value.show = false
    contextMenu.value.clientX = e.clientX
    contextMenu.value.clientY = e.clientY

    await nextTick()
    contextMenu.value.show = true
  }
}

const theHeight = computed((): string => {
  if (props.height.trim() === '') {
    return 'auto'
  }
  return props.height
})

const onLoadData = async (): Promise<void> => {
  const response: LogEntry[] = await props.backendAsync('ReadValues', {})
  items.value = response
}

const onToggleShowHeader = async (): Promise<void> => {
  try {
    await props.backendAsync('ToggleShowHeader', {})
  } catch (err: any) {
    alert(err.message)
  }
}

watch(
  () => props.eventPayload,
  (newVal: object, old: object) => {
    if (props.eventName === 'OnValuesChanged') {
      items.value = props.eventPayload as LogEntry[]
    }
  },
)
</script>
