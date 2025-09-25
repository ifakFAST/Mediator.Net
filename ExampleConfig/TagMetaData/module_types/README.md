# Dynamic Module Types Examples

This directory contains examples of how to create dynamic module types for the TagMetaData system using ES Modules.

## Files

### `util.js`
Contains reusable helper functions and utilities that can be imported by individual module type files:

- **Parameter helpers**: `paramNumeric()`, `paramString()`, `paramBool()`, `paramEnum()`
- **IO helpers**: `makeInput()`, `makeOutput()`
- **Drawing helpers**: `drawCenterText()`, `drawCenterTextAbove()`, `drawInteractiveElement()`, `drawSimpleBorder()`, `drawConnectionLines()`

### `custom_mixer.js`
Example of a custom mixer module with:
- Multiple configurable inputs
- Dynamic IO generation based on parameters
- Custom drawing with mixing visualization
- Conditional heating element
- Interactive elements for tag support

### `smart_valve.js`
Example of a smart valve module with:
- Different valve type visualizations (ball, gate, butterfly, globe)
- Actuation type indicators
- Parameter-driven IO configuration
- Custom symbols and status indicators

## How to Create a New Module Type

1. **Create a new `.js` file** with the module type ID as part of the filename (e.g., `module.my_pump.js`)

2. **Import helpers** from the util file:
   ```javascript
   import { 
     paramNumeric, 
     makeInput, 
     makeOutput, 
     drawCenterText 
   } from './util.js'
   ```

3. **Define the module type object** with required properties:
   ```javascript
   const MyPumpBlock = {
     id: 'my_pump', // Must match the filename
     parameters: [
       paramNumeric('flow_rate', 'Flow Rate (L/min)', 100, 1, 1000)
     ],
     defineIOs(parameters) {
       return [
         makeInput('inlet', 'left', 'Water'),
         makeOutput('outlet', 'right', 'Water')
       ]
     },
     customDraw(ctx) {
       drawCenterText(ctx, 'PUMP')
     },
     supportedDropTypes: ['data-tag'] // Optional
   }
   ```

4. **Export as default**:
   ```javascript
   export default MyPumpBlock
   ```

## Module Type Structure

Each module type must have:

- **`id`**: Unique identifier (string) matching the filename
- **`parameters`**: Array of parameter definitions
- **`defineIOs(parameters)`**: Function returning array of IO definitions
- **`customDraw(ctx)`**: Optional function for custom visualization
- **`supportedDropTypes`**: Optional array of supported drag-drop types
- **`updateParams(parameters)`**: Optional function for dynamic parameter updates

## Configuration

To use these dynamic module types:

1. Set the `module-types-path` configuration in your TagMetaData module config to point to this directory
2. The system will automatically enumerate all `module.*.js` files and load them as ES modules
3. Module types will be available in the flow diagram editor

## Drawing Context

The `customDraw` function receives a context object with:
- **`ctx.block`**: Block information (position, size, parameters, tags, ports)
- **`ctx.dc`**: Canvas 2D rendering context
- **`ctx.ia`**: Interactive area manager for adding clickable elements

## Best Practices

1. **Use helper functions** from `util.js` for consistency
2. **Validate parameters** before using them in calculations
3. **Handle missing parameters** with sensible defaults
4. **Test your drawing code** with different block sizes
5. **Add error handling** for robust operation
6. **Follow naming conventions** for parameter IDs and port names