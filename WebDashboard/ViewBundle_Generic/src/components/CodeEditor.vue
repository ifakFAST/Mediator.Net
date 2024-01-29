<template>
  <div>
    <div ref="rootEditor" :style="getStyle()"></div>
    <v-toolbar dense flat>
      <v-btn small text @click="decreaseFontSize">
        <v-icon>mdi-minus</v-icon>
      </v-btn>
      <v-btn small text @click="increaseFontSize">
        <v-icon>mdi-plus</v-icon>
      </v-btn>
      <v-btn small text @click="editor.execCommand('find')">
        <v-icon>mdi-magnify</v-icon>
      </v-btn>
      <v-btn small text @click="editor.execCommand('replace')">
        <v-icon>mdi-find-replace</v-icon>
      </v-btn>
      <v-btn small text @click="editor.execCommand('undo')" :disabled="undoDisabled">
        <v-icon>mdi-undo</v-icon>
      </v-btn>
      <v-btn small text @click="editor.execCommand('redo')" :disabled="redoDisabled">
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"><path fill="currentColor" d="M18.4 10.6C16.55 9 14.15 8 11.5 8c-4.65 0-8.58 3.03-9.96 7.22L3.9 16a8.002 8.002 0 0 1 7.6-5.5c1.95 0 3.73.72 5.12 1.88L13 16h9V7z"/></svg>
      </v-btn>
      <v-select class="ml-3" style="max-width: 130px;" hide-details v-model="theme" :items="themes"></v-select>
      <v-btn small text @click="showKeyBindings">
        <v-icon>mdi-help-circle-outline</v-icon>
      </v-btn>
    </v-toolbar>
  </div>
</template>


<script lang="ts">
import Vue from 'vue'
import ace from 'ace-builds/src-noconflict/ace';
import 'ace-builds/src-noconflict/mode-csharp';
import 'ace-builds/src-noconflict/mode-python';
import 'ace-builds/src-noconflict/theme-textmate';
import 'ace-builds/src-noconflict/theme-chrome';
import 'ace-builds/src-noconflict/theme-github';
import 'ace-builds/src-noconflict/theme-twilight';
import 'ace-builds/src-noconflict/theme-github_dark';
import 'ace-builds/src-noconflict/theme-monokai';
import 'ace-builds/src-noconflict/theme-terminal';
import 'ace-builds/src-noconflict/theme-xcode';
import 'ace-builds/src-noconflict/ext-language_tools';
import 'ace-builds/src-noconflict/ext-searchbox';
import 'ace-builds/src-noconflict/ext-keybinding_menu';
import { CSSProperties } from 'vue/types/jsx';

// Based on vue2-ace-editor

export default Vue.extend({

  props:{
    value: String,
    lang: String,
    height: String,
    width: String,
  },

  data: () => ({
    editor: null,
    themes: ['textmate', 'xcode', 'chrome', 'github', 'github_dark', 'monokai', 'twilight', 'terminal'],
    theme: 'textmate',
    contentBackup: "",
    undoDisabled: true,
    redoDisabled: true,
    codeEditorProperties: {
      fontSize: 14,
      theme: 'textmate',
    }
  }),

  methods: {

    px: function (n: string): string {
      if ( /^\d*$/.test(n) ) {
        return n+"px";
      }
      return n;
    },

    getStyle(): CSSProperties {
      return {
        height: this.height ? this.px(this.height) : '100%',
        width: this.width ? this.px(this.width) : '100%',
        marginTop: '8px',
      }
    },

    increaseFontSize(): void {
      const newSize = this.codeEditorProperties.fontSize + 1;
      this.editor.setFontSize(newSize);
      this.codeEditorProperties.fontSize = newSize;
      this.saveProperties();
    },

    decreaseFontSize(): void {
      const newSize = this.codeEditorProperties.fontSize - 1;
      this.editor.setFontSize(newSize);
      this.codeEditorProperties.fontSize = newSize;
      this.saveProperties();
    },

    showKeyBindings(): void {
      const editor = this.editor;
      ace.config.loadModule("ace/ext/keybinding_menu", function(module) {
        module.init(editor);
        editor.showKeyboardShortcuts()
      })
    },

    saveProperties(): void {
      localStorage.setItem('codeEditorProperties', JSON.stringify(this.codeEditorProperties));
    },

  },

  watch:{
    value: function (val: string) {
      if (this.contentBackup !== val) {
        this.editor.session.setValue(val, 1);
        this.contentBackup = val;
      }
    },
    theme: function (newTheme: string) {
      this.codeEditorProperties.theme = newTheme;
      this.editor.setTheme('ace/theme/' + newTheme);
      this.saveProperties();
    },
    lang: function (newLang: string) {
      this.editor.getSession().setMode(typeof newLang === 'string' ? ( 'ace/mode/' + newLang ) : newLang);
    },
    height: function() {
      this.$nextTick(function() {
        this.editor.resize()
      })
    },
    width: function() {
      this.$nextTick(function() {
        this.editor.resize()
      })
    }
  },

  beforeDestroy: function() {
    this.editor.destroy();
    this.editor.container.remove();
  },

  mounted: function () {
    const vm = this;
    const lang = this.lang || 'text';

    const rootEditor = this.$refs.rootEditor as HTMLElement;
    const editor = vm.editor = ace.edit(rootEditor, {
      value: this.value,
    });
    editor.setOptions({
        enableBasicAutocompletion: true,
    });
    editor.$blockScrolling = Infinity;

    const codeEditorProperties = localStorage.getItem('codeEditorProperties');
    if (codeEditorProperties) {
      this.codeEditorProperties = JSON.parse(codeEditorProperties);
      this.theme = this.codeEditorProperties.theme;
    }

    editor.setFontSize(this.codeEditorProperties.fontSize);    
    editor.getSession().setMode(typeof lang === 'string' ? ( 'ace/mode/' + lang ) : lang);
    editor.setTheme('ace/theme/' + this.codeEditorProperties.theme);

    this.contentBackup = this.value;

    editor.on('change', function() {
      var content = editor.getValue();
      vm.$emit('input', content);
      vm.contentBackup = content;
      vm.$nextTick(function() {
        vm.undoDisabled = !editor.session.getUndoManager().hasUndo();
        vm.redoDisabled = !editor.session.getUndoManager().hasRedo();
      })
    });

  }

})
</script>

<style scoped>
/* Your styles here */
</style>