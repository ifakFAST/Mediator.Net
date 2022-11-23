<template>

 <v-dialog v-model="dialog" max-width="350" @keydown.esc="cancel">
    <v-card>
      <v-card-title>
        <span class="headline">{{ title }}</span>
      </v-card-title>
      <v-card-text>
        {{ message }}
        <v-text-field :hint="error" :error="!canOK" v-model="value" ref="editText"></v-text-field>        
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

import { Component, Vue } from 'vue-property-decorator'

@Component({
  components: {
  },
})
export default class DlgTextInput extends Vue {

  dialog = false
  title = ''
  message = ''
  value = ''

  isValid: (s: string) => string = (x) => x.length > 0 ? '' : 'Must not be empty'

  resolve: (v: string | null) => void = (x) => { }

  openWithValidator(title: string, message: string, value: string, valid: (str: string) => string): Promise<string | null> {
    this.isValid = valid
    return this.open(title, message, value)
  }

  get error(): string {
    return this.isValid(this.value)
  }

  open(title: string, message: string, value: string): Promise<string | null> {
    this.title = title
    this.message = message
    this.value = value
    this.dialog = true
    const context = this
    setTimeout(() => {
      const editText = context.$refs.editText as HTMLElement
      editText.focus()
    }, 100)
    return new Promise<string | null>((resolve, reject) => {
      this.resolve = resolve
    })
  }

  get canOK(): boolean {
    return this.isValid(this.value) === ''
  }

  ok(): void {
    this.resolve(this.value)
    this.dialog = false
  }

  cancel(): void {
    this.resolve(null)
    this.dialog = false
  }
}

</script>
