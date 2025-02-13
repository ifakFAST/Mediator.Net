import * as L from 'leaflet';

declare module 'leaflet' {
    interface ControlOptions {
        exclusiveGroups?: string[];
        groupCheckboxes?: boolean;
    }

    namespace control {
        interface GroupedLayers extends L.Control {
            addOverlay(layer: L.Layer, name: string, group: string): void;
            removeLayer(layer: L.Layer): void;
        }
        
        function groupedLayers(
            baseLayers?: {[key: string]: L.Layer}, 
            overlays?: {[key: string]: {[key: string]: L.Layer}}, 
            options?: ControlOptions
        ): GroupedLayers;
    }
}