<template>
  <v-dialog v-model="dialog" max-width="600" @keydown.esc="cancel">
    <v-card>
      <v-card-title>
        <span class="headline">Config Variables</span>
      </v-card-title>
      <v-card-text>
        <v-container fluid class="pa-0 pt-6">
          <template v-for="(variable, index) in configVariables">
            <v-row :key="index" class="mb-2">
              <v-col cols="auto" class="py-0">
                <v-text-field
                  style="max-width: 115px;"
                  v-model="variable.ID"
                  label="ID"
                  dense
                  hide-details="auto"
                />
              </v-col>
              <v-col cols="fill" class="py-0">
                <v-text-field
                  v-model="variable.DefaultValue"
                  label="Default Value"
                  dense
                  hide-details="auto"
                />
              </v-col>
              <v-col cols="auto" class="py-0 pl-0 d-flex">
                <div class="d-flex flex-row">
                  <v-btn
                    icon
                    color="error"
                    @click="removeVariable(index)"
                  >
                    <v-icon>mdi-delete</v-icon>
                  </v-btn>
                  <v-btn
                    icon                    
                    :disabled="index === 0"
                    @click="moveVariable(index, 'up')"
                  >
                    <v-icon>mdi-arrow-up</v-icon>
                  </v-btn>
                </div>
              </v-col>
            </v-row>

          </template>
        </v-container>


        <div class="text-center">
          <v-btn
            color="primary"
            text          
            class="mt-2"
            @click="addVariable"
          >
            Add Variable
          </v-btn>
        </div>

      </v-card-text>
      <v-card-actions class="pt-0">
        <v-spacer></v-spacer>
        <v-btn color="grey darken-1" text @click.native="cancel">Cancel</v-btn>
        <v-btn color="primary darken-1" text @click.native="onOK">OK</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script lang="ts">
import { Component, Vue } from 'vue-property-decorator'
import { ConfigVariable } from './model'

@Component({
  components: {},
})
export default class DlgConfigVariables extends Vue {
  dialog = false
  configVariables: ConfigVariable[] = []

  resolve: (v: ConfigVariable[] | null) => void = (x) => {}

  open(configVariables: ConfigVariable[]): Promise<ConfigVariable[] | null> {
    this.configVariables = JSON.parse(JSON.stringify(configVariables)) // Deep copy
    this.dialog = true
    return new Promise<ConfigVariable[]>((resolve, reject) => {
      this.resolve = resolve
    })
  }

  moveVariable(index: number, direction: 'up' | 'down'): void {
    const newIndex = direction === 'up' ? index - 1 : index + 1
    
    // Ensure the new index is within bounds
    if (newIndex < 0 || newIndex >= this.configVariables.length) {
      return
    }

    // Swap the items
    const temp = this.configVariables[index]
    this.$set(this.configVariables, index, this.configVariables[newIndex])
    this.$set(this.configVariables, newIndex, temp)
  }

  addVariable(): void {
    this.configVariables.push({
      ID: '',
      DefaultValue: '',
    })
  }

  removeVariable(index: number): void {
    this.configVariables.splice(index, 1)
  }

  async onOK(): Promise<void> {
    try {
      await window.parent['dashboardApp'].sendViewRequestAsync('SaveConfigVariables', { configVariables: this.configVariables })
      this.resolve(this.configVariables)
      this.dialog = false
    }
    catch (err) {
      const exp = err as Error
      alert(exp.message)
      return
    }
  }

  cancel(): void {
    this.resolve(null)
    this.dialog = false
  }
}
</script>