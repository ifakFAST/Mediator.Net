<template>
    <div>

        <div @contextmenu="onContextMenu">

            <v-simple-table :height="theHeight" dense>
              <template v-slot:default>
                <thead v-if="config.ShowHeader">
                  <tr>
                    <th class="text-left" style="font-size:14px;">Setting</th>
                    <th class="text-right" style="font-size:14px; height:36px; padding-right:0px; min-width: 55px;">Value</th>
                    <th v-if="hasUnitColumn" class="text-left" style="font-size:14px; height:36px;"></th>
                    <th>&nbsp;</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="it in config.Items" :key="it.Name" >
                    <td class="text-left" style="font-size:14px; height:36px;">
                      {{ it.Name }}
                    </td>
                    <td class="text-right" style="font-size:14px; height:36px; padding-right:0px; min-width: 55px;">
                      <span v-bind:style="{ color: colorForItem(it) }"> {{ valueForItem(it, '') }} </span>
                    </td>
                    <td v-if="hasUnitColumn" class="text-left" style="font-size:14px; height:36px; padding-left:8px; padding-right:0px;">
                      {{ it.Unit }}
                    </td>
                    <td style="font-size:14px; height:36px;" class="pl-5 pr-4">
                      <v-icon style="font-size: 21px;" :disabled="!writeEnabled(it)" @click="onWriteItem(it)">edit</v-icon>                      
                    </td>
                  </tr>
                </tbody>
              </template>
            </v-simple-table>

        </div>

        <v-menu v-model="contextMenu.show" :position-x="contextMenu.clientX" :position-y="contextMenu.clientY" absolute offset-y>
          <v-list>
            <v-list-item @click="onToggleShowHeader" >
              <v-list-item-title> {{ config.ShowHeader ? 'Hide Header' : 'Show Header' }}</v-list-item-title>
            </v-list-item>
            <v-list-item @click="onConfigureItems" >
              <v-list-item-title>Configure Items...</v-list-item-title>
            </v-list-item>
          </v-list>
        </v-menu>

        <dlg-config-items ref="dlgConfigItems" :backendAsync="backendAsync" :configItems="config.Items" ></dlg-config-items>
        <dlg-text-input ref="textInput"></dlg-text-input>
        <dlg-enum-input ref="enumInput"></dlg-enum-input>

    </div>
  </template>
  
  <script lang="ts">
  
  import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
  import DlgTextInput from '../../DlgTextInput.vue'
  import DlgEnumInput from './DlgEnumInput.vue'
  import DlgConfigItems from './DlgConfigItems.vue'
  import { Config, ConfigItem, ItemValue } from './types'
  import { EnumValEntry, parseEnumValues, onWriteItemEnum, onWriteItemNumeric } from './util'

  @Component({
    components: {
      DlgConfigItems,
      DlgEnumInput,
      DlgTextInput,
    },
  })
  export default class ConfigEditNumeric extends Vue {
  
    @Prop({ default() { return '' } }) id: string
    @Prop({ default() { return '' } }) width: string
    @Prop({ default() { return '' } }) height: string
    @Prop({ default() { return {} } }) config: Config
    @Prop() backendAsync: (request: string, parameters: object) => Promise<any>
    @Prop({ default() { return '' } }) eventName: string
    @Prop({ default() { return {} } }) eventPayload: any
    @Prop({ default() { return {} } }) timeRange: object
    @Prop({ default() { return 0 } }) resize: number
  
    result: ItemValue[] = []
  
    contextMenu = {
      show: false,
      clientX: 0,
      clientY: 0,
    }

    canUpdateConfig = false

    async mounted(): Promise<void> {
      await this.readValues()
      this.canUpdateConfig = window.parent['dashboardApp'].canUpdateViewConfig()
    }

    async readValues(): Promise<void> {
      try {
        const result = await this.backendAsync('ReadValues', { })
        this.result = result
      }
      catch (exp) {
        alert(exp)
      }
    }

    get theHeight(): string {
      if (this.height.trim() === '') { return 'auto' }
      return this.height
    }

    get hasUnitColumn(): boolean {
      return this.config.Items.some((it) => it.Unit.trim() !== '')
    }

    writeEnabled(it: ConfigItem): boolean {
      const hasConfig = it.Object !== undefined && it.Object !== null && it.Object !== '' && it.Member !== undefined && it.Member !== null && it.Member !== ''
      if (!hasConfig) { return false }
      for (const entry of this.result) {
        if (entry.Object === it.Object && entry.Member === it.Member) {
          return entry.CanEdit
        }
      }
      return false
    }
  
    async onWriteItem(it: ConfigItem): Promise<void> {
      const oldValue: string = this.valueForItem(it, '')
      if (it.Type === 'Range') {        
        await onWriteItemNumeric(it, oldValue, this.textInputDlg, this.backendAsync)
      }
      else {
        await onWriteItemEnum(it, oldValue, this.enumInputDlg, this.backendAsync)
      }
    }

    valueForItem(it: ConfigItem, defaultValue: string): string {
      if (it.Type === 'Enum') {
        const vals: EnumValEntry[] = parseEnumValues(it.EnumValues)
        for (const entry of this.result) {
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
      }
      else {
        for (const entry of this.result) {
          if (entry.Object === it.Object && entry.Member === it.Member) {
            return entry.Value
          }
        }
      }
      return defaultValue
    }

    colorForItem(it: ConfigItem): string {
      if (it.Type === 'Enum') {
        const vals: EnumValEntry[] = parseEnumValues(it.EnumValues)
        for (const entry of this.result) {
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
      else {
        return ''
      }
    }

    async onToggleShowHeader(): Promise<void> {
      try {
        await this.backendAsync('ToggleShowHeader', {})
      } 
      catch (err) {
        alert(err.message)
      }
    }

    @Watch('eventPayload')
    watch_event(newVal: any, old: any): void {
      if (this.eventName === 'OnValuesChanged') {
        // console.info('OnValuesChanged event! ' + JSON.stringify(newVal))
        this.result = newVal
      }
    }
  
    async textInputDlg(title: string, message: string, value: string, valid: (str: string) => string): Promise<string | null> {
      const textInput = this.$refs.textInput as DlgTextInput
      return textInput.openWithValidator(title, message, value, valid)
    }

    async enumInputDlg(title: string, message: string, value: string, values: string[]): Promise<string | null> {
      const enumInput = this.$refs.enumInput as DlgEnumInput
      return enumInput.open(title, message, value, values)
    }

    onContextMenu(e: any): void {
      if (this.canUpdateConfig) {
        e.preventDefault()
        e.stopPropagation()
        this.contextMenu.show = false
        this.contextMenu.clientX = e.clientX
        this.contextMenu.clientY = e.clientY
        const context = this
        this.$nextTick(() => {
          context.contextMenu.show = true
        })
      }
    }

    async onConfigureItems(): Promise<void> {
      const dlg = this.$refs.dlgConfigItems as DlgConfigItems
      const ok = await dlg.showDialog()
      if (ok) {
        await this.readValues()
      }
    }
  }
  
  </script>
  