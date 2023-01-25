<template>

  <v-dialog v-model="dialog" max-width="350" @keydown.esc="cancel">
     <v-card>
       <v-card-title>
         <span class="headline">{{ title }}</span>
       </v-card-title>
       <v-card-text>
        <p>{{ message }}</p>
         <v-container>
          <v-row>
            <v-btn class="mx-2" v-for="it in values" :key="it" :color="it === value ? 'primary' : ''" @click="value = it">
              {{ it }}
            </v-btn>
          </v-row>
         </v-container>
         
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
 export default class DlgEnumInput extends Vue {
 
   dialog = false
   title = ''
   message = ''
   value = ''
   valueOriginal = ''
   values: string[] = []
   resolve: (v: string | null) => void = (x) => { }

   open(title: string, message: string, value: string, values: string[]): Promise<string | null> {
     this.title = title
     this.message = message
     this.value = value
     this.valueOriginal = value
     this.values = values
     this.dialog = true
     return new Promise<string | null>((resolve, reject) => {
       this.resolve = resolve
     })
   }

   get canOK(): boolean {
    return this.value !== this.valueOriginal
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
 