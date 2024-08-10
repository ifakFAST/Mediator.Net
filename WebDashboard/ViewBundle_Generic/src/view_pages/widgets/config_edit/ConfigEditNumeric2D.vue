<template>
  <div>

    <div>
      <v-simple-table :height="theHeight" dense>
        <template v-slot:default>
          <thead>
            <tr>
              <th class="text-left"  style="font-size:14px;"></th>
              <th v-if="config.UnitRenderMode === 'ColumnLeft'" class="text-left" style="font-size:14px;"></th>
              <template v-for="(column, colIdx) in config.Columns">
                <th :key="column + '-1'" class="text-right" style="font-size:14px; height:36px;" :style="varHeaderStyle(colIdx)">{{ column }}</th>
                <th :key="column + '-2'" v-if="config.UnitRenderMode === 'Cell'" class="text-left" style="font-size:14px;"></th>
              </template>
              <th v-if="config.UnitRenderMode === 'ColumnRight'" class="text-left" style="font-size:14px;"></th>
            </tr>
          </thead>
          <tbody>

            <tr v-if="config.UnitRenderMode === 'Row'">
              <td></td>
              <td v-for="(col, colIdx) in config.Columns" :key="colIdx" class="text-right" style="font-size:14px; height:36px;padding-right:0px;">
                {{ unitFromColumn(colIdx) }}
              </td>
            </tr>

            <tr v-for="(row, rowIdx) in config.Rows" :key="rowIdx">

              <td class="text-left"  style="font-size:14px; height:36px; padding-right:0px;">
                {{ row }}
              </td>

              <td v-if="config.UnitRenderMode === 'ColumnLeft'" class="text-left" style="font-size:14px;padding-right:0px;">{{ unitFromRow(rowIdx)  }}</td>

              <template v-for="(col, colIdx) in config.Columns">
                <td :key="col + '-1'" @click="onWriteItem(rowIdx, colIdx)" class="text-right" style="font-size:14px; height:36px; min-width: 67px;" :style="varItemStyle(rowIdx, colIdx)">
                  {{ valueForItem(rowIdx, colIdx) }}  
                </td>
                <td :key="col + '-2'" v-if="config.UnitRenderMode === 'Cell'" class="text-left" style="font-size:14px; padding-left:8px;">
                  {{ itemFromRowColumn(rowIdx, colIdx).Unit }}
                </td>
              </template>

              <td v-if="config.UnitRenderMode === 'ColumnRight'" class="text-left" style="font-size:14px;">{{ unitFromRow(rowIdx)  }}</td>

            </tr>
          </tbody>
        </template>
      </v-simple-table>
    </div>

    <dlg-text-input ref="textInput"></dlg-text-input>
    <dlg-enum-input ref="enumInput"></dlg-enum-input>

  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import { TimeRange } from '../../../utils'
import DlgTextInput from '../../DlgTextInput.vue'
import DlgEnumInput from './DlgEnumInput.vue'
import { StyleValue } from 'vue/types/jsx'
import { EnumValEntry, parseEnumValues, onWriteItemEnum, onWriteItemNumeric } from './util'
export type UnitRenderMode = 'Hide' | 'Cell' | 'ColumnLeft' | 'ColumnRight' | 'Row'

interface Config {
  Rows: string[]
  Columns: string[]
  Items: ItemConfig[]
  UnitRenderMode: UnitRenderMode
}

interface ItemConfig {
  Unit: string
  Object: string | null
  Member: string | null
  Type: 'Range' | 'Enum'
  MinValue: number | null
  MaxValue: number | null
  EnumValues: string
}

interface VarItem {
  IsEmpty: boolean
  Value: string
  Unit: string
  CanEdit: boolean
}

@Component({
  components: {
    DlgTextInput,
    DlgEnumInput,
  },
})
export default class ConfigEditNumeric2D extends Vue {

  @Prop({ default() { return '' } }) id: string
  @Prop({ default() { return '' } }) width: string
  @Prop({ default() { return '' } }) height: string
  @Prop({ default() { return {} } }) config: Config
  @Prop() backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  @Prop({ default() { return '' } }) eventName: string
  @Prop({ default() { return {} } }) eventPayload: object
  @Prop({ default() { return {} } }) timeRange: TimeRange
  @Prop({ default() { return 0 } }) resize: number
  @Prop({ default() { return null } }) dateWindow: number[]

  items: VarItem[] = []
  canUpdateConfig = false

  get theHeight(): string {
    if (this.height.trim() === '') { return 'auto' }
    return this.height
  }

  configItemFromRowColumn(row: number, column: number): ItemConfig {
    const cols = this.config.Columns.length
    const index = row * cols + column
    const items = this.config.Items
    if (index < items.length) {
      return items[index]
    }
    return { Unit: '', Object: null, Member: null, Type: 'Range', MinValue: null, MaxValue: null, EnumValues: '' }
  }

