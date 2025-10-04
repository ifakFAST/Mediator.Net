export interface Tag {
  id: string // Default: generated GUID (XmlAttribute "id")
  what: string // Default: "" (XmlAttribute "what") - Identifier of a row in the What metadata table
  unitSource: string // Default: "" (XmlAttribute "unitSource") - Identifier of a row in the Unit_Source metadata table
  unit: string // Default: "" (XmlAttribute "unit") - Determined by What metadata table

  depth?: number | null // in m - Default: null
  location?: string // module type specific location description, e.g. enum (front, middle, back) or (inlet, outlet)

  sampling: Sampling // Default: Sampling.Sensor (XmlAttribute "sampling")
  sensorDetails?: SensorDetails | null // Default: null - only if Sampling == Sensor
  autoSamplerDetails?: AutoSamplerDetails | null // Default: null - only if Sampling == AutoSampler
  notes: string // Default: ""
  sourceTag: string
}

// Sampling enum
export enum Sampling {
  Sensor = 'Sensor',
  GrabSampling = 'GrabSampling',
  AutoSampler = 'AutoSampler',
  Calculated = 'Calculated',
}

// SensorDetails record
export interface SensorDetails {
  type: SensorType // Default: SensorType.InSitu
  principle: MeasurementPrinciple // Default: MeasurementPrinciple.ISE
  t90: number // in minutes - Default: 0.0
}

// AutoSamplerDetails record
export interface AutoSamplerDetails {
  proportional: ProportionalType // Default: ProportionalType.Volume
  interval: number // in hours - Default: 1.0
  offset: number // in hours - Default: 0.0
  timestampPosition: TimestampPos // Default: TimestampPos.Start
}

// MeasurementPrinciple enum
export enum MeasurementPrinciple {
  ISE = 'ISE', // Ion Selective Electrode
  GSE = 'GSE', // Galvanic Sensor Electrode
  Colorimetric = 'Colorimetric',
  Spectral = 'Spectral',
}

// SensorType enum
export enum SensorType {
  InSitu = 'InSitu',
  ExSitu = 'ExSitu',
}

// ProportionalType enum
export enum ProportionalType {
  Volume = 'Volume',
  Time = 'Time',
  Flow = 'Flow',
}

// TimestampPos enum
export enum TimestampPos {
  Start = 'Start',
  Middle = 'Middle',
  End = 'End',
}

export function createTag(overrides?: Partial<Tag>): Tag {
  return {
    id: crypto.randomUUID(),
    what: '',
    unitSource: '',
    unit: '',
    sampling: Sampling.Sensor,
    sensorDetails: null,
    autoSamplerDetails: null,
    notes: '',
    sourceTag: '',
    ...overrides,
  }
}

export function createSensorDetails(overrides?: Partial<SensorDetails>): SensorDetails {
  return {
    type: SensorType.InSitu,
    principle: MeasurementPrinciple.ISE,
    t90: 0.0,
    ...overrides,
  }
}

export function createAutoSamplerDetails(overrides?: Partial<AutoSamplerDetails>): AutoSamplerDetails {
  return {
    proportional: ProportionalType.Volume,
    interval: 1.0,
    offset: 0.0,
    timestampPosition: TimestampPos.Start,
    ...overrides,
  }
}
