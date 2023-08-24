<template>
    <div>
        <template v-if="loginRequired">
            <login @loginSuccess="loginSuccess"></login>
        </template>

        <template v-else>
            <dashboard :user="user" :views="model.views" :busy="busy" :connectionState="connectionState" :currViewID="currentViewID" :currViewSrc="currentViewSource"
                       :timeRangeSelected="timeRange" :showTime="showTimeRangeSelector" :canUpdateViews="canUpdateViews"
                        @logout="logout" @activateView="activateView" @timechange="timeSelectChanged"
                        @duplicateView="duplicateView" @renameView="renameView"
                        @duplicateConvertView="duplicateConvertView"
                        @moveUp="moveUpView" @moveDown="moveDownView" @delete="deleteView"></dashboard>
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
      const view = this.model.views.find(v => v.viewID === this.currentViewID);
      return view.viewURL + "?viewID=" + this.currentViewID;
    }
  },
  methods: {
    loginSuccess(event) {
      console.info('Login success')
      this.sessionID = event.session;
      this.user = event.user;
      this.model = event.model;
      this.canUpdateViews = event.canUpdateViews;
      this.openWebSocket(event.user, event.pass)
      const viewIdx = this.model.views.findIndex((v) => v.viewID === event.viewID)
      if (viewIdx >= 0) {
        this.activateView(event.viewID)
      }
      else if (this.model.views.length > 0) {
        this.activateView(this.model.views[0].viewID)
      }
    },
    logout() {
      if (this.intervalVar !== 0) {
        clearInterval(this.intervalVar);
        this.intervalVar = 0;
      }
      axios.post("/logout", this.sessionID).catch(function (error) { } );
      this.sessionID = "";
      this.user = "";
      this.model = {};
      this.currentViewID = "";
      this.loggedOut = true;
      this.eventListener = function(eventName, eventPayload) {};
      try {
         this.eventSocket.close();
      }
      catch(error) {
         console.log("Error closing websocket: " + error);
      }
    },
    openWebSocket(user, pass) {
      if (window.WebSocket) {
        const context = this;
        const isHTTPS = window.location.protocol === "https:";
        const websocketProtocol = isHTTPS ? "wss://" : "ws://";
        const socket = new WebSocket(websocketProtocol + window.location.host + "/websocket/");
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
          const eventStartTime = new Date().getTime();
          context.connectionState = 0;
          const parsedData = JSON.parse(wsEvent.data);
          if (parsedData.event === "NaviAugmentation") {
            context.handleViewAugmentation(parsedData.payload);
          }
          else {
            context.eventListener(parsedData.event, parsedData.payload);
          }
          const doACK = function() {
            context.eventACKTime = new Date().getTime()
            socket.send("OK");
          };

          context.eventACKCounter += 1
          if (context.eventACKCounter < context.eventBurstCount) {
            doACK();
          }
          else {
            context.eventACKCounter = 0
            const diff = eventStartTime - context.eventACKTime;
            const minDist = 500;
            const waitTime = minDist - diff;
            if (waitTime <= 5) {
              // console.info("ACK. Diff: " + diff);
              doACK();
            }
            else {
              // console.info("Delayed ACK! Diff: " + diff + " -> WaitTime: " + waitTime);
              setTimeout(doACK, waitTime);
            }
          }
        };

        socket.onclose = function(ev) {
          context.connectionState = (ev.wasClean ? 2 : 1);
          if (context.intervalVar !== 0) {
            clearInterval(context.intervalVar);
            context.intervalVar = 0;
          }
          if (!ev.wasClean) {
            const reconnect = function() {
              context.openWebSocket(user, pass);
            };
            setTimeout(reconnect, 3000);
          }
          if (ev.wasClean && context.sessionID !== "") {
            console.info('Will try to relogin in 3 seconds...')
            const relogin = function() {
              context.tryReLogin(user, pass);
            };
            setTimeout(relogin, 3000);
          }
        }
      }
    },
    handleViewAugmentation(eventPayload) {
      const viewID = eventPayload.viewID;
      const view = this.model.views.find(v => v.viewID === viewID);
      if (view) {
        this.$set(view, 'viewIconColor', eventPayload.iconColor);
      }
    },
    tryReLogin(user, pass) {
      if (this.sessionID == '') {
        console.info('Abort relogin because of logout.')
        return
      }
      console.info('Trying to relogin...')
      const context = this;
      axios
        .post("/login", { user, pass })
        .then(function(response) {
          console.info('Relogin success.')
          const viewID = context.currentViewID;
          context.currentViewID = '';
          context.sessionID = response.data.sessionID
          context.model = response.data.model
          context.openWebSocket(user, pass)
          context.doActivateView(viewID)
        })
        .catch(function(error) {
          console.info('Relogin failed! Will try again in 2 seconds...')
          const relogin = function() {
            context.tryReLogin(user, pass);
          };
          setTimeout(relogin, 2000);
        });
    },
    activateView(viewID) {
      if (this.currentViewID === viewID) return;
      this.doActivateView(viewID);
    },
    doActivateView(viewID) {
      const context = this;
      const previousEventListener = context.eventListener;
      context.eventListener = function(eventName, eventPayload) {};
      axios
        .post("/activateView?" + this.sessionID + "_" + viewID)
        .then(function(response) {
          context.eventBurstCount = 1;
          context.currentViewID = viewID;
          context.canUpdateViewConfig = response.data.canUpdateViewConfig;
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
    },
    duplicateView(viewID) {
      const context = this;
      axios
        .post("/duplicateView?" + this.sessionID + "_" + viewID)
        .then(function(response) {
          console.info('duplicateView success.');
          const viewID = response.data.newViewID;
          context.model = response.data.model;
          context.doActivateView(viewID);
        })
        .catch(this.handleError);
    },
    duplicateConvertView(viewID) {
      const context = this;
      axios
        .post("/duplicateConvertView?" + this.sessionID + "_" + viewID)
        .then(function(response) {
          console.info('duplicateConvertView success.');
          const viewID = response.data.newViewID;
          context.model = response.data.model;
          context.doActivateView(viewID);
        })
        .catch(this.handleError);
    },
    renameView(viewID, newName) {
      const context = this;
      axios
        .post("/renameView?" + this.sessionID + "_" + viewID, { newViewName: newName })
        .then(function(response) {
          console.info('renameView success.');
          context.model = response.data.model;
        })
        .catch(this.handleError);
    },
    moveUpView(viewID) {
      const context = this;
      axios
        .post("/moveView?" + this.sessionID + "_" + viewID, { up: true })
        .then(function(response) {
          console.info('moveUpView success.');
          context.model = response.data.model;
        })
        .catch(this.handleError);
    },
    moveDownView(viewID) {
      const context = this;
      axios
        .post("/moveView?" + this.sessionID + "_" + viewID, { up: false })
        .then(function(response) {
          console.info('moveDownView success.');
          context.model = response.data.model;
        })
        .catch(this.handleError);
    },
    deleteView(viewID) {
      const context = this;
      axios
        .post("/deleteView?" + this.sessionID + "_" + viewID)
        .then(function(response) {
          console.info('deleteView success.');
          context.model = response.data.model;
          if (context.currentViewID === viewID) {
            context.currentViewID = '';
          }
        })
        .catch(this.handleError);
    }, 
    handleError(error) {
      if (error.response && error.response.data && error.response.data.error) {
        alert(error.response.data.error)
      }
      else {
        alert(error.message);
      }
    }
  }
};
</script>