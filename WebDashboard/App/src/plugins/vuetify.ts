/**
 * plugins/vuetify.ts
 *
 * Framework documentation: https://vuetifyjs.com`
 */

// Styles
import '@mdi/font/css/materialdesignicons.css'
import 'vuetify/styles'

// Composables
import { createVuetify } from 'vuetify'

// https://vuetifyjs.com/en/introduction/why-vuetify/#feature-guides
export default createVuetify({
  components: {},
  theme: {
    defaultTheme: 'light',
  },
  defaults: {
    VMenu: { transition: false },
    VDialog: { transition: false },
    VOverlay: { transition: false },
    VTextField: { density: 'compact', hideDetails: true },
    VSelect: { density: 'compact', hideDetails: true, transition: false },
    VAutocomplete: { density: 'compact', hideDetails: true, transition: false },
    VCheckbox: { density: 'compact', hideDetails: true, transition: false },
    VCombobox: { density: 'compact', hideDetails: true, transition: false },
    VTextarea: { density: 'compact', hideDetails: true },
    VChip: { density: 'compact' },
    VSwitch: { density: 'compact', transition: false },
    VList: { density: 'compact', transition: false },
  },
})
