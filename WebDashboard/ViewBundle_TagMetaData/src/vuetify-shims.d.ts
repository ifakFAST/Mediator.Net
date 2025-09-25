// Vuetify 3 slot type augmentations
declare module 'vuetify/components' {
  interface VFieldSlot {
    field: any
  }

  interface VInputSlot {
    input: any
  }

  interface DefaultInputSlot {
    item: any
  }

  interface LoaderSlotProps {
    loading: boolean
  }
}

// Global slot interface extensions
declare global {
  interface VFieldSlot {
    field: any
  }

  interface VInputSlot {
    input: any
  }

  interface DefaultInputSlot {
    item: any
  }

  interface LoaderSlotProps {
    loading: boolean
  }
}
