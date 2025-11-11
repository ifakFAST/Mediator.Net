/**
 * MyAnnotations Plugin for Dygraphs
 *
 * This is a TypeScript port of the built-in Dygraphs Annotations plugin.
 * Initially works exactly the same as the original - can be customized later.
 *
 * Based on: dygraphs/src/plugins/annotations.js
 * License: MIT
 */

interface AnnotationPoint {
  canvasx: number
  canvasy: number
  name: string
  annotation: AnnotationConfig
}

interface AnnotationConfig {
  text: string
  shortText?: string
  icon?: string
  width?: number
  height?: number
  tickHeight?: number
  tickColor?: string
  tickWidth?: number
  cssClass?: string
  attachAtBottom?: boolean
  div?: HTMLDivElement
  clickHandler?: (a: AnnotationConfig, pt: AnnotationPoint, g: any, e: Event) => void
  mouseOverHandler?: (a: AnnotationConfig, pt: AnnotationPoint, g: any, e: Event) => void
  mouseOutHandler?: (a: AnnotationConfig, pt: AnnotationPoint, g: any, e: Event) => void
  dblClickHandler?: (a: AnnotationConfig, pt: AnnotationPoint, g: any, e: Event) => void
}

interface DygraphEvent {
  dygraph: any
  canvas: HTMLCanvasElement
  drawingContext: CanvasRenderingContext2D
}

class MyAnnotations {
  private annotations_: HTMLDivElement[] = []

  toString(): string {
    return "MyAnnotations Plugin"
  }

  activate(g: any): { clearChart: Function; didDrawChart: Function } {
    return {
      clearChart: this.clearChart.bind(this),
      didDrawChart: this.didDrawChart.bind(this)
    }
  }

  detachLabels(): void {
    for (let i = 0; i < this.annotations_.length; i++) {
      const a = this.annotations_[i]
      if (a && a.parentNode) {
        a.parentNode.removeChild(a)
      }
      this.annotations_[i] = null as any
    }
    this.annotations_ = []
  }

  clearChart(e: DygraphEvent): void {
    this.detachLabels()
  }

  didDrawChart(e: DygraphEvent): void {
    const g = e.dygraph

    // Early out in the (common) case of zero annotations.
    const points: AnnotationPoint[] = g.layout_.annotated_points
    if (!points || points.length === 0) return

    const containerDiv = e.canvas.parentNode as HTMLElement

    const bindEvt = (
      eventName: keyof Pick<AnnotationConfig, 'clickHandler' | 'mouseOverHandler' | 'mouseOutHandler' | 'dblClickHandler'>,
      classEventName: string,
      pt: AnnotationPoint
    ) => {
      return (annotation_event: Event) => {
        const a = pt.annotation
        if (a.hasOwnProperty(eventName) && a[eventName]) {
          a[eventName]!(a, pt, g, annotation_event)
        } else if (g.getOption(classEventName)) {
          g.getOption(classEventName)(a, pt, g, annotation_event)
        }
      }
    }

    // Add the annotations one-by-one.
    const area = e.dygraph.getArea()

    // x-coord to sum of previous annotation's heights (used for stacking).
    const xToUsedHeight: { [key: number]: number } = {}

    for (let i = 0; i < points.length; i++) {
      const p = points[i]
      if (
        p.canvasx < area.x ||
        p.canvasx > area.x + area.w ||
        p.canvasy < area.y ||
        p.canvasy > area.y + area.h
      ) {
        continue
      }

      const a = p.annotation
      let tick_height = 6
      if (a.hasOwnProperty('tickHeight')) {
        tick_height = a.tickHeight!
      }

      // TODO: deprecate axisLabelFontSize in favor of CSS
      const div = document.createElement('div')
      div.style.fontSize = g.getOption('axisLabelFontSize') + 'px'
      let className = 'dygraph-annotation'
      if (!a.hasOwnProperty('icon')) {
        // camelCase class names are deprecated.
        className += ' dygraphDefaultAnnotation dygraph-default-annotation'
      }
      if (a.hasOwnProperty('cssClass')) {
        className += ' ' + a.cssClass
      }
      div.className = className

      const width = a.hasOwnProperty('width') ? a.width! : 16
      const height = a.hasOwnProperty('height') ? a.height! : 16
      if (a.hasOwnProperty('icon')) {
        const img = document.createElement('img')
        img.src = a.icon!
        img.width = width
        img.height = height
        div.appendChild(img)
      } else if (p.annotation.hasOwnProperty('shortText')) {
        div.appendChild(document.createTextNode(p.annotation.shortText!))
      }
      const left = p.canvasx - width / 2
      div.style.left = left + 'px'
      let divTop = 0
      if (a.attachAtBottom) {
        let y = area.y + area.h - height - tick_height
        if (xToUsedHeight[left]) {
          y -= xToUsedHeight[left]
        } else {
          xToUsedHeight[left] = 0
        }
        xToUsedHeight[left] += tick_height + height
        divTop = y
      } else {
        divTop = p.canvasy - height - tick_height
      }
      div.style.top = divTop + 'px'
      div.style.width = width + 'px'
      div.style.height = height + 'px'
      div.title = p.annotation.text
      div.style.color = g.colorsMap_[p.name]
      div.style.borderColor = g.colorsMap_[p.name]
      a.div = div

      g.addAndTrackEvent(div, 'click', bindEvt('clickHandler', 'annotationClickHandler', p))
      g.addAndTrackEvent(div, 'mouseover', bindEvt('mouseOverHandler', 'annotationMouseOverHandler', p))
      g.addAndTrackEvent(div, 'mouseout', bindEvt('mouseOutHandler', 'annotationMouseOutHandler', p))
      g.addAndTrackEvent(div, 'dblclick', bindEvt('dblClickHandler', 'annotationDblClickHandler', p))

      containerDiv.appendChild(div)
      this.annotations_.push(div)

      const ctx = e.drawingContext
      ctx.save()
      ctx.strokeStyle = a.hasOwnProperty('tickColor') ? a.tickColor! : g.colorsMap_[p.name]
      ctx.lineWidth = a.hasOwnProperty('tickWidth') ? a.tickWidth! : g.getOption('strokeWidth')
      ctx.beginPath()
      if (!a.attachAtBottom) {
        ctx.moveTo(p.canvasx, p.canvasy)
        ctx.lineTo(p.canvasx, p.canvasy - 2 - tick_height)
      } else {
        const y = divTop + height
        ctx.moveTo(p.canvasx, y)
        ctx.lineTo(p.canvasx, y + tick_height)
      }
      ctx.closePath()
      ctx.stroke()
      ctx.restore()
    }
  }

  destroy(): void {
    this.detachLabels()
  }
}

export default MyAnnotations
