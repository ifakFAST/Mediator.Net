<template>
  <div
    :style="containerStyle"
    class="vue-splitter"
    @mouseup="onUp"
    @mousemove="onMouseMove"
    @touchmove="onMove"
    @touchend="onUp"
  >
    <div
      :style="leftPaneStyle"
      class="left-pane splitter-pane"
    >
      <slot name="left-pane"></slot>
    </div>
    <div
      class="splitter"
      :class="{ active }"
      :style="splitterStyle"
      @mousedown="onDown"
      @click="onClick"
      @touchstart.prevent="onDown"
    ></div>
    <div
      :style="rightPaneStyle"
      class="right-pane splitter-pane"
    >
      <slot name="right-pane"></slot>
    </div>
  </div>
</template>

<style lang="css">
.vue-splitter {
  height: inherit;
  display: flex;
}
.vue-splitter .splitter-pane {
  height: inherit;
  overflow-y: auto;
}
.vue-splitter .splitter {
  background-color: rgba(var(--v-theme-on-surface), 0.24);
}
</style>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { CSSProperties } from 'vue'

interface Props {
  margin?: number
  horizontal?: boolean
  defaultPercent?: number
}

const props = withDefaults(defineProps<Props>(), {
  margin: 10,
  horizontal: false,
  defaultPercent: 50,
})

const emit = defineEmits<{
  resize: [percent: number]
}>()

const active = ref(false)
const percent = ref(50)
const hasMoved = ref(false)

const containerStyle = computed(
  (): CSSProperties => ({
    cursor: active.value ? (props.horizontal ? 'ns-resize' : 'ew-resize') : undefined,
    userSelect: active.value ? 'none' : undefined,
    flexDirection: props.horizontal ? 'column' : 'row',
  }),
)
const splitterStyle = computed(
  (): CSSProperties => (props.horizontal ? { height: '5px', cursor: 'ns-resize' } : { width: '5px', cursor: 'ew-resize' }),
)
const leftPaneStyle = computed((): CSSProperties => (props.horizontal ? { height: percent.value + '%' } : { width: percent.value + '%' }))
const rightPaneStyle = computed(
  (): CSSProperties => (props.horizontal ? { height: 100 - percent.value + '%' } : { width: 100 - percent.value + '%' }),
)

onMounted(() => {
  percent.value = props.defaultPercent
})

function onClick() {
  if (!hasMoved.value) {
    percent.value = props.defaultPercent
    emit('resize', percent.value)
  }
}

function onDown(e: Event) {
  active.value = true
  hasMoved.value = false
}

function onUp() {
  active.value = false
}

function onMove(e: MouseEvent | TouchEvent) {
  let offset = 0
  let target = e.currentTarget as HTMLElement
  let calculatedPercent = 0

  if (active.value) {
    if (props.horizontal) {
      while (target) {
        offset += target.offsetTop
        target = target.offsetParent as HTMLElement
      }
      const pageY = 'pageY' in e ? e.pageY : (e as TouchEvent).touches[0].pageY
      calculatedPercent = Math.floor(((pageY - offset) / (e.currentTarget as HTMLElement).offsetHeight) * 10000) / 100
    } else {
      while (target) {
        offset += target.offsetLeft
        target = target.offsetParent as HTMLElement
      }
      const pageX = 'pageX' in e ? e.pageX : (e as TouchEvent).touches[0].pageX
      calculatedPercent = Math.floor(((pageX - offset) / (e.currentTarget as HTMLElement).offsetWidth) * 10000) / 100
    }

    if (calculatedPercent > props.margin && calculatedPercent < 100 - props.margin) {
      percent.value = calculatedPercent
    }
    emit('resize', percent.value)
    hasMoved.value = true
  }
}

function onMouseMove(e: MouseEvent) {
  if (e.buttons === 0 || e.which === 0) {
    active.value = false
  }
  onMove(e)
}
</script>
