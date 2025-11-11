<template>
  <v-dialog
    v-model="show"
    max-width="800px"
    persistent
    @keydown.esc="cancel"
  >
    <v-card>
      <v-card-title>Extended Item Configuration</v-card-title>
      <v-card-text class="pa-0">
        <v-container fluid>
          <v-row style="height: 500px">
            <!-- Master: Items List -->
            <v-col
              cols="5"
              class="pr-4"
              style="border-right: 1px solid #e0e0e0; height: 100%; overflow-y: auto"
            >
              <v-list
                density="compact"
                class="pa-0"
              >
                <v-list-item
                  v-for="(item, idx) in items"
                  :key="idx"
                  :active="selectedItemIndex === idx"
                  @click="selectedItemIndex = idx"
                  class="mb-1"
                  style="border: 1px solid #e0e0e0; border-radius: 4px"
                >
                  <v-list-item-title>
                    <div class="d-flex align-center">
                      <div
                        style="width: 24px; height: 24px; border-radius: 4px; margin-right: 8px"
                        :style="{ backgroundColor: item.Color }"
                      ></div>
                      <span>{{ item.Name || '(unnamed)' }}</span>
                    </div>
                  </v-list-item-title>
                </v-list-item>
              </v-list>

              <v-alert
                v-if="items.length === 0"
                type="info"
                variant="tonal"
                class="mt-4"
              >
                No items available
              </v-alert>
            </v-col>

            <!-- Detail: Selected Item ObjectConfig Properties -->
            <v-col
              cols="7"
              style="height: 100%; overflow-y: auto"
            >
              <div v-if="selectedItem && selectedItem.ObjectConfig">
                <!-- Struct Value Configuration -->
                <v-card
                  variant="outlined"
                  class="mb-4"
                >
                  <v-card-title class="text-subtitle-1 bg-grey-lighten-4">Struct Value Settings</v-card-title>
                  <v-card-text>
                    <v-text-field
                      v-model="selectedItem.ObjectConfig.KeyValue"
                      label="Value Key"
                      class="mb-3"
                    ></v-text-field>
                    <v-checkbox
                      v-model="selectedItem.ObjectConfig.ShowLabel"
                      label="Show Annotation Label"
                      class="mt-2 mb-2"
                      :disabled="!selectedItem.ObjectConfig.KeyValue"
                    ></v-checkbox>
                    <v-text-field
                      v-model="selectedItem.ObjectConfig.KeyLabel"
                      label="Label Key"
                      class="ml-8 mb-3"
                      :disabled="!selectedItem.ObjectConfig.ShowLabel || !selectedItem.ObjectConfig.KeyValue"
                    ></v-text-field>
                    <v-text-field
                      v-model="selectedItem.ObjectConfig.KeyTooltip"
                      label="Tooltip Key"
                      class="ml-8"
                      :disabled="!selectedItem.ObjectConfig.ShowLabel || !selectedItem.ObjectConfig.KeyValue"
                    ></v-text-field>
                  </v-card-text>
                </v-card>
              </div>

              <v-alert
                v-else
                type="info"
                variant="tonal"
              >
                Select an item from the list to edit its annotation configuration
              </v-alert>
            </v-col>
          </v-row>
        </v-container>
      </v-card-text>

      <v-card-actions>
        <v-spacer></v-spacer>
        <v-btn
          @click="cancel"
          variant="text"
        >
          Cancel
        </v-btn>
        <v-btn
          color="primary-darken-1"
          variant="text"
          @click="save"
        >
          OK
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'

interface Variable {
  Object: string
  Name: string
}

type SeriesType = 'Line' | 'Scatter'
type Axis = 'Left' | 'Right'

interface ObjectConfig {
  KeyValue: string
  ShowLabel: boolean
  KeyLabel: string
  KeyTooltip: string
}

interface ItemConfig {
  Name: string
  Color: string
  Size: number
  SeriesType: SeriesType
  Axis: Axis
  Checked: boolean
  Variable: Variable
  ObjectConfig: ObjectConfig
}

interface Props {
  modelValue: boolean
  items: ItemConfig[]
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'update:items', value: ItemConfig[]): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const show = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value),
})

const selectedItemIndex = ref<number>(0)

const selectedItem = computed(() => {
  if (props.items && props.items.length > 0 && selectedItemIndex.value >= 0 && selectedItemIndex.value < props.items.length) {
    const item = props.items[selectedItemIndex.value]
    // Ensure ObjectConfig exists
    if (!item.ObjectConfig) {
      item.ObjectConfig = {
        KeyValue: '',
        ShowLabel: false,
        KeyLabel: '',
        KeyTooltip: '',
      }
    }
    return item
  }
  return null
})

// Reset selection when dialog opens
watch(show, (newValue) => {
  if (newValue && props.items && props.items.length > 0) {
    selectedItemIndex.value = 0
  }
})

const save = (): void => {
  emit('update:items', props.items)
  show.value = false
}

const cancel = (): void => {
  show.value = false
}
</script>
