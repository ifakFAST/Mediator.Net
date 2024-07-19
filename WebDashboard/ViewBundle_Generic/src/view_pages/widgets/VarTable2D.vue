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
                <td :key="col + '-1'" class="text-right" style="font-size:14px; height:36px; min-width: 67px;" :style="varItemStyle(rowIdx, colIdx)">
                  <v-tooltip right open-delay="250">
                    <template v-slot:activator="{ on, attrs }">
                      <span v-bind="attrs" v-on="on">
                        <span v-bind:style="{ color: itemFromRowColumn(rowIdx, colIdx).ValueColor }">
                          {{ itemFromRowColumn(rowIdx, colIdx).Value }}
                        </span>
                      </span>
                    </template>
                    <span>{{ varItemInfo(itemFromRowColumn(rowIdx, colIdx)) }}</span>
                  </v-tooltip>
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

  </div>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import * as fast from '../../fast_types'
import { TimeRange } from '../../utils'
import DlgObjectSelect from '../../components/DlgObjectSelect.vue'
import TextFieldNullableNumber from '../../components/TextFieldNullableNumber.vue'
import { ModuleInfo, ObjectMap, Obj, SelectObject, ObjInfo } from './common'
import { StyleValue } from 'vue/types/jsx'

export type UnitRenderMode = 'Hide' | 'Cell' | 'ColumnLeft' | 'ColumnRight' | 'Row'

interface Config {
  Rows: string[]
  Columns: string[]
  Items: ItemConfig[]
  UnitRenderMode: string
}

interface ItemConfig {
  Variable: fast.VariableRef
  WarnBelow: number | null
  WarnAbove: number | null
  AlarmBelow: number | null
  AlarmAbove: number | null
  EnumValues: string
}

interface VarItem {
  IsEmpty: boolean
  Value: string
  ValueColor: string
  Unit: string
  Time: string
  Warning?: string
  Alarm?: string
}

@Component({
  components: {
    DlgObjectSelect,
    TextFieldNullableNumber,
  },
})
export default class VarTable2D extends Vue {

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

  itemFromRowColumn(row: number, column: number): VarItem {
    const cols = this.config.Columns.length
    const index = row * cols + column
    const items = this.items
    if (index < items.length) {
      return items[index]
    }
    return { IsEmpty: true, Value: '', ValueColor: '', Unit: '', Time: '' }
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
    const item: VarItem = this.itemFromRowColumn(rowIdx, colIdx)
    const ZeroRightPadding: boolean = colIdx < this.config.Columns.length - 1
    const ZeroLeftPadding: boolean = this.config.UnitRenderMode === 'Cell'
    const style = {}
    if (!!item.Alarm || !!item.Warning) {
      style['font-weight'] = 'bold'
      style['color'] = this.varItemColor(item)
    }
    if (ZeroLeftPadding) {
      style['padding-left'] = '0px'
    }
    if (ZeroRightPadding) {
      style['padding-right'] = '0px'
    }
    return style
  }

  varItemInfo(item: VarItem): string {
    if (item.Alarm)   { return item.Alarm }
    if (item.Warning) { return item.Warning }
    return item.Time
  }

  varItemColor(item: VarItem): string {
    if (item.Alarm)   { return 'red' }
    if (item.Warning) { return 'orange' }
    return ''
  }

  mounted(): void {
    this.items = this.config.Items.map((it) => {
      const ret: VarItem = {
        IsEmpty: false,
        Value: '',
        ValueColor: '',
        Unit: '',
        Time: '',
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
    const items: VarItem[] = await this.backendAsync('LoadData', { })
    this.items = items
  }

  @Watch('eventPayload')
  watch_event(newVal: object, old: object): void {
    if (this.eventName === 'OnVarChanged') {
      const updatedItems: VarItem[] = this.eventPayload as any
      if (updatedItems.length !== this.items.length) { return }
      this.items.forEach((item, index) => {
        const updated = updatedItems[index]
        if (!updated.IsEmpty) {          
          item.Value = updated.Value
          item.ValueColor = updated.ValueColor
          item.Time = updated.Time
          item.Warning = updated.Warning
          item.Alarm = updated.Alarm
        }
      });      
    }
  }
}

</script>
