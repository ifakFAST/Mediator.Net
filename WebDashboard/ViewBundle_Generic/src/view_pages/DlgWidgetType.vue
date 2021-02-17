<template>

 <v-dialog v-model="dialog" max-width="350" @keydown.esc="cancel">
    <v-card>
      <v-card-title>
        <span class="headline">Select Widget Type</span>
      </v-card-title>
      <v-card-text>
        <v-autocomplete v-model="type" item-text="name" item-value="id" :items="entries"></v-autocomplete>
      </v-card-text>
      <v-card-actions class="pt-0">
        <v-spacer></v-spacer>
        <v-btn color="grey darken-1" text @click.native="cancel">Cancel</v-btn>
        <v-btn color="primary darken-1" :disabled="!canOK" text @click.native="ok">OK</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import * as model from './model'

interface Entry {
  id: string
  name: string
}

@Component({
  components: {
  },
})
export default class DlgWidgetType extends Vue {

  dialog = false
  type: string | null = null
  entries: Entry[] = []

  resolve: (v: string | null) => void = (x) => { }

  open(allTypes: string[], type?: string): Promise<string | null> {
    this.type = type || null
    this.dialog = true
    this.entries = this.getEntries(allTypes)
    return new Promise<string | null>((resolve, reject) => {
      this.resolve = resolve
    })
  }

  get canOK(): boolean {
    return this.type !== null
  }

  ok(): void {
    this.resolve(this.type)
    this.dialog = false
  }

  cancel(): void {
    this.resolve(null)
    this.dialog = false
  }

  getEntries(allTypes: string[]): Entry[] {
    return allTypes.map((t) => {
      return { id: t, name: t }
    })
  }
}

</script>
