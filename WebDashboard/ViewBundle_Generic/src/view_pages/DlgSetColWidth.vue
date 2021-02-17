<template>

 <v-dialog v-model="dialog" max-width="350" @keydown.esc="cancel">
    <v-card>
      <v-card-title>
        <span class="headline">Select Column Width</span>
      </v-card-title>
      <v-card-text>
        <v-autocomplete v-model="width" item-text="name" item-value="id" :items="entries"></v-autocomplete>
      </v-card-text>
      <v-card-actions class="pt-0">
        <v-spacer></v-spacer>
        <v-btn color="grey darken-1" text @click.native="cancel">Cancel</v-btn>
        <v-btn color="primary darken-1" text @click.native="agree">OK</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import * as model from './model'

interface Entry {
  id: model.ColumnWidth
  name: string
}

@Component({
  components: {
  },
})
export default class DlgSetColWidth extends Vue {

  dialog = false
  width: model.ColumnWidth = 'Fill'
  entries: Entry[] = []

  resolve: (v: model.ColumnWidth) => void = (x) => { }

  open(w: model.ColumnWidth): Promise<model.ColumnWidth> {
    this.width = w
    this.dialog = true
    this.entries = this.getEntries()
    return new Promise<model.ColumnWidth>((resolve, reject) => {
      this.resolve = resolve
    })
  }

  agree(): void {
    this.resolve(this.width)
    this.dialog = false
  }

  cancel(): void {
    this.resolve(this.width)
    this.dialog = false
  }

  getEntries(): Entry[] {
    return [
      { id: 'Fill',           name: 'Fill' },
      { id: 'Auto',           name: 'Auto' },
      { id: 'OneOfTwelve',    name: '1 of 12' },
      { id: 'TwoOfTwelve',    name: '2 of 12' },
      { id: 'ThreeOfTwelve',  name: '3 of 12' },
      { id: 'FourOfTwelve',   name: '4 of 12' },
      { id: 'FiveOfTwelve',   name: '5 of 12' },
      { id: 'SixOfTwelve',    name: '6 of 12' },
      { id: 'SevenOfTwelve',  name: '7 of 12' },
      { id: 'EightOfTwelve',  name: '8 of 12' },
      { id: 'NineOfTwelve',   name: '9 of 12' },
      { id: 'TenOfTwelve',    name: '10 of 12' },
      { id: 'ElevenOfTwelve', name: '11 of 12' },
      { id: 'TwelveOfTwelve', name: '12 of 12' },
    ]
  }
}

</script>
