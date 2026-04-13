<template>
  <div>
    <div
      :class="['tree-node', isSelected && 'bg-primary rounded', isDropTarget && 'tree-node-drop-target']"
      :draggable="isDraggable"
      style="padding: 2px"
      @click="selectNode()"
      @dragend="onDragEnd"
      @dragenter="onDragEnter"
      @dragleave="onDragLeave"
      @dragover="onDragOver"
      @dragstart="onDragStart"
      @drop="onDrop"
    >
      <span
        v-if="node.children.length > 0"
        class="node-content"
        @click.stop="isExpanded = !isExpanded"
      >
        <v-icon small>{{ expandIcon }}</v-icon>
      </span>
      <span
        class="node-content"
        :style="{
          fontWeight: isSelected ? 'bold' : 'normal',
          marginLeft: node.children.length === 0 ? '24px' : '0',
        }"
      >
        <v-icon
          v-if="nodeIcon"
          class="mr-1"
          :color="nodeIcon.color"
          small
          >{{ nodeIcon.icon }}</v-icon
        >
        {{ nodeTitle }}
      </span>
    </div>
    <TreeView
      v-for="child in children"
      :key="child.id"
      v-model:selected="selectedNode"
      class="ml-4"
      :can-drop-function="canDropFunction"
      :draggable-function="draggableFunction"
      :expanded="false"
      :icon-function="iconFunction"
      :title-function="titleFunction"
      :root="child"
      @drag-drop="onSubDragDrop"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

export interface Node {
  id: string
  title: string
  children: Node[]
}

export interface NodeIcon {
  icon: string
  color?: string
}

const props = defineProps<{
  root: Node | null
  iconFunction: (node: Node, isExpanded: boolean) => string | NodeIcon
  titleFunction?: (node: Node) => string
  expanded: boolean
  draggableFunction?: (node: Node) => boolean
  canDropFunction?: (fromID: string, toID: string) => boolean
}>()

const emit = defineEmits<{
  (e: 'dragDrop', fromID: string, toID: string): void
}>()

const selectedNode = defineModel<Node | null>('selected', { default: null })
const isExpanded = ref(props.expanded)
const isDropTarget = ref(false)

const node = computed(() => {
  if (props.root) return props.root
  const res: Node = {
    id: '0',
    title: '',
    children: [],
  }
  return res
})

const children = computed(() => {
  return isExpanded.value && props.root ? props.root.children : []
})

const isSelected = computed(() => {
  return selectedNode.value?.id === node.value.id
})

const isDraggable = computed(() => {
  return props.draggableFunction ? props.draggableFunction(node.value) : false
})

const nodeIcon = computed<NodeIcon | null>(() => {
  const icon = props.iconFunction(node.value, isExpanded.value)
  if (typeof icon === 'string') {
    return icon ? { icon } : null
  }
  return icon.icon ? icon : null
})

const nodeTitle = computed(() => {
  return props.titleFunction ? props.titleFunction(node.value) : node.value.title
})

const expandIcon = computed(() => {
  return isExpanded.value ? 'mdi-menu-down' : 'mdi-menu-right'
})

const selectNode = (): void => {
  selectedNode.value = node.value
}

const getDraggedID = (event: DragEvent): string => {
  const dataTransfer = event.dataTransfer
  if (dataTransfer === null) {
    return ''
  }
  return dataTransfer.getData('application/x-tree-node-id') || dataTransfer.getData('text/plain')
}

const canDrop = (fromID: string): boolean => {
  return fromID !== '' && props.canDropFunction !== undefined && props.canDropFunction(fromID, node.value.id)
}

const onDragStart = (event: DragEvent): void => {
  if (!isDraggable.value) {
    event.preventDefault()
    return
  }
  const dataTransfer = event.dataTransfer
  if (dataTransfer === null) {
    return
  }
  dataTransfer.effectAllowed = 'move'
  dataTransfer.setData('application/x-tree-node-id', node.value.id)
  dataTransfer.setData('text/plain', node.value.id)
}

const onDragEnter = (event: DragEvent): void => {
  isDropTarget.value = canDrop(getDraggedID(event))
}

const onDragLeave = (): void => {
  isDropTarget.value = false
}

const onDragOver = (event: DragEvent): void => {
  const dropAllowed = canDrop(getDraggedID(event))
  isDropTarget.value = dropAllowed
  if (!dropAllowed) {
    return
  }
  event.preventDefault()
  if (event.dataTransfer !== null) {
    event.dataTransfer.dropEffect = 'move'
  }
}

const onDrop = (event: DragEvent): void => {
  isDropTarget.value = false
  const fromID = getDraggedID(event)
  if (!canDrop(fromID)) {
    return
  }
  event.preventDefault()
  emit('dragDrop', fromID, node.value.id)
}

const onDragEnd = (): void => {
  isDropTarget.value = false
}

const onSubDragDrop = (fromID: string, toID: string): void => {
  emit('dragDrop', fromID, toID)
}
</script>

<style scoped>
.tree-node {
  cursor: pointer;
}

.tree-node-drop-target {
  background-color: rgba(var(--v-theme-primary), 0.12);
  border-radius: 4px;
}

.node-content {
  display: inline-flex;
  align-items: center;
}
</style>
