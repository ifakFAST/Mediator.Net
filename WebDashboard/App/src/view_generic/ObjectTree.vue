<template>
  <li>
    <div :class="{ bold: isSelected }">
      <span
        v-if="isFolder && !open"
        style="cursor: pointer"
        @click="open = true"
        ><v-icon>mdi-chevron-right</v-icon></span
      >
      <span
        v-if="isFolder && open"
        style="cursor: pointer"
        @click="open = false"
        ><v-icon>mdi-chevron-down</v-icon></span
      >
      <span
        v-if="!isFolder"
        style="cursor: pointer"
        >{{ '\xa0\u2022\xa0\xa0' }}</span
      >
      <span
        draggable="true"
        style="cursor: pointer"
        @click="selectObject"
        @dragover="onDragOver"
        @dragstart="onDragStart"
        @drop="onDrop"
        >{{ shortText }}</span
      >
      <span
        v-if="showVariableValue"
        :style="{ color: qualityColor }"
      >
        {{ '\xa0\xa0' + varValue }}
      </span>
      <span
        v-if="!showStruct && showVariableValue && firstVar.Struct"
        style="cursor: pointer"
        @click="showStruct = !showStruct"
        ><v-icon>mdi-chevron-right</v-icon></span
      >
      <span
        v-if="showStruct && showVariableValue && firstVar.Struct"
        style="cursor: pointer"
        @click="showStruct = !showStruct"
        ><v-icon>mdi-chevron-down</v-icon></span
      >
    </div>
    <struct-view
      v-if="showStruct && showVariableValue && firstVar.Struct"
      :value="firstVar.V"
      :vertical="firstVar.Dim !== 1"
    ></struct-view>
    <ul
      v-if="open"
      style="padding-left: 1em"
    >
      <object-tree
        v-for="child in modelChildren"
        :key="child.ID"
        :initial-open="false"
        :model="child"
        :selection-id="selectionId"
        :type-info="typeInfo"
        @drag-drop="onSubDragDrop"
        @select-object="onSubObjectSelected"
      ></object-tree>
    </ul>
  </li>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import type { TreeNode, VariableVal, TypeMap, ObjMemInfo } from './types'
import StructView from '../components/StructView.vue'

interface Props {
  model: TreeNode
  selectionId: string
  initialOpen: boolean
  typeInfo: TypeMap
}

const props = defineProps<Props>()

const emit = defineEmits<{
  (e: 'selectObject', model: TreeNode): void
  (e: 'dragDrop', fromID: string, toID: string, toArrayName: string): void
}>()

const open = ref(false)
const showStruct = ref(false)

onMounted(() => {
  open.value = props.initialOpen
})

const firstVar = computed((): VariableVal => {
  return props.model.Variables[0]
})

const varValue = computed((): string => {
  const str = firstVar.value.V
  const MaxLen = 25
  if (str.length > MaxLen) {
    return str.substring(0, MaxLen) + '\u00A0...'
  } else {
    return str
  }
})

const modelChildren = computed((): TreeNode[] => {
  if (props.model === null) {
    return []
  }
  return props.model.Children
})

const isFolder = computed((): boolean => {
  return props.model !== null && props.model.Children.length > 0
})

const isSelected = computed((): boolean => {
  return props.model !== null && props.model.ID === props.selectionId
})

const shortText = computed((): string => {
  if (props.model !== null) {
    const fullType = props.model.Type
    const i = fullType.lastIndexOf('.')
    let typeName = fullType
    if (i > 0) {
      typeName = fullType.substring(i + 1)
    }
    if (typeName === props.model.Name) {
      return typeName
    }
    if (typeName === 'DataItem') {
      return props.model.Name
    }
    if (typeName === 'Node') {
      return props.model.Name
    }
    return typeName + ' ' + props.model.Name
  }
  return ''
})

const showVariableValue = computed((): boolean => {
  if (props.model === null) {
    return false
  }
  return props.model.Variables.length === 1
})

const qualityColor = computed((): string => {
  if (props.model === null) {
    return 'black'
  }
  const q = firstVar.value.Q
  if (q === 'Good') {
    return 'green'
  }
  if (q === 'Uncertain') {
    return 'orange'
  }
  return 'red'
})

watch(
  () => props.selectionId,
  (newSel: string, oldSel: string) => {
    if (!open.value && containsChild(props.model, newSel)) {
      open.value = true
    }
  },
)

const selectObject = () => {
  emit('selectObject', props.model)
}

const onSubObjectSelected = (selectObject: TreeNode) => {
  emit('selectObject', selectObject)
}

const containsChild = (tree: TreeNode, id: string): boolean => {
  for (const child of tree.Children) {
    if (child.ID === id) {
      return true
    }
    const res = containsChild(child, id)
    if (res === true) {
      return true
    }
  }
  return false
}

const onDragStart = (event: DragEvent) => {
  const data = {
    ID: props.model.ID,
    Type: props.model.Type,
  }
  event.dataTransfer?.setData('text/plain', JSON.stringify(data))
}

const onDragOver = (event: DragEvent) => {
  const data = JSON.parse(event.dataTransfer?.getData('text/plain') || '{}')
  if (getMatchingMembers(data.Type).length === 1) {
    event.preventDefault()
  }
}

const onDrop = (event: DragEvent) => {
  event.preventDefault()
  const data = JSON.parse(event.dataTransfer?.getData('text/plain') || '{}')
  const memberWithType = getMatchingMembers(data.Type)
  emit('dragDrop', data.ID, props.model.ID, memberWithType[0].Array)
}

const onSubDragDrop = (fromID: string, toID: string, toArrayName: string) => {
  emit('dragDrop', fromID, toID, toArrayName)
}

const getMatchingMembers = (type: string): ObjMemInfo[] => {
  const members = props.typeInfo[props.model.Type].ObjectMembers
  return members.filter((m) => m.Type === type)
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