  itemFromRowColumn(row: number, column: number): VarItem {
    const cols = this.config.Columns.length
    const index = row * cols + column
    const items = this.items
    if (index < items.length) {
      return items[index]
    }
    return { IsEmpty: true, Value: '', Unit: '', CanEdit: false }
  }

  unitFromRow(rowIndex: number): string {
    const cols = this.config.Columns.length
    const startIndexOfRow = rowIndex * cols
    const endIndexOfRow = startIndexOfRow + cols
    const items = this.items
    for (let i = startIndexOfRow; i < endIndexOfRow; i++) {
      if (i < items.length && items[i].Unit !== '') {
        return items[i].Unit
      }
    }
    return ''
  }

  unitFromColumn(columnIndex: number): string {
    const cols = this.config.Columns.length
    for (let i = columnIndex; i < this.items.length; i += cols) {
      if (this.items[i].Unit !== '') {
        return this.items[i].Unit
      }
    }
    return ''
  }

  varHeaderStyle(colIdx: number): StyleValue {
    const ZeroRightPadding: boolean = colIdx < this.config.Columns.length - 1
    const ZeroLeftPadding: boolean = this.config.UnitRenderMode === 'Cell'
    const style = {}
    if (ZeroLeftPadding) {
      style['padding-left'] = '0px'
    }
    if (ZeroRightPadding) {
      style['padding-right'] = '0px'
    }
    return style
  }

  varItemStyle(rowIdx: number, colIdx: number): StyleValue {
    const color = this.colorForItem(rowIdx, colIdx)
    const ZeroRightPadding: boolean = colIdx < this.config.Columns.length - 1
    const ZeroLeftPadding: boolean = this.config.UnitRenderMode === 'Cell'
    const style = {}
    
    const varItem: VarItem = this.itemFromRowColumn(rowIdx, colIdx)

    if (varItem.CanEdit && varItem.IsEmpty === false) {
      style['cursor'] = 'pointer'
    }

    if (color !== '') {
      style['color'] = color
    }
    if (ZeroLeftPadding) {
      style['padding-left'] = '0px'
    }
    if (ZeroRightPadding) {
      style['padding-right'] = '0px'
    }
    return style
  }

  valueForItem(rowIdx: number, colIdx: number): string {
    const configItem: ItemConfig = this.configItemFromRowColumn(rowIdx, colIdx)
    const varItem: VarItem = this.itemFromRowColumn(rowIdx, colIdx)
    if (configItem.Type === 'Enum') {
      const vals: EnumValEntry[] = parseEnumValues(configItem.EnumValues)
      const v = varItem.Value
      const vnum: number = parseFloat(v)
      for (const item of vals) {
        if (item.num === vnum) {
          return item.label
        }
      }
      return v
    }
    return varItem.Value
  }

  colorForItem(rowIdx: number, colIdx: number): string {
    const configItem: ItemConfig = this.configItemFromRowColumn(rowIdx, colIdx)
    if (configItem.Type === 'Enum') {
      const varItem: VarItem = this.itemFromRowColumn(rowIdx, colIdx)
      const vals: EnumValEntry[] = parseEnumValues(configItem.EnumValues)
      const v = varItem.Value
      const vnum: number = parseFloat(v)
      for (const item of vals) {
        if (item.num === vnum) {
          return item.color ?? ''
        }
      }
      return ''
    }
    return ''
  }

  mounted(): void {
    this.items = this.config.Items.map((it) => {
      const ret: VarItem = {
        IsEmpty: false,
        Value: '',
        Unit: it.Unit,
        CanEdit: false,
      }
      return ret
    })
    this.onLoadData()
    this.canUpdateConfig = window.parent['dashboardApp'].canUpdateViewConfig()
  }
 
  get configItems(): ItemConfig[] {
    return this.config.Items ?? []
  }

  async onLoadData(): Promise<void> {
    const items: VarItem[] = await this.backendAsync('ReadValues', { })
    this.items = items
  }

  writeEnabled(it: ItemConfig): boolean {
    return true
  }

  async onWriteItem(rowIdx: number, colIdx: number): Promise<void> {
    const it: ItemConfig = this.configItemFromRowColumn(rowIdx, colIdx)
    const value = this.valueForItem(rowIdx, colIdx)
    if (it.Type === 'Range') {
      await onWriteItemNumeric(it, value, this.textInputDlg, this.backendAsync)
    }
    else {
      await onWriteItemEnum(it, value, this.enumInputDlg, this.backendAsync)
    }
  }

  @Watch('eventPayload')
  watch_event(newVal: object, old: object): void {
    if (this.eventName === 'OnValuesChanged') {
      const updatedItems: VarItem[] = this.eventPayload as any
      if (updatedItems.length !== this.items.length) { return }
      this.items.forEach((item, index) => {
        const updated = updatedItems[index]
        if (!updated.IsEmpty) {          
          item.Value = updated.Value
          item.Unit = updated.Unit
          item.CanEdit = updated.CanEdit
        }
      });      
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
}

</script>
