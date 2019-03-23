<template>
    <div>
        <template v-if="loginRequired">
            <login @loginSuccess="loginSuccess"></login>
        </template>

        <template v-else>
            <dashboard :views="model.views" :busy="busy" :currViewSrc="currentViewSource"
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
      if (window.WebSocket) {
        const context = this;
        const socket = new WebSocket("ws://" + window.location.host + "/websocket/");
        socket.onopen = function(openEvent) {
          socket.send(context.sessionID);
          const doKeepAlive = function() {
             socket.send("KA");
          };
          context.intervalVar = setInterval(doKeepAlive, 5000);
        };
        socket.onmessage = function(wsEvent) {
          const parsedData = JSON.parse(wsEvent.data);
          context.eventListener(parsedData.event, parsedData.payload);
        };
        context.eventSocket = socket;
      }
    },
    logout() {
      clearInterval(this.intervalVar);
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