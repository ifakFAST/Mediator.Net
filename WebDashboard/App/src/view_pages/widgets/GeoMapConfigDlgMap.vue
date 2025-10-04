<template>
  <div>
    <v-dialog
      v-model="show"
      max-width="600px"
      persistent
      @keydown="
        (e: KeyboardEvent) => {
          if (e.keyCode === 27) {
            closeDialog()
          }
        }
      "
    >
      <v-card>
        <v-card-title>
          <span class="text-h5">Configure Map Settings</span>
        </v-card-title>

        <v-card-text>
          <!-- Map Configuration -->
          <v-list-subheader class="pl-0">Map Settings</v-list-subheader>
          <v-row>
            <v-col cols="4">
              <v-text-field
                v-model="theConfig.MapConfig.Center"
                label="Center"
              ></v-text-field>
            </v-col>
            <v-col cols="4">
              <v-text-field
                v-model="theConfig.MapConfig.ZoomDefault"
                label="Default Zoom Level"
                type="number"
              ></v-text-field>
            </v-col>
            <v-col cols="4">
              <v-text-field
                v-model="theConfig.MapConfig.MouseOverOpacityDelta"
                label="MouseOver Opacity Delta"
              ></v-text-field>
            </v-col>
          </v-row>
          <v-row>
            <v-col cols="4">
              <v-text-field
                v-model="theConfig.MapConfig.MainGroupLabel"
                label="Main Group Label"
              ></v-text-field>
            </v-col>
            <v-col cols="4">
              <v-text-field
                v-model="theConfig.MapConfig.OptionalGroupLabel"
                label="Optional Group Label"
              ></v-text-field>
            </v-col>
            <v-col cols="4">
              <v-text-field
                v-model="theConfig.MapConfig.GeoTiffResolution"
                label="GeoTiff Resolution"
                type="number"
              ></v-text-field>
            </v-col>
          </v-row>

          <v-row>
            <v-col cols="4">
              <v-text-field
                v-model="theConfig.MapConfig.FrameDelay"
                label="Frame Delay (ms)"
              ></v-text-field>
            </v-col>
            <v-col cols="4">
              <v-text-field
                v-model="theConfig.MapConfig.EndOfLoopPause"
                label="End of Loop Pause (ms)"
              ></v-text-field>
            </v-col>
            <v-col cols="4">
              <v-checkbox
                v-model="theConfig.MapConfig.AutoPlayLoop"
                label="Auto Play"
              ></v-checkbox>
            </v-col>
          </v-row>

          <v-list-subheader class="pl-0">Legend Settings</v-list-subheader>
          <v-row>
            <v-col cols="8">
              <v-text-field
                v-model="theConfig.LegendConfig.File"
                label="File in WebAssets folder"
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.LegendConfig.Width"
                label="Width"
                type="number"
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.LegendConfig.Height"
                label="Height"
                type="number"
              ></v-text-field>
            </v-col>
          </v-row>
        </v-card-text>

        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn
            color="grey-darken-1"
            variant="text"
            @click="closeDialog"
          >
            Cancel
          </v-btn>
          <v-btn
            color="primary-darken-1"
            variant="text"
            @click="SaveConfig"
          >
            Save
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import * as config from './GeoMapConfigTypes'

// Props
interface Props {
  configuration?: config.GeoMapConfig
  backendAsync?: (request: string, parameters: object) => Promise<any>
}

const props = withDefaults(defineProps<Props>(), {
  configuration: () => ({}) as config.GeoMapConfig,
})

// Reactive data
const show = ref(false)
const theConfig = ref<config.GeoMapConfig>(config.DefaultGeoMapConfig)
let resolveDialog: (v: boolean) => void = () => {}

// Methods
const showDialog = async (): Promise<boolean> => {
  const str = JSON.stringify(props.configuration)
  theConfig.value = JSON.parse(str)
  show.value = true

  return new Promise<boolean>((resolve) => {
    resolveDialog = resolve
  })
}

const SaveConfig = async (): Promise<void> => {
  const para = {
    config: theConfig.value,
  }
  try {
    await props.backendAsync!('SaveConfig', para)
  } catch (exp) {
    alert(exp)
    return
  }
  show.value = false
  resolveDialog(true)
}

const closeDialog = (): void => {
  show.value = false
  resolveDialog(false)
}

// Expose methods for parent component
defineExpose({
  showDialog,
})
</script>
