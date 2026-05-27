export const enumValuesHelpText = `Map numeric values to display labels. Separate entries with semicolons (;).

Simple: value=label
Example: 0=Off; 1=On

With color (shown when the value is displayed):
value={label, color}
Example: 0={Off, #888888}; 1={On, green}; 2={Fault, red}

Color can be a CSS name or hex (#RRGGBB).`

export const enumValuesHelpStyle = {
  whiteSpace: 'pre-line',
  maxWidth: '420px',
  display: 'block',
} as const

export const enumValuesPlaceholder = '0=Off; 1={On, #00AA00}'
