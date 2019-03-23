import 'typeface-roboto/index.css'
import 'material-design-icons-iconfont/dist/material-design-icons.css'
import Vue from 'vue'
import '../plugins/vuetify'
import ViewGeneric from './ViewGeneric.vue'

Vue.config.productionTip = false

import { setupDashboardEnv } from '../debug'

if (process.env.NODE_ENV === 'development') {
  setupDashboardEnv('io')
}

export const app = new Vue({
  el: '#app',
  render(h) {
    return h(ViewGeneric)
  },
})
