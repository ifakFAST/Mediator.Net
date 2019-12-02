<template>
    <div>
        <template v-if="loginRequired">
            <login @loginSuccess="loginSuccess"></login>
        </template>

        <template v-else>
            <dashboard :views="model.views" :busy="busy" :connectionState="connectionState" :currViewID="currentViewID" :currViewSrc="currentViewSource"
                       :timeRangeSelected="timeRange" :showTime="showTimeRangeSelector"
                        @logout="logout" @activateView="activateView" @timechange="timeSelectChanged"></dashboard>
        </template>
    </div>
</template>

<script>
import axios from "axios";
import Login from "./Login.vue";
import Dashboard from "./Dashboard.vue";
import globalState from "./Global.js";
import { setTimeout } from 'timers';

export default {
  data() {
    return globalState;
  },
  components: {
    login: Login,
    dashboard: Dashboard
  },
  computed: {
    loginRequired() {
      return this.sessionID === "";
    },
    currentViewSource() {
      if (this.currentViewID === "") return "";
      const view = this.model.views.find(v => v.viewID == this.currentViewID);
      return view.viewURL;
    }
  },
  methods: {
    loginSuccess(event) {
      this.sessionID = event.session;
      this.model = event.model;
      this.openWebSocket()
      if (this.model.views.length > 0) {
        this.activateView(this.model.views[0].viewID)
      }
    },
    logout() {
      if (this.intervalVar !== 0) {
        clearInterval(this.intervalVar);
        this.intervalVar = 0;
      }
      axios.post("/logout", this.sessionID);
      this.sessionID = "";
      this.model = {};
      this.currentViewID = "";
      this.eventListener = function(eventName, eventPayload) {};
      try {
         this.eventSocket.close();
      }
      catch(error) {
         console.log("Error closing websocket: " + error);
      }
    },
    openWebSocket() {
      if (window.WebSocket) {
        const context = this;
        const socket = new WebSocket("ws://" + window.location.host + "/websocket/");
        context.eventSocket = socket;

        socket.onopen = function(openEvent) {
          context.connectionState = 0;
          socket.send(context.sessionID);
          const doKeepAlive = function() {
            socket.send("KA");
          };
          context.intervalVar = setInterval(doKeepAlive, 5000);
        };

        socket.onmessage = function(wsEvent) {
          context.connectionState = 0;
          const parsedData = JSON.parse(wsEvent.data);
          context.eventListener(parsedData.event, parsedData.payload);
          const doACK = function() {
            socket.send("OK");
          };
          setTimeout(doACK, 500);
        };

        socket.onclose = function(ev) {
          context.connectionState = (ev.wasClean ? 2 : 1);
          if (context.intervalVar !== 0) {
            clearInterval(context.intervalVar);
            context.intervalVar = 0;
          }
          if (!ev.wasClean) {
            const reconnect = function() {
              context.openWebSocket();
            };
            setTimeout(reconnect, 3000);
          }
        }
      }
    },
    activateView(viewID) {
      if (this.currentViewID === viewID) return;
      const context = this;
      const previousEventListener = context.eventListener;
      context.eventListener = function(eventName, eventPayload) {};
      axios
        .post("/activateView?" + this.sessionID + "_" + viewID)
        .then(function(response) {
          context.currentViewID = viewID;
          context.showTimeRangeSelector = false;
        })
        .catch(function(error) {
          context.eventListener = previousEventListener;
          if (
            error.response &&
            error.response.data &&
            error.response.data.error
          ) {
            alert(error.response.data.error);
          } else {
            alert("Failed to activate view.");
          }
        });
    },
    timeSelectChanged(timeRange) {
       this.timeRange = Object.assign({}, timeRange);
       this.timeRangeListener(Object.assign({}, timeRange));
    }
  }
};
</script>