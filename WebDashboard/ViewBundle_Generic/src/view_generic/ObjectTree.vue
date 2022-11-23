<template>
  <li>
    <div :class="{bold: isSelected}">
      <span @click="open = true"  v-if="isFolder && !open" style="cursor: pointer;"><v-icon>keyboard_arrow_right</v-icon></span>
      <span @click="open = false" v-if="isFolder &&  open" style="cursor: pointer;"><v-icon>keyboard_arrow_down</v-icon></span>
      <span v-if="!isFolder" style="cursor: pointer;">{{ '\xa0\u2022\xa0\xa0' }}</span>
      <span style="cursor: pointer;" draggable="true" @dragstart="onDragStart" @dragover="onDragOver" @drop="onDrop" @click="selectObject">{{shortText}}</span>
      <span v-if="showVariableValue" v-bind:style="{ color: qualityColor }">
          {{ '\xa0\xa0' + varValue }}
      </span>
      <span v-if="!showStruct && showVariableValue && firstVar.Struct" @click="showStruct = !showStruct" style="cursor: pointer;"><v-icon>keyboard_arrow_right</v-icon></span>
      <span v-if=" showStruct && showVariableValue && firstVar.Struct" @click="showStruct = !showStruct" style="cursor: pointer;"><v-icon>keyboard_arrow_down</v-icon></span>
    </div>
    <struct-view v-if="showStruct && showVariableValue && firstVar.Struct" :value="firstVar.V" :vertical="firstVar.Dim !== 1"></struct-view>
    <ul style="padding-left:1em;" v-if="open">
      <object-tree v-for="child in modelChildren" :key="child.ID" :model="child"
          :selection-id="selectionId"
          :initial-open="false"
          :type-info="typeInfo"
          @selectObject="onSubObjectSelected"
          @dragDrop="onSubDragDrop"></object-tree>
    </ul>
  </li>
</template>

<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import { TreeNode, VariableVal, TypeMap, ObjMemInfo } from './types'
import StructView from '../components/StructView.vue'

@Component({
  name: 'object-tree',
  components: {
    StructView,
  },
})
export default class ObjectTree extends Vue {

  @Prop(Object) model: TreeNode
  @Prop(String) selectionId: string
  @Prop(Boolean)initialOpen: boolean
  @Prop(Object) typeInfo: TypeMap

  open: boolean = false
  showStruct: boolean = false

  mounted() {
    this.open = this.initialOpen
  }

  get firstVar(): VariableVal {
    return this.model.Variables[0]
  }

  get varValue(): string {
    const str = this.firstVar.V
    const MaxLen = 25
    if (str.length > MaxLen) {
      return str.substring(0, MaxLen) + '\u00A0...'
    }
    else {
      return str
    }
  }

  get modelChildren(): TreeNode[] {
    if (this.model === null) { return [] }
    return this.model.Children
  }

  get isFolder(): boolean {
    return this.model !== null && this.model.Children.length > 0
  }

  get isSelected(): boolean {
    return this.model !== null && this.model.ID === this.selectionId
  }

  get shortText(): string {
    if (this.model !== null) {
      const fullType = this.model.Type
      const i = fullType.lastIndexOf('.')
      let typeName = fullType
      if (i > 0) {
        typeName = fullType.substring(i + 1)
      }
      if (typeName === this.model.Name) { return typeName }
      if (typeName === 'DataItem') { return this.model.Name }
      if (typeName === 'Node') { return this.model.Name }
      return typeName + ' ' + this.model.Name
    }
    return ''
  }

  get showVariableValue(): boolean {
    if (this.model === null) { return false }
    return this.model.Variables.length === 1
  }

  get qualityColor(): string {
    if (this.model === null) { return 'black' }
    const q = this.firstVar.Q
    if (q === 'Good') { return 'green' }
    if (q === 'Uncertain') { return 'orange' }
    return 'red'
  }

  @Watch('selectionId')
  watch_selectionId(newSel: string, oldSel: string) {
    if (!this.open && this.containsChild(this.model, newSel)) {
      this.open = true
    }
  }

  selectObject() {
    this.$emit('selectObject', this.model)
  }

  onSubObjectSelected(selectObject) {
    this.$emit('selectObject', selectObject)
  }

  containsChild(tree: TreeNode, id: string): boolean {
    for (const child of tree.Children) {
      if (child.ID === id) { return true }
      const res = this.containsChild(child, id)
      if (res === true) { return true }
    }
    return false
  }

  onDragStart(event) {
    const data = {
      ID:   this.model.ID,
      Type: this.model.Type,
    }
    event.dataTransfer.setData('text/plain', JSON.stringify(data))
  }

  onDragOver(event) {
    const data = JSON.parse(event.dataTransfer.getData('text/plain'))
    if (this.getMatchingMembers(data.Type).length === 1) {
      event.preventDefault()
    }
  }

  onDrop(event) {
    event.preventDefault()
    const data = JSON.parse(event.dataTransfer.getData('text/plain'))
    const memberWithType = this.getMatchingMembers(data.Type)
    this.$emit('dragDrop', data.ID, this.model.ID, memberWithType[0].Array)
  }

  onSubDragDrop(fromID: string, toID: string, toArrayName: string) {
    this.$emit('dragDrop', fromID, toID, toArrayName)
  }

  getMatchingMembers(type: string): ObjMemInfo[] {
    const members = this.typeInfo[this.model.Type].ObjectMembers
    return members.filter((m) => m.Type === type)
  }

}

</script>

<style>

  .fast-struct-table table thead th {
    font-size: 14px;
  }

  .fast-struct-table table tbody td {
    font-size: 14px;
  }

</style>