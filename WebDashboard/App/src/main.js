import 'typeface-roboto/index.css'
import 'material-design-icons-iconfont/dist/material-design-icons.css'
import '@mdi/font/css/materialdesignicons.css'
import Vue from 'vue'
import vuetify from './plugins/vuetify'
import App from './App'
import axios from "axios";
import globalState from "./Global.js";

Vue.config.productionTip = false

window.dashboardApp = new Vue({
  el: '#app',
  vuetify,
  render: h => h(App),
  methods: {
    sendViewRequest(request, payload, successHandler) {
      this.doSendViewRequest(request, payload, successHandler, 'text')
    },
    sendViewRequestBlob(request, payload, successHandler) {
      this.doSendViewRequest(request, payload, successHandler, 'blob')
    },
    doSendViewRequest(request, payload, successHandler, responseType) {
      const config = { // suppress auto conversion of string to JSON, otherwise strange behavior
         transformResponse: [function (data) { return data; }],
         responseType
      };
      globalState.busy = true;
      axios.post('/viewRequest/' + request + "?" + this.getDashboardViewContext(), payload, config)
        .then(function (response) {
            globalState.busy = false;
            successHandler(response.data);
        })
        .catch(function (error) {
            globalState.busy = false;
            if (error.response && error.response.data) {
               const data = JSON.parse(error.response.data);
               if (data.error) {
                  alert("Request failed: " + data.error);
               }
               else {
                  alert("Request failed: " + error.response.data);
               }
            }
            else {
               alert("Connection error: " + error);
            }
        });
    },
    sendViewRequestAsync(request, payload, responseType) {
      const rspType = responseType || 'text'
      const config = { // suppress auto conversion of string to JSON, otherwise strange behavior
          transformResponse: [function (data) { return data; }],
          responseType: rspType
      };
      globalState.busy = true;
      return axios.post('/viewRequest/' + request + "?" + this.getDashboardViewContext(), payload, config)
        .then(function (response) {
          globalState.busy = false;
          if (response.data && response.data !== '' && rspType === 'text') {
            return JSON.parse(response.data);
          }
          return response.data;
        })
        .catch(function (error) {
           globalState.busy = false;
           if (error.response && error.response.data) {

              if (error.response.data instanceof Blob) {
                return new Promise((resolve, reject) => {
                  const reader = new FileReader();
                  reader.onload = () => {
                    const data = JSON.parse(reader.result);
                    if (data.error) {
                      reject(new Error(data.error));
                    }
                    else {
                      reject(new Error(reader.result));
                    }
                  };
                  reader.readAsText(error.response.data);
                });
              }

              if ((typeof error.response.data) !== 'string') {
                throw new Error(error.response.statusText);
              }
              const data = JSON.parse(error.response.data);
              if (data.error) {
                 throw new Error(data.error);
              }
              else {
                throw new Error(error.response.data);
              }
           }
           else {
             throw error;
           }
       });
    },
    getDashboardViewContext() {
      return globalState.sessionID + '_' + globalState.currentViewID;
    },
    registerViewEventListener(listener) {
      globalState.eventListener = listener;
    },
    registerResizeListener(listener) {
      globalState.resizeListener = listener;
    },
    showTimeRangeSelector(show) {
       if (show) {
         globalState.showTimeRangeSelector = true;
       }
       else {
         globalState.showTimeRangeSelector = false;
       }
    },
    getCurrentTimeRange() {
       return Object.assign({}, globalState.timeRange);
    },
    registerTimeRangeListener(listener) {
      globalState.timeRangeListener = listener;
    },
    setEventBurstCount(count) {
      // console.info("setEventBurstCount: " + count);
      globalState.eventBurstCount = count;
    },
    canUpdateViewConfig() {
      return globalState.canUpdateViewConfig;
    }
  }
})