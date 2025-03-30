// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;
using VTTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTTQ>;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets;

[IdentifyWidget(id: "GeoMap")]
public class GeoMap : WidgetBaseWithConfig<GeoMapConfig>
{
    private VariableRefUnresolved[] variablesUnresolved = [];
    private VariableRef[] variables = [];

    public override string DefaultHeight => "300px";

    public override string DefaultWidth => "100%";

    GeoMapConfig configuration => Config;

    private bool showLatest = true;

    public VariableRefUnresolved[] VariablesUnresolved {
        get => variablesUnresolved;
        set {
            variablesUnresolved = value;
            ResolveVariables();
        }
    }

    VariableRef[] ResolveVariables() {
        VariableRef[] newVariables = variablesUnresolved.Select(v => Context.ResolveVariableRef(v)).ToArray();
        if (!Arrays.Equals(newVariables, variables)) {
            variables = newVariables;
            Task ignored = Connection.EnableVariableValueChangedEvents(SubOptions.OnlyValueAndQualityChanges(sendValueWithEvent: false), newVariables);
        }
        return variables;
    }

    public override Task OnActivate() {
        VariablesUnresolved = GetVariablesUnresolved();
        return Task.FromResult(true);
    }

    private VariableRefUnresolved[] GetVariablesUnresolved() {
        return configuration.MainLayers.Select(layer => layer.Variable)
            .Concat(configuration.OptionalLayers.Select(layer => layer.Variable))
            .Distinct()
            .ToArray();
    }

    public Task<ReqResult> UiReq_GetItemsData() {

        ObjectRef[] usedObjects = configuration.MainLayers.Select(layer => layer.Variable.Object)
            .Concat(configuration.OptionalLayers.Select(layer => layer.Variable.Object))
            .Distinct()
            .ToArray();

        static bool IsJson(DataType t) => t == DataType.JSON;
        return Common.GetVarItemsData(Connection, usedObjects, IsJson);
    }

    public async Task<ReqResult> UiReq_GetGeoData(VariableRefUnresolved variable, TimeRange timeRange) {
        showLatest = timeRange.Type == TimeType.Last;
        var variableResolved = Context.ResolveVariableRef(variable);
        if (timeRange.Type == TimeType.Last) {
            
            VTQ vtq = await Connection.ReadVariable(variableResolved);
            return ReqResult.OK(vtq.V);
        }
        else {
            Timestamp end = timeRange.GetEnd();
            DataValue? v = await ReadLatestOlderOrEqualT(variableResolved, end);
            if (v == null) return ReqResult.Bad($"No data found for t = {end}");
            return ReqResult.OK(v.Value);
        }
    }

    async Task<DataValue?> ReadLatestOlderOrEqualT(VariableRef vref, Timestamp t) {
        Timestamp tMaxLoockback = t - Duration.FromSeconds(59); // TODO: make configurable?
        VTTQs vttqs = await Connection.HistorianReadRaw(vref, tMaxLoockback, t, 1, BoundingMethod.TakeLastN);
        if (vttqs.Count == 0) return null;
        VTTQ vtq = vttqs[0];
        return vtq.V;
    }

    public async Task<ReqResult> UiReq_SaveConfig(GeoMapConfig config) {
        VariablesUnresolved = GetVariablesUnresolved();
        configuration.MapConfig = config.MapConfig;
        configuration.LegendConfig = config.LegendConfig;
        configuration.TileLayers = config.TileLayers;
        configuration.MainLayers = config.MainLayers;
        configuration.OptionalLayers = config.OptionalLayers;
        await Context.SaveWidgetConfiguration(configuration);
        return ReqResult.OK();
    }

    public override async Task OnVariableValueChanged(List<VariableValue> variables) {
        if (!showLatest) return;
        foreach (VariableValue variable in variables) {
            VariableRef var = variable.Variable;
            VTQ vtq = variable.Value;
            var payload = new {
                Object = var.Object,
                Name = var.Name,
            };
            await Context.SendEventToUI("OnVarChanged", payload);
        }
    }
}

public sealed class GeoMapConfig {
    public MapConfig   MapConfig { get; set; } = new MapConfig();
    public LegendConfig LegendConfig { get; set; } = new LegendConfig();
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
    public string Center { get; set; } = "52.38671, 9.75749";
    public double ZoomDefault { get; set; } = 11.7;
    public string MainGroupLabel { get; set; } = "Main";
    public string OptionalGroupLabel { get; set; } = "Optional";
    public double MouseOverOpacityDelta { get; set; } = 0.3;
    public double GeoTiffResolution { get; set; } = 128;
}

public sealed class LegendConfig {
    public string File { get; set; } = ""; // Must be located in WebAssets folder
    public double Width { get; set; } = 50;
    public double Height { get; set; } = 100;
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
    public LayerType Type { get; set; } = LayerType.GeoJson;
    public VariableRefUnresolved Variable { get; set; }
}

public sealed class OptionalLayer {
    public string Name { get; set; } = "";
    public LayerType Type { get; set; } = LayerType.GeoJson;
    public VariableRefUnresolved Variable { get; set; }
    public bool IsSelected { get; set; } = true;
}

public enum LayerType {
    GeoJson,
    GeoTiff
}