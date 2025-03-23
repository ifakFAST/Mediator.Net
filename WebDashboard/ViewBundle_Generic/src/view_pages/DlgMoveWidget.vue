<template>
  <v-dialog v-model="show" max-width="500px" persistent @keydown.esc="onCancel">
    <v-card>
      <v-card-title>
        {{ title }}
      </v-card-title>
      <v-card-text>
        <v-container>
          <v-row>
            <v-col cols="12">
              <p>Select the row and column where you want to move this widget:</p>
            </v-col>
          </v-row>
          <v-row>
            <v-col cols="12" sm="6">
              <v-select
                ref="rowSelect"
                v-model="selectedRow"
                :items="rowOptions"
                item-text="text"
                item-value="id"
                label="Target Row"
                @change="onRowChanged"
              ></v-select>
            </v-col>
            <v-col cols="12" sm="6">
              <v-select
                v-model="selectedColumn"
                :items="availableColumns"
                item-text="text"
                item-value="id"
                label="Target Column"
                :disabled="!selectedRow && selectedRow !== 0"
              ></v-select>
            </v-col>
          </v-row>
        </v-container>
      </v-card-text>
      <v-card-actions>
        <v-spacer></v-spacer>
        <v-btn color="blue darken-1" text @click="onCancel">Cancel</v-btn>
        <v-btn color="blue darken-1" text @click="onOK" :disabled="!isValidNewPosition">Move</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script lang="ts">
import { Component, Vue, Watch } from 'vue-property-decorator'

@Component
export default class DlgMoveWidget extends Vue {
  show = false
  title = ''
  rowOptions: { id: number; text: string }[] = []
  columnOptionsMap: { [rowId: number]: { id: number; text: string }[] } = {}
  selectedRow: number | null = null
  selectedColumn: number | null = null
  initialRow: number = -1
  initialColumn: number = -1
  resolve: ((value: { targetRow: number; targetCol: number } | null) => void) | null = null

  get availableColumns(): { id: number; text: string }[] {
    if (this.selectedRow === null) return []
    return this.columnOptionsMap[this.selectedRow] || []
  }

  get isSelectionValid(): boolean {
    return this.selectedRow !== null && this.selectedColumn !== null
  }

  get isValidNewPosition(): boolean {
    if (!this.isSelectionValid) return false
    
    // Disable Move button if the selection is the same as the initial position
    return !(this.selectedRow === this.initialRow && this.selectedColumn === this.initialColumn)
  }

  onRowChanged(): void {
    // Reset column when row changes
    this.selectedColumn = null
  }

  @Watch('show')
  onShowChanged(val: boolean): void {
    if (val) {
      // Focus the row select dropdown when the dialog opens
      this.$nextTick(() => {
        const rowSelect = this.$refs.rowSelect as any
        if (rowSelect) {
          rowSelect.focus()
        }
      })
    }
  }

  async open(
    title: string,
    options: { 
      rows: { id: number; text: string }[];
      columns: { [rowId: number]: { id: number; text: string }[] }
    },
    currentRow: number,
    currentColumn: number
  ): Promise<{ targetRow: number; targetCol: number } | null> {
    this.title = title
    this.rowOptions = options.rows
    this.columnOptionsMap = options.columns
    this.selectedRow = currentRow
    this.selectedColumn = currentColumn
    this.initialRow = currentRow
    this.initialColumn = currentColumn
    this.show = true

    return new Promise((resolve) => {
      this.resolve = resolve
    })
  }

  onOK(): void {
    if (this.resolve && this.selectedRow !== null && this.selectedColumn !== null) {
      this.resolve({
        targetRow: this.selectedRow,
        targetCol: this.selectedColumn
      })
    }
    this.show = false
    this.resolve = null
  }

  onCancel(): void {
    if (this.resolve) {
      this.resolve(null)
    }
    this.show = false
    this.resolve = null
  }
}
</script>
