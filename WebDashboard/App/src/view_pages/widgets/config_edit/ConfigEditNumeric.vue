<template>
  <div>
    <div @contextmenu="onContextMenu">
      <v-table
        density="compact"
        :height="theHeight"
      >
        <thead v-if="config.ShowHeader">
          <tr>
            <th
              class="text-left"
              style="font-size: 14px"
            >
              Setting
            </th>
            <th
              class="text-right"
              style="font-size: 14px; height: 36px; padding-right: 0px; min-width: 55px"
            >
              Value
            </th>
            <th
              v-if="hasUnitColumn"
              class="text-left"
              style="font-size: 14px; height: 36px"
            ></th>
            <th>&nbsp;</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="it in config.Items"
            :key="it.Name"
          >
            <td
              class="text-left"
              style="font-size: 14px; height: 36px"
            >
              {{ it.Name }}
            </td>
            <td
              class="text-right"
              style="font-size: 14px; height: 36px; padding-right: 0px; min-width: 55px"
            >
              <span :style="{ color: colorForItem(it) }">{{ valueForItem(it, '') }}</span>
            </td>
            <td
              v-if="hasUnitColumn"
              class="text-left"
              style="font-size: 14px; height: 36px; padding-left: 8px; padding-right: 0px"
            >
              {{ it.Unit }}
            </td>
            <td
              class="pl-5 pr-4"
              style="font-size: 14px; height: 36px"
            >
              <v-icon
                :disabled="!writeEnabled(it)"
                icon="mdi-pencil"
                style="font-size: 21px"
                @click="onWriteItem(it)"
              ></v-icon>
            </td>
          </tr>
        </tbody>
      </v-table>
    </div>

    <v-menu
      v-model="contextMenu.show"
      :target="[contextMenu.clientX, contextMenu.clientY]"
    >
      <v-list>
        <v-list-item @click="onToggleShowHeader">
          <v-list-item-title>{{ config.ShowHeader ? 'Hide Header' : 'Show Header' }}</v-list-item-title>
        </v-list-item>
        <v-list-item @click="onConfigureItems">
          <v-list-item-title>Configure Items...</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <dlg-config-items
      ref="dlgConfigItems"
      :backend-async="backendAsync"
      :config-items="config.Items"
    ></dlg-config-items>
    <dlg-text-input ref="textInput"></dlg-text-input>
    <dlg-enum-input ref="enumInput"></dlg-enum-input>
  </div>
</template>

<script setup lang="ts">
// @ts-nocheck
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import DlgTextInput from '../../DlgTextInput.vue'
import DlgEnumInput from './DlgEnumInput.vue'
import DlgConfigItems from './DlgConfigItems.vue'
import type { Config, ConfigItem, ItemValue } from './types'
import type { EnumValEntry } from './util'
import { parseEnumValues, onWriteItemEnum, onWriteItemNumeric } from './util'

// Props
interface Props {
  id?: string
  width?: string
  height?: string
  config: Config
  backendAsync: (request: string, parameters: object) => Promise<any>
  eventName?: string
  eventPayload?: any
  timeRange?: object
  resize?: number
}

const props = withDefaults(defineProps<Props>(), {
  id: '',
  width: '',
  height: '',
  eventName: '',
  eventPayload: () => ({}),
  timeRange: () => ({}),
  resize: 0,
})

// Reactive data
const result = ref<ItemValue[]>([])
const contextMenu = ref({
  show: false,
  clientX: 0,
  clientY: 0,
})
const canUpdateConfig = ref(false)

// Template refs
const dlgConfigItems = ref<InstanceType<typeof DlgConfigItems> | null>(null)
const textInput = ref<InstanceType<typeof DlgTextInput> | null>(null)
const enumInput = ref<InstanceType<typeof DlgEnumInput> | null>(null)

// Computed properties
const theHeight = computed(() => {
  if (props.height.trim() === '') return 'auto'
  return props.height
})

