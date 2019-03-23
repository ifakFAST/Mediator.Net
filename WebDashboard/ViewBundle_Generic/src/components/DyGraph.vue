
<script lang="ts">

import { Component, Prop, Vue, Watch } from 'vue-property-decorator'
import Dygraph from '../assets/dygraph.js'

let dgUUID = 0

@Component
export default class DyGraph extends Vue {

  @Prop() graphData!: any
  @Prop() graphOptions!: any
  @Prop({ default() { return { width: '100%', height: '600px' }}} ) graphStyle!: any
  @Prop() graphResetZoom!: any

  uid = dgUUID.toString()
  graph = null
  needFullUpdate = false

  render(h) {
    return h('div', {
      attrs: {
          id: 'vue-dygraphs-' + this.uid,
      },
      style: this.graphStyle,
    })
  }

  beforeCreate() {
    dgUUID += 1
  }

  mounted() {
    const id = 'vue-dygraphs-' + this.uid
    this.graph = new Dygraph(id, this.graphData, this.deepCopy(this.graphOptions))
  }

  updateGraph() {
    if (this.graph !== null) {
      const obj = Object.assign({}, this.deepCopy(this.graphOptions), {file: this.graphData})
      this.graph.updateOptions(obj)
    }
  }

  @Watch('graphData')
  watch_graphData(val, oldVal) {

    if (val.length > 0 && oldVal.length > 0 && val[0].length !== oldVal[0].length) {

      if (this.columnMatch(val, this.graphOptions)) {
        this.updateGraph()
      }
      else {
        this.needFullUpdate = true
      }
      return
    }

    if (this.graph !== null) {

      const zoomed = this.graph.isZoomed('y')

      const axesValueRange = {
        y: {
          valueRange: null,
        },
        y2: {
          valueRange: null,
        },
      }

      if (zoomed) {
        const ranges = this.graph.yAxisRanges()
        axesValueRange.y.valueRange  = ranges[0]
        axesValueRange.y2.valueRange = ranges[1]
      }

      this.graph.updateOptions({
        file: val,
        axes: axesValueRange,
      })
    }
  }

  @Watch('graphOptions')
  watch_graphOptions(val, oldVal) {

    if (this.columnMatch(this.graphData, val)) {
      if (this.needFullUpdate) {
        this.needFullUpdate = false
        this.updateGraph()
      }
      else {
        this.graph.updateOptions(this.deepCopy(val))
      }
    }
  }

  columnMatch(data: any[][], options: any): boolean {
    if (data.length === 0) { return true }
    const columns = data[0].length
    return columns === options.labels.length
  }

  @Watch('graphResetZoom')
  watch_graphResetZoom(val, oldVal) {
    if (this.graph !== null) {
      this.graph.resetZoom()
    }
  }

  deepCopy(obj) {
    const str = JSON.stringify(obj)
    const copy = JSON.parse(str)
    copy.legendFormatter = obj.legendFormatter
    return copy
  }

}

</script>

<style src="./dygraph.css"></style>

<style>

  .dygraph-legend {
    background-color: transparent !important;
    left: 80px !important;
  }

  .dygraph-legend-line {
    border-bottom-width: 12px !important;
  }

</style>
