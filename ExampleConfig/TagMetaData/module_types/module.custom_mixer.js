/**
 * Example custom mixer module type
 * Demonstrates how to create a dynamic module type with custom drawing and parameters
 */

import { 
  paramNumeric, 
  paramString, 
  paramBool,
  makeInput, 
  makeOutput, 
  drawCenterText,
  drawConnectionLines,
  drawInteractiveElement
} from './util.js'

// Custom mixer module type definition
const CustomMixerBlock = {
  id: 'custom_mixer',
  
  parameters: [
    paramNumeric('capacity', 'Mixing Capacity (L)', 1000, 1, 50000),
    paramNumeric('rpm', 'Rotation Speed (RPM)', 120, 10, 1000),
    paramString('material', 'Material Type', 'Stainless Steel'),
    paramBool('heating', 'Has Heating Element', false),
    paramNumeric('inputs', 'Number of Inputs', 3, 2, 8)
  ],
  
  defineIOs(parameters) {
    const numInputs = parseInt(parameters['inputs'] || '3')
    const ios = []
    
    // Add multiple inputs on the left side
    for (let i = 0; i < numInputs; i++) {
      ios.push(makeInput(`in_${i}`, 'left', 'Water', i / (numInputs - 1)))
    }
    
    // Add main output
    ios.push(makeOutput('out_mixed', 'right', 'Water', 0.5))
    
    // Add heating control if enabled
    const hasHeating = JSON.parse(parameters['heating'] || 'false')
    if (hasHeating) {
      ios.push(makeInput('temp_control', 'top', 'Signal', 0.5))
      ios.push(makeOutput('temp_sensor', 'bottom', 'Signal', 0.5))
    }
    
    return ios
  },
  
  customDraw(ctx) {
    const block = ctx.block
    const dc = ctx.dc
    const centerX = block.x + 0.5 * block.w
    const centerY = block.y + 0.5 * block.h
    
    // Draw connection lines from all ports to center
    drawConnectionLines(ctx, centerX, centerY, 2)
    
    // Draw mixing symbol (circular arrows)
    dc.strokeStyle = 'black'
    dc.lineWidth = 2
    const radius = Math.min(block.w, block.h) * 0.15
    
    // Draw circular arrow indicating mixing
    dc.beginPath()
    dc.arc(centerX, centerY, radius, 0, 1.5 * Math.PI)
    dc.stroke()
    
    // Add arrowhead
    const arrowX = centerX
    const arrowY = centerY - radius
    dc.beginPath()
    dc.moveTo(arrowX, arrowY)
    dc.lineTo(arrowX - 4, arrowY - 4)
    dc.lineTo(arrowX + 4, arrowY - 4)
    dc.closePath()
    dc.fill()
    
    // Display capacity and RPM
    const capacity = block.parameters['capacity'] || '1000'
    const rpm = block.parameters['rpm'] || '120'
    drawCenterText(ctx, `${capacity}L`, { offsetY: -0.3, font: '10px Arial' })
    drawCenterText(ctx, `${rpm} RPM`, { offsetY: 0.3, font: '10px Arial' })
    
    // Draw heating indicator if enabled
    const hasHeating = JSON.parse(block.parameters['heating'] || 'false')
    if (hasHeating) {
      dc.fillStyle = 'orange'
      dc.beginPath()
      dc.arc(centerX + radius * 0.7, centerY - radius * 0.7, 3, 0, 2 * Math.PI)
      dc.fill()
    }
    
    // Add interactive element for tags
    drawInteractiveElement(ctx, 'green')
  },
  
  // Support dropping data tags
  supportedDropTypes: ['data-tag']
}

// Export as default for ES module compatibility
export default CustomMixerBlock