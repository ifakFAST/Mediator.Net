/**
 * Example smart valve module type
 * Demonstrates a valve with smart control features and custom visualization
 */

import { 
  paramNumeric, 
  paramString, 
  paramEnum,
  makeInput, 
  makeOutput, 
  drawCenterText,
  drawSimpleBorder,
  drawInteractiveElement
} from './util.js'

// Smart valve module type definition
const SmartValveBlock = {
  id: 'smart_valve',
  
  parameters: [
    paramEnum('valve_type', 'Valve Type', 'ball', ['ball', 'gate', 'butterfly', 'globe']),
    paramNumeric('size', 'Pipe Size (inches)', 4, 0.5, 48),
    paramNumeric('max_pressure', 'Max Pressure (bar)', 16, 1, 100),
    paramString('material', 'Body Material', 'Cast Iron'),
    paramEnum('actuation', 'Actuation Type', 'pneumatic', ['manual', 'pneumatic', 'electric', 'hydraulic']),
    paramNumeric('response_time', 'Response Time (s)', 5, 0.1, 60)
  ],
  
  defineIOs(parameters) {
    const ios = [
      // Main flow path
      makeInput('inlet', 'left', 'Water', 0.5),
      makeOutput('outlet', 'right', 'Water', 0.5),
      
      // Position feedback
      makeOutput('position', 'top', 'Signal', 0.3),
      
      // Status outputs
      makeOutput('status', 'top', 'Signal', 0.7)
    ]
    
    // Add control input based on actuation type
    const actuationType = parameters['actuation'] || 'pneumatic'
    if (actuationType !== 'manual') {
      ios.push(makeInput('control', 'bottom', 'Signal', 0.5))
    }
    
    return ios
  },
  
  customDraw(ctx) {
    const block = ctx.block
    const dc = ctx.dc
    const centerX = block.x + 0.5 * block.w
    const centerY = block.y + 0.5 * block.h
    
    // Draw valve body outline
    drawSimpleBorder(ctx, 'black', 2)
    
    // Draw valve symbol based on type
    const valveType = block.parameters['valve_type'] || 'ball'
    dc.strokeStyle = 'black'
    dc.lineWidth = 2
    
    switch (valveType) {
      case 'ball':
        // Draw ball valve symbol (circle with line through it)
        dc.beginPath()
        dc.arc(centerX, centerY, block.w * 0.15, 0, 2 * Math.PI)
        dc.stroke()
        dc.beginPath()
        dc.moveTo(centerX - block.w * 0.1, centerY - block.w * 0.1)
        dc.lineTo(centerX + block.w * 0.1, centerY + block.w * 0.1)
        dc.stroke()
        break
        
      case 'gate':
        // Draw gate valve symbol (rectangular gate)
        dc.strokeRect(centerX - block.w * 0.1, centerY - block.h * 0.2, 
                     block.w * 0.2, block.h * 0.4)
        break
        
      case 'butterfly':
        // Draw butterfly valve symbol (ellipse)
        dc.beginPath()
        dc.ellipse(centerX, centerY, block.w * 0.15, block.w * 0.05, Math.PI / 4, 0, 2 * Math.PI)
        dc.stroke()
        break
        
      case 'globe':
        // Draw globe valve symbol (hourglass shape)
        dc.beginPath()
        dc.moveTo(centerX - block.w * 0.1, centerY - block.h * 0.1)
        dc.lineTo(centerX, centerY)
        dc.lineTo(centerX - block.w * 0.1, centerY + block.h * 0.1)
        dc.moveTo(centerX + block.w * 0.1, centerY - block.h * 0.1)
        dc.lineTo(centerX, centerY)
        dc.lineTo(centerX + block.w * 0.1, centerY + block.h * 0.1)
        dc.stroke()
        break
    }
    
    // Display valve size
    const size = block.parameters['size'] || '4'
    drawCenterText(ctx, `${size}"`, { offsetY: 0.4, font: '10px Arial' })
    
    // Draw actuation indicator
    const actuationType = block.parameters['actuation'] || 'pneumatic'
    let actuationColor = 'gray'
    let actuationSymbol = 'M'
    
    switch (actuationType) {
      case 'pneumatic':
        actuationColor = 'blue'
        actuationSymbol = 'P'
        break
      case 'electric':
        actuationColor = 'yellow'
        actuationSymbol = 'E'
        break
      case 'hydraulic':
        actuationColor = 'red'
        actuationSymbol = 'H'
        break
      case 'manual':
        actuationColor = 'gray'
        actuationSymbol = 'M'
        break
    }
    
    // Draw actuation indicator circle
    dc.fillStyle = actuationColor
    dc.beginPath()
    dc.arc(centerX + block.w * 0.3, centerY - block.h * 0.3, 8, 0, 2 * Math.PI)
    dc.fill()
    
    // Add actuation symbol
    dc.fillStyle = 'white'
    dc.font = '8px Arial'
    dc.textAlign = 'center'
    dc.textBaseline = 'middle'
    dc.fillText(actuationSymbol, centerX + block.w * 0.3, centerY - block.h * 0.3)
    
    // Add interactive element for tags
    drawInteractiveElement(ctx, 'purple')
  },
  
  // Support dropping data tags
  supportedDropTypes: ['data-tag']
}

// Export as default for ES module compatibility
export default SmartValveBlock