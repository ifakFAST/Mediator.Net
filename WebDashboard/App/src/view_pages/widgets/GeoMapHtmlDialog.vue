<template>
  <v-dialog
    v-model="dialog"
    :max-width="dialogOptions.maxWidth"
    :width="dialogOptions.width"
    :persistent="dialogOptions.persistent"
    @keydown.esc="close"
  >
    <v-card>
      <v-toolbar
        v-if="dialogOptions.title"
        color="primary"
        dark
        density="compact"
        flat
      >
        <div class="my-toolbar-title">
          {{ dialogOptions.title }}
        </div>

        <v-spacer />
        <v-btn
          icon="mdi-close"
          @click="close"
        />
      </v-toolbar>
      <v-card-text class="pa-4">
        <div
          class="html-content"
          v-html="dialogOptions.content"
        ></div>
      </v-card-text>
      <v-card-actions
        v-if="!dialogOptions.hideActions"
        class="pt-0"
      >
        <v-spacer />
        <v-btn
          color="primary-darken-1"
          variant="text"
          @click="close"
        >
          Close
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'

interface HtmlDialogOptions {
  title?: string
  content: string
  width?: number | string
  maxWidth?: number | string
  persistent?: boolean
  hideActions?: boolean
}

const dialog = ref(false)
const resolve = ref<(() => void) | null>(null)
const dialogOptions = ref<HtmlDialogOptions>({
  title: '',
  content: '',
  width: 600,
  maxWidth: '90vw',
  persistent: false,
  hideActions: false,
})

const open = (htmlDialogData: string | HtmlDialogOptions): Promise<void> => {
  // Handle both string and object formats
  if (typeof htmlDialogData === 'string') {
    dialogOptions.value = {
      title: '',
      content: htmlDialogData,
      width: 600,
      maxWidth: '90vw',
      persistent: false,
      hideActions: false,
    }
  } else {
    dialogOptions.value = {
      title: htmlDialogData.title || '',
      content: htmlDialogData.content,
      width: htmlDialogData.width || 750,
      maxWidth: htmlDialogData.maxWidth || '90vw',
      persistent: htmlDialogData.persistent || false,
      hideActions: htmlDialogData.hideActions || true,
    }
  }

  dialog.value = true

  return new Promise<void>((resolvePromise) => {
    resolve.value = resolvePromise
  })
}

const close = (): void => {
  if (resolve.value) {
    resolve.value()
  }
  dialog.value = false
}

defineExpose({ open })
</script>

<style scoped>
.html-content {
  /* Ensure proper styling for HTML content */
  line-height: 1.5;
}

.html-content :deep(img) {
  max-width: 100%;
  height: auto;
}

.html-content :deep(table) {
  width: 100%;
  border-collapse: collapse;
}

.html-content :deep(table th),
.html-content :deep(table td) {
  border: 1px solid rgb(var(--v-theme-outline));
  padding: 8px;
  text-align: left;
}

.html-content :deep(table th) {
  background-color: rgb(var(--v-theme-surface-variant));
  font-weight: bold;
}

.html-content :deep(h1),
.html-content :deep(h2),
.html-content :deep(h3),
.html-content :deep(h4),
.html-content :deep(h5),
.html-content :deep(h6) {
  margin-top: 0;
  margin-bottom: 16px;
}

.html-content :deep(p) {
  margin-bottom: 12px;
}

.html-content :deep(ul),
.html-content :deep(ol) {
  margin-bottom: 12px;
  padding-left: 20px;
}

.my-toolbar-title {
  font-size: 1.25rem;
  font-weight: 400;
  letter-spacing: 0;
  line-height: 1.75rem;
  text-transform: none;
  margin-left: 15px;
  margin-right: 15px;
}
</style>
