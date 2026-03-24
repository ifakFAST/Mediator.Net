<template>
  <div class="pa-2">
    <div class="d-flex align-center ga-2 mb-2 flex-wrap">
      <v-btn-group
        density="compact"
        variant="outlined"
      >
        <v-btn
          :disabled="historyLoading"
          title="Latest values (descending)"
          @click="latestDesc"
        >
          <v-icon :style="viewMode === 'plot' ? 'transform: rotate(-90deg)' : ''">mdi-arrow-collapse-down</v-icon>
        </v-btn>
        <v-btn
          :disabled="historyLoading"
          title="Oldest values (ascending)"
          @click="oldestAsc"
        >
          <v-icon :style="viewMode === 'plot' ? 'transform: rotate(-90deg)' : ''">mdi-arrow-collapse-up</v-icon>
        </v-btn>
        <v-btn
          :disabled="historyLoading || historyRows.length === 0"
          title="Move down"
          @click="moveDown"
        >
          <v-icon :style="viewMode === 'plot' ? 'transform: rotate(-90deg)' : ''">mdi-chevron-down</v-icon>
        </v-btn>
        <v-btn
          :disabled="historyLoading || historyRows.length === 0"
          title="Move up"
          @click="moveUp"
        >
          <v-icon :style="viewMode === 'plot' ? 'transform: rotate(-90deg)' : ''">mdi-chevron-up</v-icon>
        </v-btn>
      </v-btn-group>

      <v-text-field
        v-model.number="pageSize"
        density="compact"
        hide-details
        max="10000"
        min="1"
        style="max-width: 110px"
        type="number"
      />

      <v-btn
        v-if="isStruct"
        :title="showRawJson ? 'Show as table' : 'Show as JSON'"
        density="compact"
        variant="outlined"
        @click="showRawJson = !showRawJson"
      >
        <v-icon>{{ showRawJson ? 'mdi-table' : 'mdi-code-json' }}</v-icon>
      </v-btn>

      <v-btn
        v-if="!isStruct"
        :title="viewMode === 'plot' ? 'Show as table' : 'Show as plot'"
        density="compact"
        variant="outlined"
        @click="viewMode = viewMode === 'plot' ? 'table' : 'plot'"
      >
        <v-icon>{{ viewMode === 'plot' ? 'mdi-table' : 'mdi-chart-line' }}</v-icon>
      </v-btn>

      <v-spacer />

      <span
        v-if="totalCount !== null"
        class="text-body-2 text-grey-darken-1"
      >Total: {{ totalCount.toLocaleString() }}</span>
      <v-btn
        v-else
        :loading="countLoading"
        density="compact"
        variant="text"
        @click="requestCount"
      >
        Count
      </v-btn>
    </div>

    <template v-if="historyRows.length > 0">
      <div
        v-if="viewMode === 'table'"
        style="overflow-x: auto"
      >
        <v-table density="compact">
          <thead>
            <tr>
              <th>Timestamp {{ sortDesc ? '▼' : '▲' }}</th>
              <th>Quality</th>
              <template v-if="showStructColumns">
                <th
                  v-for="member in structMembers"
                  :key="member"
                >
                  {{ member }}
                </th>
              </template>
              <th v-else>
                Value
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="(row, idx) in displayedRows"
              :key="idx"
            >
              <td class="text-no-wrap">
                {{ row.T }}
              </td>
              <td>{{ row.Q }}</td>
              <template v-if="showStructColumns">
                <td
                  v-for="member in structMembers"
                  :key="member"
                >
                  {{ getStructMemberValue(row.V, member) }}
                </td>
              </template>
              <td v-else-if="showStructPerRow">
                <StructView
                  :value="row.V"
                  :vertical="true"
                />
              </td>
              <td v-else>
                {{ row.V }}
              </td>
            </tr>
          </tbody>
        </v-table>
      </div>
      <DyGraph
        v-else
        :graph-data="graphData"
        :graph-options="graphOptions"
        :graph-style="{ width: '100%', height: '500px' }"
      />
    </template>

    <div
      v-else-if="!historyLoading"
      class="text-grey text-left mt-4 ml-2"
    >
      No history data.
    </div>

    <div
      v-else-if="historyLoading"
      class="text-left mt-4 ml-2"
    >
      <v-progress-circular
        indeterminate
        size="30"
        width="4"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import StructView from '../components/StructView.vue'

interface HistoryRow {
  T: string
  TJ: number
  Q: string
  V: string
}

