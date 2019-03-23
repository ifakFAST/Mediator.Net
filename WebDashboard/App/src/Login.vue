<template>
    <v-app light>
      <v-content>
        <v-layout row>
          <v-flex xs12 sm4 offset-sm4>
            <v-card raised class="mt-4">
              <v-card-title class="headline">
                Dashboard Login
              </v-card-title>
              <v-card-text>
                <v-text-field label="User Name" v-model="loginUser" autofocus></v-text-field>
                <v-text-field label="Password" v-model="loginPass" type="password" v-on:keyup.enter="login"></v-text-field>
              </v-card-text>
              <v-card-actions>
                <v-btn @click="login">LogIn</v-btn>
              </v-card-actions>
            </v-card>
            <v-alert color="error" icon="warning" dismissible v-model="hasAlert">
              {{alertText}}
            </v-alert>
          </v-flex>
        </v-layout>
      </v-content>
    </v-app>
</template>

<script>
import axios from "axios";

export default {
  data() {
    return {
      alertText: '',
      hasAlert: false,
      loginUser: '',
      loginPass: ''
    };
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
            model: response.data.model
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

