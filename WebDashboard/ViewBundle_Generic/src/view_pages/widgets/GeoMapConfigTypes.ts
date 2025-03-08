import * as fast from '../../fast_types'

export interface GeoMapConfig {
  MapConfig: MapConfig
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
    MouseOverOpacityDelta: 0.3
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
