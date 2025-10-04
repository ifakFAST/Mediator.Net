<template>
  <div>
    <div
      :class="[isSelected && 'bg-primary rounded', '']"
      style="padding: 2px"
      @click="selectNode()"
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
          small
          >{{ nodeIcon }}</v-icon
        >
        {{ node.title }}
      </span>
    </div>
    <TreeView
      v-for="child in children"
      :key="child.id"
      v-model:selected="selectedNode"
      class="ml-4"
      :expanded="false"
      :icon-function="iconFunction"
      :root="child"
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

const props = defineProps<{
  root: Node | null
  iconFunction: (node: Node, isExpanded: boolean) => string
  expanded: boolean
}>()

const selectedNode = defineModel<Node | null>('selected', { default: null })
const isExpanded = ref(props.expanded)

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

const nodeIcon = computed(() => {
  return props.iconFunction(node.value, isExpanded.value)
})

const expandIcon = computed(() => {
  return isExpanded.value ? 'mdi-menu-down' : 'mdi-menu-right'
})

const selectNode = (): void => {
  selectedNode.value = node.value
}
</script>

<style scoped>
.node-content {
  display: inline-flex;
  align-items: center;
}
</style>
