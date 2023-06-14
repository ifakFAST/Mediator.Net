<template>
  <v-app light>

    <v-navigation-drawer app fixed clipped :mini-variant="miniVariant" v-model="drawer" width="220" hide-overlay disable-resize-watcher>

      <v-list nav dense class="mt-4">
        <v-list-item-group :value="currViewID" >
          <v-list-item v-for="(view, i) in views" :value="view.viewID" :key="view.viewID"
            @click="activateView(view.viewID)"
            @contextmenu="(e) => onContextMenuViewEntry(e, view, i)">
            <v-list-item-icon>
              <v-icon :color="iconColorFromView(view)" light>{{iconFromView(view)}}</v-icon>
            </v-list-item-icon>
            <v-list-item-content>
              <v-list-item-title style="font-size: 15px" v-text="view.viewName"></v-list-item-title>
            </v-list-item-content>
          </v-list-item>
        </v-list-item-group>
      </v-list>

      <v-menu v-if="canUpdateViews" v-model="contextMenuViewEntry.show" :position-x="contextMenuViewEntry.clientX" :position-y="contextMenuViewEntry.clientY" absolute offset-y>
        <v-list>
          <v-list-item @click="onContextViewRename">
            <v-list-item-title>Rename</v-list-item-title>
          </v-list-item>
          <v-list-item @click="onContextViewDuplicate">
            <v-list-item-title>Duplicate</v-list-item-title>
          </v-list-item>
          <v-list-item @click="onContextViewDuplicateConvert" v-if="contextMenuViewEntry.view.viewType === 'HistoryPlots'">
            <v-list-item-title>Duplicate Convert</v-list-item-title>
          </v-list-item>
          <v-list-item v-if="contextMenuViewEntry.canMoveUp" @click="onContextViewMoveUp">
            <v-list-item-title>Move Up</v-list-item-title>
          </v-list-item>
          <v-list-item v-if="contextMenuViewEntry.canMoveDown" @click="onContextViewMoveDown">
            <v-list-item-title>Move Down</v-list-item-title>
          </v-list-item>
           <v-list-item @click="onContextViewDelete">
            <v-list-item-title>Delete</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>

      <v-dialog v-model="renameDlg.show" max-width="350" @keydown.esc="renameDlgCancel">
        <v-card>
          <v-card-title>
            <span class="headline">Rename View</span>
          </v-card-title>
          <v-card-text>
            Enter the new name for the view.
            <v-text-field v-model="renameDlg.text" ref="editText"></v-text-field>
          </v-card-text>
          <v-card-actions class="pt-0">
            <v-spacer></v-spacer>
            <v-btn color="grey darken-1" text @click.native="renameDlgCancel">Cancel</v-btn>
            <v-btn color="primary darken-1" :disabled="renameDlg.text.length === 0" text @click.native="renameDlgOK">OK</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <v-dialog v-model="confirmDlg.show" :max-width="250" @keydown.esc="() => { confirmDlg.show = false }">
        <v-card>
          <v-toolbar dark color="red" dense flat>
            <v-toolbar-title class="white--text">{{ confirmDlg.title }}</v-toolbar-title>
          </v-toolbar>
          <v-card-text class="mt-3">{{ confirmDlg.message }}</v-card-text>
          <v-card-actions class="pt-0">
            <v-spacer></v-spacer>
            <v-btn color="grey darken-1" text @click.native="() => { confirmDlg.show = false }">Cancel</v-btn>
            <v-btn color="primary darken-1" text @click.native="confirmDlg.onOK">OK</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

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

    <v-main>
      <v-container fluid fill-height>
        <iframe :src="currViewSrc" style="border: 0; width: 100%; height: 100%;"></iframe>
      </v-container>
    </v-main>

  </v-app>
</template>

<script>
  import globalState from "./Global.js";
  import Vue from 'vue'

  export default {
    props: {
      currViewID: String,
      currViewSrc: String,
      views: Array,
      canUpdateViews: Boolean,
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
        today: '',
        contextMenuViewEntry: {
          show: false,
          clientX: 0,
          clientY: 0,
          view: {},
          canMoveUp: false,
          canMoveDown: false,
        },
        renameDlg: {
          show: false,
          text: ''
        },
        confirmDlg: {
          show: false,
          title: '',
          message: '',
          onOK: () => {}
        }
      }
    },
    mounted() {
      this.title = TheDashboardHeader || 'Dashboard';
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
      },
      iconColorFromView(view) {
        const color = view.viewIconColor;
        if (color === undefined || color === '') return ''
        return color
      },
      onContextMenuViewEntry(e, view, viewIdx) {
        e.preventDefault()
        e.stopPropagation()
        this.contextMenuViewEntry.show = true
        this.contextMenuViewEntry.clientX = e.clientX
        this.contextMenuViewEntry.clientY = e.clientY
        this.contextMenuViewEntry.view = view
        this.contextMenuViewEntry.canMoveUp = viewIdx > 0
        this.contextMenuViewEntry.canMoveDown = viewIdx < this.views.length - 1
      },
      onContextViewRename() {
        this.renameDlg.show = true
        this.renameDlg.text = this.contextMenuViewEntry.view.viewName
      },
      renameDlgOK() {
        this.renameDlg.show = false
        this.$emit('renameView', this.contextMenuViewEntry.view.viewID, this.renameDlg.text);
      },
      renameDlgCancel() {
        this.renameDlg.show = false
      },
      onContextViewDuplicate() {
        this.$emit('duplicateView', this.contextMenuViewEntry.view.viewID)
      },
       onContextViewDuplicateConvert() {
        this.$emit('duplicateConvertView', this.contextMenuViewEntry.view.viewID)
      },
      onContextViewMoveUp() {
        this.$emit('moveUp', this.contextMenuViewEntry.view.viewID)
      },
      onContextViewMoveDown() {
        this.$emit('moveDown', this.contextMenuViewEntry.view.viewID)
      },
      onContextViewDelete() {
        this.confirmDlg.show = true
        this.confirmDlg.title = 'Delete view?'
        this.confirmDlg.message = `Do you really want to delete view '${this.contextMenuViewEntry.view.viewName}'?`
        this.confirmDlg.onOK = this.onViewDeleteOK
      },
      onViewDeleteOK() {
        this.confirmDlg.show = false
        this.$emit('delete', this.contextMenuViewEntry.view.viewID)
      }
    },
    watch: {
       showCustomTimeRangeSelector(v) {
         const d = new Date();
         this.today = d.toISOString().substr(0, 10);
       },
       miniVariant(v) {
         Vue.nextTick(() => {
           globalState.resizeListener();
         });
       },
       drawer(v) {
         Vue.nextTick(() => {
           globalState.resizeListener();
         });
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