// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets;

[IdentifyWidget(id: "HistoryTable")]
public class HistoryTable : WidgetBaseWithConfig<HistoryTableConfig>
{
    private VariableRef? variable = null;

    public override string DefaultHeight => "";

    public override string DefaultWidth => "100%";

    HistoryTableConfig configuration => Config;

    public override Task OnActivate() {
        ResolveVariableAndSubscribe();
        return Task.CompletedTask;
    }

    private VariableRef? ResolveVariable(VariableRefUnresolved? unresolvedOrNull = null) {
        VariableRefUnresolved unresolved = unresolvedOrNull ?? configuration.Variable;
        if (string.IsNullOrEmpty(unresolved.Object.ToEncodedString()) || string.IsNullOrEmpty(unresolved.Name)) {
            if (!unresolvedOrNull.HasValue) {
                variable = null;
            }
            return null;
        }
        VariableRef resolved = Context.ResolveVariableRef(unresolved);
        if (!unresolvedOrNull.HasValue) {
            if (!variable.HasValue || variable.Value != resolved) {
                variable = resolved;
                Task ignored = Connection.EnableVariableHistoryChangedEvents([resolved]);
            }
        }
        return resolved;
    }

    private void ResolveVariableAndSubscribe() {
        VariableRef? resolved = ResolveVariable();
        if (resolved.HasValue) {
            Task ignored = Connection.EnableVariableHistoryChangedEvents([resolved.Value]);
        }
    }

    public Task<ReqResult> UiReq_GetItemsData() {
        ObjectRef[] usedObjects = string.IsNullOrEmpty(configuration.Variable.Object.ToEncodedString())
            ? []
            : [configuration.Variable.Object];
        return Common.GetVarItemsData(Connection, usedObjects, _ => true);
    }

    public record VarInfo(
        DataType Type,
        int Dimension,
        string TypeConstraints,
        string Unit
    );

    public async Task<ReqResult> UiReq_GetVariableInfo(VariableRefUnresolved? variable = null, Dictionary<string, string>? configVars = null) {
        Context.SetConfigVariables(configVars ?? []);
        VariableRef? resolved = ResolveVariable(variable);
        if (!resolved.HasValue) {
            return ReqResult.OK(new VarInfo(DataType.JSON, 1, "", ""));
        }

        ObjectInfo objInfo = await Connection.GetObjectByID(resolved.Value.Object);
        Variable variableMeta = objInfo.Variables.First(v => v.Name == resolved.Value.Name);
        return ReqResult.OK(new VarInfo(
            Type: variableMeta.Type,
            Dimension: variableMeta.Dimension,
            TypeConstraints: variableMeta.TypeConstraints,
            Unit: variableMeta.Unit
        ));
    }

    public async Task<ReqResult> UiReq_LoadHistory(string mode, long startJavaTicks, long endJavaTicks, Dictionary<string, string> configVars) {
        Context.SetConfigVariables(configVars ?? []);

        VariableRef? resolved;
        try {
            resolved = ResolveVariable();
        }
        catch {
            return ReqResult.OK(Array.Empty<HistoryRow>());
        }

        if (!resolved.HasValue) {
            return ReqResult.OK(Array.Empty<HistoryRow>());
        }

        Timestamp tStart = startJavaTicks <= 0 ? Timestamp.Empty : Timestamp.FromJavaTicks(startJavaTicks);
        Timestamp tEnd = endJavaTicks <= 0 ? Timestamp.Max : Timestamp.FromJavaTicks(endJavaTicks);
        BoundingMethod bounding = mode == "Last" ? BoundingMethod.TakeLastN : BoundingMethod.TakeFirstN;

        try {
            List<VTTQ> data = await Connection.HistorianReadRaw(resolved.Value, tStart, tEnd, ClampRowCount(configuration.RowCount), bounding);
            HistoryRow[] rows = data.Select(d => new HistoryRow(
                T: FormatTimestamp(d.T, configuration.TimestampFormat),
                TJ: d.T.JavaTicks,
                Q: d.Q.ToString(),
                V: d.V.JSON
            )).ToArray();
            return ReqResult.OK(rows);
        }
        catch {
            return ReqResult.OK(Array.Empty<HistoryRow>());
        }
    }

