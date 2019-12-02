<template>
  <v-app light>

    <v-navigation-drawer app fixed clipped :mini-variant="miniVariant" v-model="drawer" width="220" hide-overlay disable-resize-watcher>

      <v-list nav dense class="mt-4">
        <v-list-item-group :value="currViewID" >
          <v-list-item v-for="(view, i) in views" :value="view.viewID" :key="i" @click="activateView(view.viewID)">
            <v-list-item-icon>
              <v-icon light>{{iconFromView(view)}}</v-icon>
            </v-list-item-icon>
            <v-list-item-content>
              <v-list-item-title style="font-size: 15px" v-text="view.viewName"></v-list-item-title>
            </v-list-item-content>
          </v-list-item>
        </v-list-item-group>
      </v-list>

    </v-navigation-drawer>

    <v-app-bar app fixed clipped-left>
      <v-app-bar-nav-icon @click.stop="drawer = !drawer"></v-app-bar-nav-icon>
      <v-btn icon @click.stop="miniVariant = !miniVariant">
        <v-icon v-html="miniVariant ? 'chevron_right' : 'chevron_left'"></v-icon>
      </v-btn>
      <v-toolbar-title v-text="title"></v-toolbar-title>

      <v-progress-circular v-if="busy" class="mx-5" indeterminate color="primary"></v-progress-circular>

      <v-spacer></v-spacer>

      <v-alert class="mx-4 my-0" outlined :value="connectionState > 0" icon="cloud_off" :color="connectionColor" transition="scale-transition">
        {{ connectionText }}
      </v-alert>

      <v-menu v-if="showTime" offset-y :close-on-content-click="false" v-model="showTimeEdit">
         <template v-slot:activator="{ on }">
          <div v-on="on">
            Time Range:
            <v-btn text color="primary" style="margin-left: 0px; margin-right: 30px;" @click="timeRangePrepare">{{timeRangeString}}</v-btn>
          </div>
         </template>
         <v-list>
            <v-list-item v-for="item in predefinedTimeRanges" :key="item.title" @click="predefinedTimeRangeSelected(item)">
               <v-list-item-title>{{ item.title }}</v-list-item-title>
            </v-list-item>
            <v-list-item>
               <table>
                  <tr>
                     <td>
                        <v-text-field style="width: 60px; margin-right: 12px;" type="Number" v-model.number="timeRangeEdit.lastCount"></v-text-field>
                     </td>
                     <td>
                        <v-select style="width: 110px;" v-model="timeRangeEdit.lastUnit" :items="['Minutes', 'Hours', 'Days', 'Weeks', 'Months', 'Years']"></v-select>
                     </td>
                     <td>
                        <v-btn style="min-width: 40px; width: 40px; margin-right: 0px;" color="primary" :disabled="!isValidTimeLast" text @click="timeRangeApply"><v-icon>check</v-icon></v-btn>
                     </td>
                  </tr>
               </table>
            </v-list-item>
            <v-list-item @click="customTimeRangeSelected">
               <v-list-item-title>Custom range...</v-list-item-title>
            </v-list-item>
         </v-list>
      </v-menu>

      <v-dialog v-model="showCustomTimeRangeSelector" max-width="670px" @keydown="editKeydown">
         <v-card>
            <v-card-text>
               <table style="border-collapse: separate; border-spacing: 16px;">
                  <tr>
                     <td><v-text-field label="From" v-model="timeRangeEdit.rangeStart"></v-text-field></td>
                     <td><v-text-field label="To" v-model="timeRangeEdit.rangeEnd"></v-text-field></td>
                  </tr>
                  <tr>
                     <td><v-date-picker :max="today" no-title v-model="timeRangeEdit.rangeStart" class="elevation-4"></v-date-picker></td>
                     <td><v-date-picker :max="today" no-title v-model="timeRangeEdit.rangeEnd" class="elevation-4"></v-date-picker></td>
                  </tr>
               </table>
            </v-card-text>
            <v-card-actions>
               <v-spacer></v-spacer>
               <v-btn color="primary" :disabled="!isValidTimeRange" text @click="timeRangeApply2">Apply</v-btn>
            </v-card-actions>
         </v-card>
      </v-dialog>

      <v-btn text @click.stop="logout">Logout</v-btn>
    </v-app-bar>

    <v-content>
      <v-container fluid fill-height>
        <iframe :src="currViewSrc" style="border: 0; width: 100%; height: 100%;"></iframe>
      </v-container>
    </v-content>

  </v-app>
