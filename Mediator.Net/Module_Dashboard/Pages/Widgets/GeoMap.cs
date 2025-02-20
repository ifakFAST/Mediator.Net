﻿// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets;

[IdentifyWidget(id: "GeoMap")]
public class GeoMap : WidgetBaseWithConfig<GeoMapConfig>
{
    public override string DefaultHeight => "300px";

    public override string DefaultWidth => "100%";

    GeoMapConfig configuration => Config;

    public override Task OnActivate() {

        VariableRef[] variables = configuration.MainLayers.Select(layer => layer.Variable)
           .Concat(configuration.OptionalLayers.Select(layer => layer.Variable))
           .Distinct()
           .ToArray();

        Task ignored1 = Connection.EnableVariableValueChangedEvents(SubOptions.OnlyValueAndQualityChanges(sendValueWithEvent: true), variables);

        return Task.FromResult(true);
    }

    public Task<ReqResult> UiReq_GetItemsData() {

        ObjectRef[] usedObjects = configuration.MainLayers.Select(layer => layer.Variable.Object)
            .Concat(configuration.OptionalLayers.Select(layer => layer.Variable.Object))
            .Distinct()
            .ToArray();

        static bool IsJson(DataType t) => t == DataType.JSON;
        return Common.GetVarItemsData(Connection, usedObjects, IsJson);
    }

    public async Task<ReqResult> UiReq_GetGeoJson(VariableRef variable) {
        VTQ vtq = await Connection.ReadVariable(variable);
        return ReqResult.OK(vtq.V);
    }

    public async Task<ReqResult> UiReq_SaveConfig(GeoMapConfig config) {
        configuration.MapConfig = config.MapConfig;
        configuration.TileLayers = config.TileLayers;
        configuration.MainLayers = config.MainLayers;
        configuration.OptionalLayers = config.OptionalLayers;
        await Context.SaveWidgetConfiguration(configuration);
        return ReqResult.OK();
    }

    public override async Task OnVariableValueChanged(List<VariableValue> variables) {
        foreach (VariableValue variable in variables) {
            VariableRef var = variable.Variable;
            VTQ vtq = variable.Value;
            var payload = new {
                Object = var.Object,
                Name = var.Name,
                Value = vtq.V.JSON,
            };
            await Context.SendEventToUI("OnVarChanged", payload);
        }
    }
}

public sealed class GeoMapConfig {
    public MapConfig   MapConfig { get; set; } = new MapConfig();
    public TileLayer[] TileLayers { get; set; } = [
            new TileLayer() {
                Name = "OpenStreetMap",
                Url = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
                Attribution = "© OpenStreetMap contributors",
                MinZoom = 10,
                MaxZoom = 19
            },
            new TileLayer() {
                Name = "OpenTopoMap",
                Url = "https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png",
                Attribution = "© OpenTopoMap contributors",
                MinZoom = 10,
                MaxZoom = 17
            },
            new TileLayer() {
                Name = "Satellite",
                Url = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
                Attribution = "Tiles &copy; Esri &mdash; Source: Esri, i-cubed, USDA, USGS, AEX, GeoEye, Getmapping, Aerogrid, IGN, IGP, UPR-EGP, and the GIS User Community",
                MinZoom = 10,
                MaxZoom = 18
            }
        ];
    public MainLayer[] MainLayers { get; set; } = [];         // Exclusive
    public OptionalLayer[] OptionalLayers { get; set; } = []; // Checked
}

public sealed class MapConfig {
    public double ZoomDefault { get; set; } = 11.7;
    public string MainGroupLabel { get; set; } = "Main";
    public string OptionalGroupLabel { get; set; } = "Optional";
    public double MouseOverOpacityDelta { get; set; } = 0.3;
}

public sealed class TileLayer {
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Attribution { get; set; } = "";
    public double MinZoom { get; set; } = 10;
    public double MaxZoom { get; set; } = 19;
}

public sealed class MainLayer {
    public string Name { get; set; } = "";
    public VariableRef Variable { get; set; }
}

public sealed class OptionalLayer {
    public string Name { get; set; } = "";
    public VariableRef Variable { get; set; }
    public bool IsSelected { get; set; } = true;
}
