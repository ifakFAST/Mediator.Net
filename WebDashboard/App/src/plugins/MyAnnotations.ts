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
  text?: string
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

    // Add the annotations one-by-one.
    const area = e.dygraph.getArea()

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

      const a: AnnotationConfig = p.annotation

      const div = document.createElement('div')
      div.style.fontSize = g.getOption('axisLabelFontSize') + 'px'
      div.className = 'dygraph-annotation'      
      div.appendChild(document.createTextNode(p.annotation.shortText!))
      div.style.visibility = 'hidden'
      containerDiv.appendChild(div)
      const divWidth = div.offsetWidth
      const divHeight = div.offsetHeight

      const left = p.canvasx - divWidth / 2
      const divTop = p.canvasy - divHeight - 3
      div.style.left = left + 'px'      
      div.style.top = divTop + 'px'
      div.style.visibility = 'visible'
      div.title = p.annotation.text || ''
      //div.style.color = g.colorsMap_[p.name]
      a.div = div
      this.annotations_.push(div)
    }
  }

  destroy(): void {
    this.detachLabels()
  }
}

export default MyAnnotations