</template>

<script>
  export default {
    props: {
      currViewID: String,
      currViewSrc: String,
      views: Array,
      busy: Boolean,
      connectionState: Number,
      showTime: Boolean,
      timeRangeSelected: Object
    },
    data() {
      return {
         drawer: true,
         miniVariant: false,
         title: 'Dashboard',
         showTimeEdit: false,
         timeRangeEdit: {
            type: 'Last',
            lastCount: 7,
            lastUnit: 'Days',
            rangeStart: '',
            rangeEnd: ''
         },
         predefinedTimeRanges: [
            { title: 'Last 60 minutes', count: 60, unit: 'Minutes' },
            { title: 'Last 6 hours',    count:  6, unit: 'Hours'   },
            { title: 'Last 24 hours',   count: 24, unit: 'Hours'   },
            { title: 'Last 7 days',     count:  7, unit: 'Days'    },
            { title: 'Last 30 days',    count: 30, unit: 'Days'    },
            { title: 'Last 12 months',  count: 12, unit: 'Months'  },
         ],
         showCustomTimeRangeSelector: false,
         today: ''
      }
    },
    methods: {
      logout() {
        this.$emit('logout');
      },
      activateView(viewID) {
        this.$emit('activateView', viewID);
      },
      timeRangePrepare() {
         Object.assign(this.timeRangeEdit, this.timeRangeSelected);
      },
      timeRangeApply() {
         this.showTimeEdit = false;
         this.timeRangeEdit.type = 'Last';
         const range = Object.assign({}, this.timeRangeEdit);
         this.$emit("timechange", range);
      },
      timeRangeApply2() {
         this.showCustomTimeRangeSelector = false;
         this.timeRangeEdit.type = 'Range';
         const range = Object.assign({}, this.timeRangeEdit);
         this.$emit("timechange", range);
      },
      predefinedTimeRangeSelected(timeRange) {
         this.showTimeEdit = false;
         this.timeRangeEdit.type = 'Last';
         this.timeRangeEdit.lastCount = timeRange.count;
         this.timeRangeEdit.lastUnit = timeRange.unit;
         const range = Object.assign({}, this.timeRangeEdit);
         this.$emit("timechange", range);
      },
      customTimeRangeSelected() {
         this.showTimeEdit = false;
         this.showCustomTimeRangeSelector = true;
      },
      editKeydown(e) {
         if (e.keyCode === 27) {
            this.showCustomTimeRangeSelector = false;
         }
      },
      isValidDate(s) {
         const bits = s.split(new RegExp('\\-', 'g'));
         const year = bits[0];
         const month = bits[1];
         const day = bits[2];
         const d = new Date(year, month - 1, day);
         return d.getFullYear() == year && d.getMonth() + 1 == month && year > 1900 && year < 3000;
      },
      iconFromView(view) {
        const icon = view.viewIcon;
        if (icon === undefined || icon === '') return 'bubble_chart'
        return icon
      }
    },
    watch: {
       showCustomTimeRangeSelector(v) {
         const d = new Date();
         this.today = d.toISOString().substr(0, 10);
       }
    },
    computed: {
      timeRangeString() {
         if (this.timeRangeSelected.type === 'Last') {
            return "Last " + this.timeRangeSelected.lastCount + " " + this.timeRangeSelected.lastUnit;
         }
         if (this.timeRangeSelected.type === 'Range') {
            const s = this.timeRangeSelected.rangeStart;
            const e = this.timeRangeSelected.rangeEnd;
            return s + " - " + e;
         }
         return "";
      },
      isValidTimeRange() {
         const s = this.timeRangeEdit.rangeStart;
         const e = this.timeRangeEdit.rangeEnd;
         return s.length > 0 && e.length > 0 && e >= s && this.isValidDate(s) && this.isValidDate(e);
      },
      isValidTimeLast() {
         const num = this.timeRangeEdit.lastCount;
         return Number.isInteger(num) && num > 0;
      },
       connectionColor() {
        if (this.connectionState === 1) { return 'warning' }
        return 'error'
      },
      connectionText() {
        if (this.connectionState === 1) { return 'Trying to reconnect...' }
        return 'No Connection!'
      }
    }
  }
</script>