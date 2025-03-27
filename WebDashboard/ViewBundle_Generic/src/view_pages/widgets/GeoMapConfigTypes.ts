import * as fast from '../../fast_types'

export interface GeoMapConfig {
  MapConfig: MapConfig
  LegendConfig: LegendConfig
  TileLayers: TileLayer[]
  MainLayers: MainLayer[]         // Exclusive group (radio buttons or dropdown list)
  OptionalLayers: OptionalLayer[] // Non-exclusive group (checkboxes)
}

export const DefaultGeoMapConfig: GeoMapConfig = {
  MapConfig: {
    Center: '52.38671, 9.75749',
    ZoomDefault: 2,
    MainGroupLabel: 'Main',
    OptionalGroupLabel: 'Optional',
    MouseOverOpacityDelta: 0.3,
    GeoTiffResolution: 128
  },
  LegendConfig: {
    File: '',
    Width: 50,
    Height: 100
  },
  TileLayers: [],
  MainLayers: [],
  OptionalLayers: []
}

export interface MapConfig {
  Center: string
  ZoomDefault: number
  MainGroupLabel: string
  OptionalGroupLabel: string
  MouseOverOpacityDelta: number
  GeoTiffResolution: number
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

export type GeoLayerType = 'GeoJson' | 'GeoTiff'

export interface NamedLayerType {
  Name: string
  Type: GeoLayerType
  Variable: fast.VariableRef
}

export interface MainLayer extends NamedLayerType {
  Name: string
  Type: GeoLayerType
  Variable: fast.VariableRef
}

export interface OptionalLayer extends NamedLayerType{
  Name: string
  Type: GeoLayerType
  Variable: fast.VariableRef
  IsSelected: boolean
}