const props = withDefaults(defineProps<{
  objectId: string | number
  variableName?: string
  dataType?: string
  dimension?: number
}>(), {
  variableName: 'Value',
  dataType: '',
  dimension: 1,
})

const pageSize = ref(12)
const historyRows = ref<HistoryRow[]>([])
const historyLoading = ref(false)
const countLoading = ref(false)
const totalCount = ref<number | null>(null)
const showRawJson = ref(false)
const sortDesc = ref(true)
const viewMode = ref<'table' | 'plot'>('table')

const displayedRows = computed(() => {
  if (sortDesc.value) {
    return [...historyRows.value].reverse()
  }
  return historyRows.value
})

const isStruct = computed(() => props.dataType === 'Struct')

const showStructColumns = computed(
  () => isStruct.value && props.dimension === 1 && !showRawJson.value,
)

const showStructPerRow = computed(
  () => isStruct.value && props.dimension !== 1 && !showRawJson.value,
)

const graphData = computed(() => {
  if (viewMode.value !== 'plot') return []
  return historyRows.value.map((row) => {
    const val = parseFloat(row.V)
    return [new Date(row.TJ), isNaN(val) ? null : val]
  })
})

const graphOptions = computed(() => ({
  labels: ['Date', props.variableName],
  legend: 'always',
  drawPoints: true,
  strokeWidth: 0,
  pointSize: 3,
}))

const structMembers = computed(() => {
  if (!showStructColumns.value) return []
  const set = new Set<string>()
  for (const row of historyRows.value) {
    try {
      const obj = JSON.parse(row.V)
      if (obj && typeof obj === 'object' && !Array.isArray(obj)) {
        for (const key of Object.keys(obj)) {
          set.add(key)
        }
      }
    } catch {
      // skip unparseable values
    }
  }
  return Array.from(set)
})

function getDashboard(): any {
  // @ts-ignore
  return window.parent['dashboardApp']
}

function latestDesc(): void {
  sortDesc.value = true
  loadHistory('Last', 0, 0)
}

function oldestAsc(): void {
  sortDesc.value = false
  loadHistory('First', 0, 0)
}

function moveDown(): void {
  if (historyRows.value.length === 0) return
  if (sortDesc.value) {
    loadHistory('Last', 0, historyRows.value[0].TJ - 1, true)
  } else {
    loadHistory('First', historyRows.value[historyRows.value.length - 1].TJ + 1, 0, true)
  }
}

function moveUp(): void {
  if (historyRows.value.length === 0) return
  if (sortDesc.value) {
    loadHistory('First', historyRows.value[historyRows.value.length - 1].TJ + 1, 0, true)
  } else {
    loadHistory('Last', 0, historyRows.value[0].TJ - 1, true)
  }
}

function loadHistory(mode: string, startJavaTicks: number, endJavaTicks: number, keepIfEmpty = false): void {
  historyLoading.value = true
  const params = {
    ObjectID: props.objectId,
    VariableName: props.variableName,
    Count: pageSize.value,
    Mode: mode,
    StartJavaTicks: startJavaTicks,
    EndJavaTicks: endJavaTicks,
  }

  getDashboard().sendViewRequest(
    'ReadVariableHistory',
    params,
    (strResponse: string) => {
      const rows: HistoryRow[] = JSON.parse(strResponse)
      if (rows.length > 0 || !keepIfEmpty) {
        historyRows.value = rows
      }
      historyLoading.value = false
    },
  )
}

function requestCount(): void {
  countLoading.value = true
  getDashboard().sendViewRequest(
    'CountVariableHistory',
    { ObjectID: props.objectId, VariableName: props.variableName },
    (strResponse: string) => {
      const result = JSON.parse(strResponse)
      totalCount.value = result.Count
      countLoading.value = false
    },
  )
}

function getStructMemberValue(v: string, member: string): string {
  try {
    const obj = JSON.parse(v)
    if (!obj || typeof obj !== 'object') return ''
    const val = obj[member]
    if (val === undefined) return ''
    if (typeof val === 'string') return val
    return JSON.stringify(val)
  } catch {
    return ''
  }
}

function reset(): void {
  historyRows.value = []
  totalCount.value = null
  viewMode.value = 'table'
}

function refresh(): void {
  if (sortDesc.value) {
    latestDesc()
  } else {
    oldestAsc()
  }
}

onMounted(() => {
  latestDesc()
})

watch(
  () => props.objectId,
  () => {
    reset()
    refresh()
  },
)

defineExpose({ reset, refresh, latestDesc, oldestAsc })
</script>
