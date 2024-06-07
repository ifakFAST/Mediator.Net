
export interface Alarm {
  T: number
  TimeFirstLocal: string
  TimeLastLocal: string
  TimeAckLocal: string
  TimeResetLocal: string
  TimeRTNLocal: string
  Source: string
  Type: string
  Severity: Severity
  State: EventState
  RTN: boolean
  Message: string
  Details: string
  Count: number
  Initiator?: Origin
  Objects?: string[]
  InfoACK?: UserAction
  InfoReset?: UserAction
  InfoRTN?: InfoRTN
  selected: boolean
}

export interface Origin {
  ID: string
  Name: string
  Type: OriginType
}

interface UserAction {
  Time: string
  UserID: string
  UserName: string
  Comment: string
}

interface InfoRTN {
  Time: string
  Message: string
}

type OriginType = 'Module' | 'User'

type EventState = 'New' | 'Ack' | 'Reset'

type Severity = 'Info' | 'Warning' | 'Alarm'
