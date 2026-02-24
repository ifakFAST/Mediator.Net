// Plugins
import Components from 'unplugin-vue-components/vite'
import Vue from '@vitejs/plugin-vue'
import Vuetify, { transformAssetUrls } from 'vite-plugin-vuetify'
import webfontDownload from 'vite-plugin-webfont-dl'
import { viteStaticCopy } from 'vite-plugin-static-copy'

// Utilities
import { defineConfig } from 'vite'
import { fileURLToPath, URL } from 'node:url'

// https://vitejs.dev/config/
export default defineConfig({
  base: '/App/',
  build: {
    outDir: '../../Run/DashboardDist/App',
    emptyOutDir: true,
    sourcemap: false,
    chunkSizeWarningLimit: 2000,
  },
  plugins: [
    Vue({
      template: { transformAssetUrls },
    }),
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
    proxy: {
      '/login': 'http://localhost:8082',
      '/logout': 'http://localhost:8082',
      '/viewRequest': 'http://localhost:8082',
      '/activateView': 'http://localhost:8082',
      '/duplicateView': 'http://localhost:8082',
      '/duplicateConvertView': 'http://localhost:8082',
      '/renameView': 'http://localhost:8082',
      '/moveView': 'http://localhost:8082',
      '/deleteView': 'http://localhost:8082',
      '/toggleHeader': 'http://localhost:8082',
      '/ctx': 'http://localhost:8082',
      '/view_tagmetadata/moduletype': 'http://localhost:8082',
      '/block_images': 'http://localhost:8082',
      '/websocket': {
        target: 'ws://localhost:8082',
        ws: true,
      },
    },
  },
})
