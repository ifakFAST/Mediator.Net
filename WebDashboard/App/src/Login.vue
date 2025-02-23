<template>
    <v-app light>
      <v-main>

        <v-container>
          <v-row justify="center" align="center">
            <v-col cols="12" sm="6" md="6" lg="5" xl="4">

              <v-card raised class="mt-4" elevation="5">
                <v-card-title class="headline">
                 {{ title }}
                </v-card-title>
                <v-card-text>
                  <v-text-field label="User Name" v-model="loginUser" autofocus></v-text-field>
                  <v-text-field label="Password" v-model="loginPass" type="password" v-on:keyup.enter="login"></v-text-field>
                </v-card-text>
                <v-card-actions>
                  <v-btn class="mx-2 mb-2 px-4" @click="login">LogIn</v-btn>
                </v-card-actions>
              </v-card>
              <v-alert color="error" icon="warning" dismissible v-model="hasAlert">
                {{alertText}}
              </v-alert>

            </v-col>
          </v-row>
        </v-container>

      </v-main>
    </v-app>
</template>

<script>
import axios from "axios";
import globalState from "./Global.js";

export default {
  data() {
    return {
      title: "Dashboard Login",
      alertText: '',
      hasAlert: false,
      loginUser: '',
      loginPass: '',
      viewID: ''
    };
  },
  mounted() {
    this.title = TheDashboardLogin || "Dashboard Login";
    if (globalState.loggedOut === false && window.location.search.startsWith("?view=")) {
      this.loginUser = "ifak";
      this.loginPass = "fast";
      this.login();
      this.viewID = window.location.search.substring(6)
    }
  },
  methods: {
    login() {
      const context = this;
      this.resetAlarm();
      axios
        .post("/login", {
          user: this.loginUser,
          pass: this.loginPass
        })
        .then(function(response) {
          context.$emit("loginSuccess", {
            session: response.data.sessionID,
            model: response.data.model,
            user: context.loginUser,
            pass: context.loginPass,
            viewID: context.viewID,
            canUpdateViews: response.data.canUpdateViews,
            initialTimeRange: response.data.initialTimeRange
          });
          context.loginPass = "";
        })
        .catch(function(error) {
          context.loginPass = "";
          if (
            error.response &&
            error.response.data &&
            error.response.data.error
          ) {
            context.setAlarm(error.response.data.error);
          } else {
            context.setAlarm("Connect error.");
          }
        });
    },
    resetAlarm() {
      this.hasAlert = false;
      this.alertText = '';
    },
    setAlarm(text) {
      this.alertText = text;
      this.hasAlert = true;
    }
  }
};
</script>

