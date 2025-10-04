/**
 * plugins/index.ts
 *
 * Automatically included in `./src/main.ts`
 */

// Plugins
import vuetify from './vuetify'

// Types
import type { App } from 'vue'

export function registerPlugins(app: App) {
  app.use(vuetify)

  // Import global styles after plugins to ensure proper cascade order
  import('@/styles/global.css')
}
