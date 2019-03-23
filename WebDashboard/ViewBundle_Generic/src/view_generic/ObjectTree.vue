<template>
  <li>
    <div :class="{bold: isSelected}" draggable="true" @dragstart="onDragStart" @dragover="onDragOver" @drop="onDrop">
      <span @click="open = true"  v-if="isFolder && !open"><v-icon>keyboard_arrow_right</v-icon></span>
      <span @click="open = false" v-if="isFolder &&  open"><v-icon>keyboard_arrow_down</v-icon></span>
      <span v-if="!isFolder">&nbsp;&nbsp;&nbsp;&#8226;&nbsp;&nbsp;</span>
      <span @click="selectObject">{{shortText}}</span>
      <span v-if="showVariableValue" v-bind:style="{ color: qualityColor }">
            &nbsp;&nbsp;{{ model.Variables[0].V }}
      </span>
    </div>
    <ul v-if="open">
      <object-tree class="item" v-for="child in modelChildren" :key="child.ID" :model="child"
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
import { TreeNode, TypeMap, ObjMemInfo } from './types'

@Component({
  name: 'object-tree',
})
export default class ObjectTree extends Vue {

  @Prop(Object) model: TreeNode
  @Prop(String) selectionId: string
  @Prop(Boolean)initialOpen: boolean
  @Prop(Object) typeInfo: TypeMap

  open: boolean = this.initialOpen

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
    const q = this.model.Variables[0].Q
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
