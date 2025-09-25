/**
 * Reusable helper functions and utilities for creating dynamic module types
 * This file provides common functionality that can be imported by individual module type files
 */

// Parameter definition helpers
export function paramNumeric(id, name, defaultValue, minValue, maxValue) {
  return {
    id,
    name,
    type: 'Numeric',
    defaultValue: JSON.stringify(defaultValue),
    minValue,
    maxValue,
  }
}

export function paramString(id, name, defaultValue) {
  return {
    id,
    name,
    type: 'String',
    defaultValue: defaultValue !== undefined ? defaultValue : '',
  }
}

export function paramBool(id, name, defaultValue) {
  return {
    id,
    name,
    type: 'Bool',
    defaultValue: JSON.stringify(defaultValue),
  }
}

export function paramEnum(id, name, defaultValue, enumValues) {
  return {
    id,
    name,
    type: 'Enum',
    defaultValue,
    enumValues,
  }
}

// IO definition helpers
export function makeInput(id, orientation, type, relPos = -1000) {
  return { id, input: true, type, relPos, orientation }
}

export function makeOutput(id, orientation, type, relPos = -1000) {
  return { id, input: false, type, relPos, orientation }
}

// Drawing helper functions
export function drawCenterText(ctx, text, options = {}) {
  const dc = ctx.dc
  const b = ctx.block
  dc.fillStyle = options.color !== undefined ? options.color : b.colorForeground !== undefined ? b.colorForeground : 'black'
  dc.textAlign = 'center'
  dc.textBaseline = 'middle'
  dc.font = options.font !== undefined ? options.font : ctx.block.fontStr
  const offY = options.offsetY !== undefined ? options.offsetY : 0
  const offX = options.offsetX !== undefined ? options.offsetX : 0
  dc.fillText(text, b.x + 0.5 * b.w + offX * b.w, b.y + 0.5 * b.h + offY * b.h)
}

export function drawCenterTextAbove(ctx, text, options = {}) {
  const dc = ctx.dc
  const b = ctx.block
  dc.fillStyle = options.color !== undefined ? options.color : b.colorForeground !== undefined ? b.colorForeground : 'black'
  dc.textAlign = 'center'
  dc.textBaseline = 'bottom'
  dc.font = options.font !== undefined ? options.font : ctx.block.fontStr
  const offY = options.offsetY !== undefined ? options.offsetY : 0
  const offX = options.offsetX !== undefined ? options.offsetX : 0
  dc.fillText(text, b.x + 0.5 * b.w + offX * b.w, b.y - 3 + offY * b.h)
}

// Interactive element helpers
export function drawInteractiveElement(ctx, color = 'blue') {
  const tags = ctx.block.tags || []
  if (tags.length > 0) {
    const block = ctx.block
    const dc = ctx.dc

    dc.fillStyle = color
    const r = 4
    const x = block.x + 0.5 * block.w
    const y = block.y + 0.5 * block.h
    dc.beginPath()
    dc.ellipse(x, y, r, r, 0, 0, 2 * Math.PI, false)
    dc.fill()

    ctx.ia.addInteractiveElement({ 
      block: block.name, 
      x: x - r, 
      y: y - r, 
      w: 2 * r, 
      h: 2 * r, 
      type: 'tag', 
      id: tags[0].id 
    })
  }
}

// Common drawing patterns
export function drawSimpleBorder(ctx, color = 'black', lineWidth = 2) {
  const dc = ctx.dc
  const b = ctx.block
  dc.strokeStyle = color
  dc.lineWidth = lineWidth
  dc.strokeRect(b.x, b.y, b.w, b.h)
}

export function drawConnectionLines(ctx, centerX, centerY, lineWidth = 3) {
  const dc = ctx.dc
  const b = ctx.block
  dc.lineWidth = lineWidth
  dc.beginPath()
  for (const p of b.ports) {
    dc.moveTo(p.x, p.y)
    dc.lineTo(centerX, centerY)
  }
  dc.stroke()
}