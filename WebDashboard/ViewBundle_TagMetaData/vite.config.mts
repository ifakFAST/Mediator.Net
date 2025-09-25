// Plugins
import Components from 'unplugin-vue-components/vite'
import Vue from '@vitejs/plugin-vue'
import Vuetify, { transformAssetUrls } from 'vite-plugin-vuetify'
import webfontDownload from 'vite-plugin-webfont-dl'
import { viteStaticCopy } from 'vite-plugin-static-copy'

// Utilities
import { defineConfig } from 'vite'
import { fileURLToPath, URL } from 'node:url'
import { resolve } from 'path'

// https://vitejs.dev/config/
export default defineConfig({
  base: '/ViewBundle_TagMetaData/',
  build: {
    outDir: '../../Run/DashboardDist/ViewBundle_TagMetaData',
    emptyOutDir: true,
    rollupOptions: {
      input: {
        tagmeta: resolve(__dirname, 'tagmetadata.html'),
      }
    },
    chunkSizeWarningLimit: 2000, // Replace webpack performance hints
  },
  plugins: [
    Vue({
      template: { transformAssetUrls },
    }),
    // https://github.com/vuetifyjs/vuetify-loader/tree/master/packages/vite-plugin#readme
    Vuetify(),
    Components(),   
    webfontDownload([
      'https://fonts.googleapis.com/css2?family=Roboto:wght@100;300;400;500;700;900&display=swap',
    ]),
    viteStaticCopy({
      targets: [
        {
          src: 'src/assets',
          dest: ''
        }
      ]
    }),
  ],
  optimizeDeps: {
    exclude: ['vuetify'],
  },
  define: { 'process.env': {} },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
    extensions: [
      '.js',
      '.json',
      '.jsx',
      '.mjs',
      '.ts',
      '.tsx',
      '.vue',
    ],
  },
  server: {
    port: 8080,
  },
})
