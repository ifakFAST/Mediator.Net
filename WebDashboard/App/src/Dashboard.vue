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
            <v-btn text color="primary" class="pl-1 pr-1" @click="timeRangePrepare">{{timeRangeString}}</v-btn>
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

      <v-btn text icon v-show="showRangeStepButtonLeft" @mousedown="onRangeStep(-1)" class="mr-n2">
        <v-icon>chevron_left</v-icon>
      </v-btn>
      
      <v-btn text icon v-show="showRangeStepButtonRight" @mousedown="onRangeStep(+1)" class="ml-n2">
        <v-icon>chevron_right</v-icon>
      </v-btn>

      <v-menu v-if="showTime" offset-y :close-on-content-click="false" v-model="showStepSizeMenu">
        <template v-slot:activator="{ on: menu }">
          <v-tooltip bottom :open-delay="1000">
            <template v-slot:activator="{ on: tooltip }">
              <v-btn text icon v-bind="attrs" v-on="{ ...tooltip, ...menu }">
                <v-icon>mdi-tune</v-icon>
              </v-btn>
            </template>
            <span>Step size...</span>
          </v-tooltip>
        </template>
        <v-list>
          <v-list-item @click="autoStepSizeSelected" :class="stepSize === 0 ? 'primary--text' : ''">
            <v-list-item-title>Auto</v-list-item-title>
          </v-list-item>
          <v-list-item v-for="item in predefinedStepSizes" :key="item.title" @click="predefinedStepSizeSelected(item)" :class="stepSize === millisecondsFromCountAndUnit(item.count, item.unit) ? 'primary--text' : ''">
            <v-list-item-title>{{ item.title }}</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>

      <v-dialog v-model="showCustomTimeRangeSelector" max-width="670px" @keydown="editKeydown">
         <v-card>
            <v-card-text>
               <table style="border-collapse: separate; border-spacing: 16px;">
                  <tr>
                     <td><v-text-field label="From" v-model="customRangeStartDate"></v-text-field></td>
                     <td><v-text-field label=""     v-model="customRangeStartTime"></v-text-field></td>
                     <td><v-text-field label="To"   v-model="customRangeEndDate"></v-text-field></td>
                     <td><v-text-field label=""     v-model="customRangeEndTime"></v-text-field></td>
                  </tr>
                  <tr>
                     <td colspan="2"><v-date-picker no-title v-model="customRangeStartDate" class="elevation-4"></v-date-picker></td>
                     <td colspan="2"><v-date-picker no-title v-model="customRangeEndDate"   class="elevation-4"></v-date-picker></td>
                  </tr>
               </table>
            </v-card-text>
            <v-card-actions>
               <v-spacer></v-spacer>
               <v-btn color="primary" :disabled="!isValidTimeRange" text @click="timeRangeApply2">Apply</v-btn>
            </v-card-actions>
         </v-card>
      </v-dialog>

      <v-tooltip bottom :open-delay="1000">
        <template v-slot:activator="{ on, attrs }">
          <v-chip class="ma-1 ml-4" outlined @click.stop="logout" v-bind="attrs" v-on="on">        
            <v-icon left>mdi-account-outline</v-icon>
            {{ user }}
          </v-chip>
        </template>
        <span>Logout...</span>
      </v-tooltip>

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

  const OneSecond = 1000;
  const OneMinute = 60 * OneSecond;
  const OneHour = 60 * OneMinute;
  const OneDay = 24 * OneHour;

  function time2Minutes(time) {
    // e.g. '01:30' -> 90
    const bits = time.split(':');
    return parseInt(bits[0]) * 60 + parseInt(bits[1]);
  };

  function isMidnight(dateString) {
    try {
      const time = getTimePartOfISOString(dateString, '');
      return time === '00:00' || time === '00:00:00';
    } catch (e) {
      return false;
    }
  };

  function getDatePartOfISOString(s, defaultDate) {
      if (s.length < 10) {
        return defaultDate;
      }
      return s.substring(0, 10);
  };
      
  function getTimePartOfISOString(s, defaultTime) {
    if (s.length < 16) {
      return defaultTime;
    }
    return s.substring(11);
  };

  function date2LocalStr(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0'); // Months are zero-based
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0'); // Local time hours
    const minutes = String(date.getMinutes()).padStart(2, '0'); // Local time minutes
    const seconds = String(date.getSeconds()).padStart(2, '0'); // Local time seconds
    if (seconds !== '00') {
      return `${year}-${month}-${day}T${hours}:${minutes}:${seconds}`;          
    }
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  };

  function millisecondsFromCountAndUnit(count, unit) {
    switch (unit) {
      case 'Minutes': return count * OneMinute;
      case 'Hours': return count * OneHour;
      case 'Days': return count * OneDay;
      case 'Weeks': return count * 7 * OneDay;
      case 'Months': return count * 30 * OneDay;
      case 'Years': return count * 365 * OneDay;
    }
    return 0;
  };

  export default {
    props: {
      currViewID: String,
      currViewSrc: String,
      user: String,
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
        showStepSizeMenu: false,
        customRangeStartDate: '',
        customRangeStartTime: '00:00',
        customRangeEndDate: '',
        customRangeEndTime: '00:00',
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
        predefinedStepSizes: [
          { title: '5 minutes',  count:  5, unit: 'Minutes' },
          { title: '15 minutes', count: 15, unit: 'Minutes' },
          { title: '1 hour',     count:  1, unit: 'Hours'   },
          { title: '1 day',      count:  1, unit: 'Days'    },
          { title: '7 days',     count:  7, unit: 'Days'    },
        ],
        showCustomTimeRangeSelector: false,
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
         this.timeRangeEdit.rangeStart = this.customRangeStartDate + 'T' + this.customRangeStartTime;
         this.timeRangeEdit.rangeEnd   = this.customRangeEndDate + 'T' + this.customRangeEndTime;
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
         const now = new Date();
         const tomorrow = new Date(now.getTime() + OneDay);
         const todayAsStringWithoutTime    = getDatePartOfISOString(date2LocalStr(now), '');
         const tomorrowAsStringWithoutTime = getDatePartOfISOString(date2LocalStr(tomorrow), '');
         this.customRangeStartDate = getDatePartOfISOString(this.timeRangeEdit.rangeStart, todayAsStringWithoutTime);
         this.customRangeStartTime = getTimePartOfISOString(this.timeRangeEdit.rangeStart, '00:00');
         this.customRangeEndDate = getDatePartOfISOString(this.timeRangeEdit.rangeEnd, tomorrowAsStringWithoutTime);
         this.customRangeEndTime = getTimePartOfISOString(this.timeRangeEdit.rangeEnd, '00:00');
      },
      predefinedStepSizeSelected(stepSize) {
         this.showStepSizeMenu = false;
         globalState.diffStepSizeMS = millisecondsFromCountAndUnit(stepSize.count, stepSize.unit);
      },
      autoStepSizeSelected() {
         this.showStepSizeMenu = false;
         globalState.diffStepSizeMS = 0;
      },
      millisecondsFromCountAndUnit(count, unit) {
         return millisecondsFromCountAndUnit(count, unit);
      },
      editKeydown(e) {
         if (e.keyCode === 27) {
            this.showCustomTimeRangeSelector = false;
         }
      },
      onRangeStep(count) {

        this.timeRangePrepare();

        if (this.timeRangeEdit.type === 'Last') {

          const dateNow = new Date();
          dateNow.setMilliseconds(0);
          dateNow.setSeconds(0);

          let addMilliseconds;
          switch (this.timeRangeEdit.lastUnit) {
            case 'Minutes': 
              addMilliseconds = OneMinute; 
              break;
            default:
              dateNow.setUTCMinutes(0);
              addMilliseconds = OneHour; 
              break;
          }

          const dateEnd = new Date(dateNow.getTime() + addMilliseconds);
          const dateStart = new Date(dateEnd.getTime() - millisecondsFromCountAndUnit(this.timeRangeEdit.lastCount, this.timeRangeEdit.lastUnit));
          this.timeRangeEdit.type = 'Range';
          this.timeRangeEdit.rangeStart = date2LocalStr(dateStart);
          this.timeRangeEdit.rangeEnd = date2LocalStr(dateEnd);
        }
        
        const dateStart = new Date(this.timeRangeEdit.rangeStart); // will interpret as local time
        const dateEnd   = new Date(this.timeRangeEdit.rangeEnd);   // will interpret as local time
        const diff = globalState.diffStepSizeMS === 0 ? (dateEnd - dateStart) : globalState.diffStepSizeMS; // milliseconds

        const bothMidnightBeforeShift = isMidnight(this.timeRangeEdit.rangeStart) && isMidnight(this.timeRangeEdit.rangeEnd);

        if (diff !== diff) { // check for NaN
          return;
        }
        
        const newDateStart = new Date(dateStart.getTime() + count * diff);
        const newDateEnd   = new Date(dateEnd.getTime()   + count * diff);

        this.timeRangeEdit.rangeStart = date2LocalStr(newDateStart);
        this.timeRangeEdit.rangeEnd   = date2LocalStr(newDateEnd);
       
        if (bothMidnightBeforeShift && !isMidnight(this.timeRangeEdit.rangeStart)) {
          // correct for daylight saving time shift, so that the range is still a full day (either add or substract 60 minutes usually):
          const minutes = time2Minutes(getTimePartOfISOString(this.timeRangeEdit.rangeStart, '00:00'))
          const offsetMilliseconds = (minutes < 12 * 60 ? -minutes : (24*60)-minutes) * OneMinute;
          this.timeRangeEdit.rangeStart = date2LocalStr(new Date(newDateStart.getTime() + offsetMilliseconds));
        }

        if (bothMidnightBeforeShift && !isMidnight(this.timeRangeEdit.rangeEnd)) {
          // correct for daylight saving time shift, so that the range is still a full day (either add or substract 60 minutes usually):
          const minutes = time2Minutes(getTimePartOfISOString(this.timeRangeEdit.rangeEnd, '00:00'))
          const offsetMilliseconds = (minutes < 12 * 60 ? -minutes : (24*60)-minutes) * OneMinute;
          this.timeRangeEdit.rangeEnd = date2LocalStr(new Date(newDateEnd.getTime() + offsetMilliseconds));
        }

        const range = Object.assign({}, this.timeRangeEdit);
        this.$emit("timechange", range);
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
            const dateSeparator = ' \u2013 ';

            const dateStartStr = getDatePartOfISOString(s, '');
            const dateEndStr = getDatePartOfISOString(e, '');

            if (isMidnight(s) && isMidnight(e)) {
              // substract one day from e:
              const dateEnd = new Date(e);
              const dateEndMinus1Day = new Date(dateEnd.getTime() - OneDay);
              const dateEndMinusOneDayStr = getDatePartOfISOString(date2LocalStr(dateEndMinus1Day));

              if (dateStartStr === dateEndMinusOneDayStr) {
                return dateStartStr;
              }

              return dateStartStr + dateSeparator + dateEndMinusOneDayStr;
            }

            const replaceTWithSpace = (str) => {
              return str.replace('T', ' ');
            }

            if (dateStartStr === dateEndStr) {
              const timeEndStr = getTimePartOfISOString(e, '');
              if (timeEndStr !== '') {
                return replaceTWithSpace(s) + dateSeparator + timeEndStr;
              }
            }

            return replaceTWithSpace(s) + dateSeparator + replaceTWithSpace(e);
         }
         return "";
      },
      stepSize() {
        return globalState.diffStepSizeMS;
      },
      showRangeStepButtonLeft() {
        return this.showTime;
      },
      showRangeStepButtonRight() {
        return this.showTime;
      },
      isValidTimeRange() {
        const s = this.customRangeStartDate + 'T' + this.customRangeStartTime;
        const e = this.customRangeEndDate + 'T' + this.customRangeEndTime;
        try {
          const sDate = new Date(s);
          const eDate = new Date(e);
          return sDate < eDate;
        } catch (e) {
          return false;
        }
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