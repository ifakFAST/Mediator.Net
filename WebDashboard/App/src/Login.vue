<template>
  <v-app>
    <v-main>
      <v-container>
        <v-row
          justify="center"
          align="center"
        >
          <v-col
            cols="12"
            sm="6"
            md="6"
            lg="5"
            xl="4"
          >
            <v-card
              class="mt-4"
              elevation="5"
            >
              <v-card-title class="text-h5">
                {{ title }}
              </v-card-title>
              <v-card-text>
                <v-text-field
                  label="User Name"
                  v-model="loginUser"
                  class="mb-4 mt-2"
                  autofocus
                ></v-text-field>
                <v-text-field
                  label="Password"
                  v-model="loginPass"
                  type="password"
                  @keyup.enter="login"
                ></v-text-field>
              </v-card-text>
              <v-card-actions>
                <v-btn
                  variant="tonal"
                  class="mx-2 mb-2 mt-4 px-4"
                  @click="login"
                  >LogIn</v-btn
                >
              </v-card-actions>
            </v-card>
            <v-alert
              v-model="hasAlert"
              color="error"
              icon="mdi-alert"
              closable
              class="mt-2"
            >
              {{ alertText }}
            </v-alert>
          </v-col>
        </v-row>
      </v-container>
    </v-main>
  </v-app>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import axios from 'axios'
import globalState from './global'

interface LoginSuccessEvent {
  session: string
  model: any
  user: string
  pass: string
  viewID: string
  canUpdateViews: boolean
  initialTimeRange: any
  initialStepSizeMS: number
}

const emit = defineEmits<{
  (e: 'loginSuccess', event: LoginSuccessEvent): void
}>()

const title = ref('Dashboard Login')
const alertText = ref('')
const hasAlert = ref(false)
const loginUser = ref('')
const loginPass = ref('')
const viewID = ref('')

onMounted(() => {
  title.value = (window as any).TheDashboardLogin || 'Dashboard Login'

  // Auto-login in dev mode or when ?view= parameter is present
  const shouldAutoLogin = import.meta.env.DEV || (globalState.loggedOut === false && window.location.search.startsWith('?view='))

  if (shouldAutoLogin) {
    loginUser.value = 'ifak'
    loginPass.value = 'fast'
    login()
    if (window.location.search.startsWith('?view=')) {
      viewID.value = window.location.search.substring(6)
    }
  }
})

function login() {
  resetAlarm()
  axios
    .post('/login', {
      user: loginUser.value,
      pass: loginPass.value,
    })
    .then((response) => {
      emit('loginSuccess', {
        session: response.data.sessionID,
        model: response.data.model,
        user: loginUser.value,
        pass: loginPass.value,
        viewID: viewID.value,
        canUpdateViews: response.data.canUpdateViews,
        initialTimeRange: response.data.initialTimeRange,
        initialStepSizeMS: response.data.initialStepSizeMS,
      })
      loginPass.value = ''
    })
    .catch((error) => {
      loginPass.value = ''
      if (error.response && error.response.data && error.response.data.error) {
        setAlarm(error.response.data.error)
      } else {
        setAlarm('Connect error.')
      }
    })
}

function resetAlarm() {
  hasAlert.value = false
  alertText.value = ''
}

function setAlarm(text: string) {
  alertText.value = text
  hasAlert.value = true
}
</script>