    public async Task<ReqResult> UiReq_CountHistory(Dictionary<string, string> configVars) {
        Context.SetConfigVariables(configVars ?? []);

        try {
            VariableRef? resolved = ResolveVariable();
            if (!resolved.HasValue) {
                return ReqResult.OK(new { Count = 0L });
            }
            long count = await Connection.HistorianCount(resolved.Value, Timestamp.Empty, Timestamp.Max);
            return ReqResult.OK(new { Count = count });
        }
        catch {
            return ReqResult.OK(new { Count = 0L });
        }
    }

    public async Task<ReqResult> UiReq_SaveConfig(HistoryTableConfig config, Dictionary<string, string>? configVars = null) {
        Context.SetConfigVariables(configVars ?? []);

        configuration.Variable = config.Variable;
        configuration.RowCount = ClampRowCount(config.RowCount);
        configuration.ShowQualityColumn = config.ShowQualityColumn;
        configuration.ColorQualityGood = config.ColorQualityGood;
        configuration.ColorQualityUncertain = config.ColorQualityUncertain;
        configuration.ColorQualityBad = config.ColorQualityBad;
        configuration.StructColumns = config.StructColumns;
        configuration.InitialPosition = config.InitialPosition;
        configuration.TimeColumnTitle = config.TimeColumnTitle;
        configuration.TimestampFormat = string.IsNullOrWhiteSpace(config.TimestampFormat)
            ? HistoryTableConfig.DefaultTimestampFormat
            : config.TimestampFormat;
        configuration.FractionDigits = config.FractionDigits;

        await Context.SaveWidgetConfiguration(configuration);
        ResolveVariableAndSubscribe();
        return ReqResult.OK();
    }

    public override async Task OnVariableHistoryChanged(List<HistoryChange> changes) {
        VariableRef? resolved = variable;
        if (resolved.HasValue && changes.Any(ch => ch.Variable == resolved.Value)) {
            await Context.SendEventToUI("OnHistoryChanged", new { });
        }
    }

    private static int ClampRowCount(int rowCount) => Math.Clamp(rowCount, 1, 10000);

    private static string FormatTimestamp(Timestamp t, string? format) {
        DateTime tLocal = AppTimeZone.ConvertToLocalTime(t);
        string timestampFormat = string.IsNullOrWhiteSpace(format) ? HistoryTableConfig.DefaultTimestampFormat : format;
        try {
            return tLocal.ToString(timestampFormat, CultureInfo.InvariantCulture);
        }
        catch (FormatException) {
            return tLocal.ToString(HistoryTableConfig.DefaultTimestampFormat, CultureInfo.InvariantCulture);
        }
    }

    public record HistoryRow(
        string T,
        long TJ,
        string Q,
        string V
    );
}

public class HistoryTableConfig
{
    public const string DefaultTimestampFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss";

    public VariableRefUnresolved Variable { get; set; }
    public int RowCount { get; set; } = 12;
    public bool ShowQualityColumn { get; set; } = true;
    public string ColorQualityGood { get; set; } = "black";
    public string ColorQualityUncertain { get; set; } = "orange";
    public string ColorQualityBad { get; set; } = "red";
    public string StructColumns { get; set; } = "";
    public HistoryTableInitialPosition InitialPosition { get; set; } = HistoryTableInitialPosition.Latest;
    public string TimeColumnTitle { get; set; } = "Timestamp";
    public string TimestampFormat { get; set; } = DefaultTimestampFormat;
    public int FractionDigits { get; set; } = 2;
}

public enum HistoryTableInitialPosition
{
    Latest,
    Oldest
}
