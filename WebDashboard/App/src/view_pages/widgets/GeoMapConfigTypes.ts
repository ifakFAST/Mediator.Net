import type * as fast from '../../fast_types'

export interface GeoMapConfig {
  MapConfig: MapConfig
  LegendConfig: LegendConfig
  TileLayers: TileLayer[]
  StaticLayers: StaticLayer[] // Not implemented yet
  MainLayers: MainLayer[] // Exclusive group (radio buttons or dropdown list)
  OptionalLayers: OptionalLayer[] // Non-exclusive group (checkboxes)
}

export const DefaultGeoMapConfig: GeoMapConfig = {
  MapConfig: {
    Center: '52.38671, 9.75749',
    ZoomDefault: 2,
    MainGroupLabel: 'Main',
    OptionalGroupLabel: 'Optional',
    MouseOverOpacityDelta: 0.3,
    GeoTiffResolution: 128,
    FrameDelay: 750,
    EndOfLoopPause: 1500,
    AutoPlayLoop: true,
  },
  LegendConfig: {
    File: '',
    Width: 50,
    Height: 100,
  },
  TileLayers: [],
  StaticLayers: [],
  MainLayers: [],
  OptionalLayers: [],
}

export interface MapConfig {
  Center: string
  ZoomDefault: number
  MainGroupLabel: string
  OptionalGroupLabel: string
  MouseOverOpacityDelta: number
  GeoTiffResolution: number
  FrameDelay: number // in milliseconds
  EndOfLoopPause: number // in milliseconds
  AutoPlayLoop: boolean
}

export interface LegendConfig {
  File: string
  Width: number
  Height: number
}

export interface TileLayer {
  Name: string
  Url: string
  Attribution: string
  MinZoom: number
  MaxZoom: number
}

export interface StaticLayer {
  Name: string
  FileName: string // the file name of a GeoJSON or GeoTiff file in the WebAssets folder
  IsSelected: boolean
}

export type GeoLayerType = 'GeoJson' | 'GeoTiff'

export interface NamedLayerType {
  Name: string
  Type: GeoLayerType
  Variable: fast.VariableRef
  FrameCount?: number
}

export interface MainLayer extends NamedLayerType {}

export interface OptionalLayer extends NamedLayerType {
  IsSelected: boolean
}
