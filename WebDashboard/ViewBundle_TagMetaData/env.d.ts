/// <reference types="vite/client" />

interface DashboardApp {
  registerResizeListener: (callback: () => void) => void
  registerViewEventListener: (callback: (eventName: string, eventPayload: any) => void) => void
  sendViewRequest: (requestType: string, params: any, callback?: (response: any) => void) => void
  sendViewRequestAsync: (requestType: string, params: any, responseType?: string) => Promise<any>
  canUpdateViewConfig: () => boolean
}

declare global {
  interface Window {
    parent: Window & {
      dashboardApp: DashboardApp
    }
  }
}