const hasUnitColumn = computed(() => {
  return props.config.Items.some((it) => it.Unit.trim() !== '')
})

// Methods
const readValues = async (): Promise<void> => {
  try {
    const response = await props.backendAsync('ReadValues', {})
    result.value = response
  } catch (exp) {
    alert(exp)
  }
}

const writeEnabled = (it: ConfigItem): boolean => {
  const hasConfig =
    it.Object !== undefined && it.Object !== null && it.Object !== '' && it.Member !== undefined && it.Member !== null && it.Member !== ''
  if (!hasConfig) {
    return false
  }
  for (const entry of result.value) {
    if (entry.Object === it.Object && entry.Member === it.Member) {
      return entry.CanEdit
    }
  }
  return false
}

const onWriteItem = async (it: ConfigItem): Promise<void> => {
  const oldValue: string = valueForItem(it, '')
  if (it.Type === 'Range') {
    await onWriteItemNumeric(it, oldValue, textInputDlg, props.backendAsync)
  } else {
    await onWriteItemEnum(it, oldValue, enumInputDlg, props.backendAsync)
  }
}

const valueForItem = (it: ConfigItem, defaultValue: string): string => {
  if (it.Type === 'Enum') {
    const vals: EnumValEntry[] = parseEnumValues(it.EnumValues)
    for (const entry of result.value) {
      if (entry.Object === it.Object && entry.Member === it.Member) {
        const v = entry.Value
        const vnum: number = parseFloat(v)
        for (const item of vals) {
          if (item.num === vnum) {
            return item.label
          }
        }
        return v
      }
    }
  } else {
    for (const entry of result.value) {
      if (entry.Object === it.Object && entry.Member === it.Member) {
        return entry.Value
      }
    }
  }
  return defaultValue
}

const colorForItem = (it: ConfigItem): string => {
  if (it.Type === 'Enum') {
    const vals: EnumValEntry[] = parseEnumValues(it.EnumValues)
    for (const entry of result.value) {
      if (entry.Object === it.Object && entry.Member === it.Member) {
        const v = entry.Value
        const vnum: number = parseFloat(v)
        for (const item of vals) {
          if (item.num === vnum) {
            return item.color ?? ''
          }
        }
        return ''
      }
    }
  }
  return ''
}

const onToggleShowHeader = async (): Promise<void> => {
  try {
    await props.backendAsync('ToggleShowHeader', {})
  } catch (err: any) {
    alert(err.message)
  }
}

const textInputDlg = async (title: string, message: string, value: string, valid: (str: string) => string): Promise<string | null> => {
  if (!textInput.value) return null
  return textInput.value.openWithValidator(title, message, value, valid)
}

const enumInputDlg = async (title: string, message: string, value: string, values: string[]): Promise<string | null> => {
  if (!enumInput.value) return null
  return enumInput.value.open(title, message, value, values)
}

const onContextMenu = (e: MouseEvent): void => {
  if (canUpdateConfig.value) {
    e.preventDefault()
    e.stopPropagation()
    contextMenu.value.show = false
    contextMenu.value.clientX = e.clientX
    contextMenu.value.clientY = e.clientY
    nextTick(() => {
      contextMenu.value.show = true
    })
  }
}

const onConfigureItems = async (): Promise<void> => {
  if (!dlgConfigItems.value) return
  const ok = await dlgConfigItems.value.showDialog()
  if (ok) {
    await readValues()
  }
}

// Watchers
watch(
  () => props.eventPayload,
  (newVal: any) => {
    if (props.eventName === 'OnValuesChanged') {
      // console.info('OnValuesChanged event! ' + JSON.stringify(newVal))
      result.value = newVal
    }
  },
)

// Lifecycle
onMounted(async () => {
  await readValues()
  canUpdateConfig.value = (window.parent as any)['dashboardApp'].canUpdateViewConfig()
})
</script>
