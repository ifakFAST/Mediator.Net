<template>
  <div>
    <v-dialog v-model="show" persistent max-width="700px" @keydown="(e) => { if (e.keyCode === 27) { closeDialog(); }}">
      <v-card>
        <v-card-title>
          <span class="headline">Configure Map Settings</span>
        </v-card-title>

        <v-card-text>
          <!-- Map Configuration -->
          <v-subheader>Map Settings</v-subheader>
          <v-row>
            <v-col cols="4">
              <v-text-field
                v-model="theConfig.MapConfig.Center"
                label="Center"
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.MapConfig.ZoomDefault"
                label="Default Zoom Level"
                type="number"
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.MapConfig.MainGroupLabel"
                label="Main Group Label"
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.MapConfig.OptionalGroupLabel"
                label="Optional Group Label"
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.MapConfig.MouseOverOpacityDelta"
                label="MouseOver Opacity Delta"
              ></v-text-field>
            </v-col>
          </v-row>

          <v-subheader>Legend Settings</v-subheader>
          <v-row>
            <v-col cols="4">
              <v-text-field
                v-model="theConfig.LegendConfig.File"
                label="File in WebAssets folder"
                hide-details
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.LegendConfig.Width"
                label="Width"
                type="number"
                hide-details
              ></v-text-field>
            </v-col>
            <v-col cols="2">
              <v-text-field
                v-model="theConfig.LegendConfig.Height"
                label="Height"
                type="number"
                hide-details
              ></v-text-field>
            </v-col>
          </v-row>
        </v-card-text>

        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn color="grey darken-1" text @click.native="closeDialog">Cancel</v-btn>
          <v-btn color="primary darken-1" text @click.native="SaveConfig">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue } from 'vue-property-decorator'
import * as config from './GeoMapConfigTypes'

@Component({
  components: {},
})
export default class GeoMapConfigDlgMap extends Vue {

  @Prop({ default() { return {} } }) configuration: config.GeoMapConfig
  @Prop() backendAsync: (request: string, parameters: object) => Promise<any>

  show: boolean = false
  theConfig: config.GeoMapConfig = config.DefaultGeoMapConfig

  resolveDialog: ((v: boolean) => void) = (v) => {}

  async showDialog(): Promise<boolean> {
    const str = JSON.stringify(this.configuration)
    this.theConfig = JSON.parse(str)
    this.show = true

    return new Promise<boolean>((resolve) => {
      this.resolveDialog = resolve
    })
  }

  async SaveConfig(): Promise<void> {
    const para = {
      config: this.theConfig,
    }
    try {
      await this.backendAsync('SaveConfig', para)
    }
    catch (exp) {
      alert(exp)
      return
    }
    this.show = false
    this.resolveDialog(true)
  }

  closeDialog(): void {
    this.show = false
    this.resolveDialog(false)
  }
}

</script>