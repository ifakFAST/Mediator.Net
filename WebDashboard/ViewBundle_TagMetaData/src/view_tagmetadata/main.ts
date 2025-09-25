import { registerPlugins } from '@/plugins'
import ViewTagMetaData from './ViewTagMetaData.vue'
import { createApp } from 'vue'
import { setupDashboardEnv } from '../debug'

if (import.meta.env.DEV) {
  setupDashboardEnv('tagmetadata')
}

const app = createApp(ViewTagMetaData)

registerPlugins(app)

app.mount('#app')
