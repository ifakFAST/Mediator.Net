
export interface Alarm {
  T: number
  Source: string
  Severity: Severity
  State: EventState
  Message: string
  selected: boolean
}

type EventState = 'New' | 'Ack' | 'Reset'

type Severity = 'Info' | 'Warning' | 'Alarm'
