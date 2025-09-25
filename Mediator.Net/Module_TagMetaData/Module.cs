// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;

namespace Ifak.Fast.Mediator.TagMetaData;

public class Module : ModelObjectModule<Config.TagMetaData_Model>, EventListener
{
    private ModuleInitInfo info;
    private Connection connection = new ClosedConnection();

    private MetaModel metaModel = new();
    private DataValue metaModelDataValue = DataValue.Empty;

    private DataValue blockLibraryDataValue = DataValue.Empty;
    private DataValue imagePathDataValue = DataValue.Empty;
    private DataValue moduleTypesPathDataValue = DataValue.Empty;
    private string moduleTypesPath = "";

    public override async Task Init(ModuleInitInfo info, VariableValue[] restoreVariableValues, Notifier notifier, ModuleThread moduleThread) {
        this.info = info;
        await Connect();
        await base.Init(info, restoreVariableValues, notifier, moduleThread);

        var config = info.GetConfigReader();
        string metaModelFile = Path.GetFullPath(config.GetString("meta-model-file"));
        string blockLibFile = Path.GetFullPath(config.GetString("block-library-file"));
        string imagePath = Path.GetFullPath(config.GetString("images-path"));
        moduleTypesPath = Path.GetFullPath(config.GetString("module-types-path"));
        
        if (!Directory.Exists(imagePath)) {
            throw new Exception($"'images-path' does not exist or is not a directory: {imagePath}");
        }

        if (!Directory.Exists(moduleTypesPath)) {
            throw new Exception($"'module-types-path' does not exist or is not a directory: {moduleTypesPath}");
        }

        imagePathDataValue = DataValue.FromString(imagePath);
        moduleTypesPathDataValue = DataValue.FromString(moduleTypesPath);

        string jsonBlockLib = File.ReadAllText(blockLibFile, Encoding.UTF8);
        if (!StdJson.IsValidJson(jsonBlockLib)) {
            throw new Exception($"Invalid JSON in block library file: {blockLibFile}");
        }
        blockLibraryDataValue = DataValue.FromJSON(jsonBlockLib);

        metaModel = Xml.FromXmlFile<MetaModel>(metaModelFile);
        metaModelDataValue = DataValue.FromObject(metaModel);
    }

    protected override async Task OnConfigModelChanged(bool init) {
        await base.OnConfigModelChanged(init);
        await EnableEvents();
    }

    private async Task Connect() {
        connection = await HttpConnection.ConnectWithModuleLogin(info, this);
    }

    private async Task EnableEvents() {

        VariableRef[] variables = model.GetAllTags()
            .Where(t => t.GetSourceTagVarRef().HasValue)
            .Select(v => v.GetSourceTagVarRef()!.Value)
            .ToArray();

        await connection.DisableChangeEvents();
        await connection.EnableVariableValueChangedEvents(
            SubOptions.OnlyValueAndQualityChanges(sendValueWithEvent: true), 
            variables);
    }

    private bool running = false;

    public override async Task Run(Func<bool> shutdown) {
        running = true;
        while (!shutdown()) {
            await Task.Delay(500);
        }
        running = false;
    }

    public override async Task<Result<DataValue>> OnMethodCall(Origin origin, string methodName, NamedValue[] parameters) {
        return methodName switch {
            "GetMetaModel" => Result<DataValue>.OK(metaModelDataValue),
            "GetBlockLib" => Result<DataValue>.OK(blockLibraryDataValue),
            "SaveBlockLib" => SaveBlockLib(parameters),
            "GetImagePath" => Result<DataValue>.OK(imagePathDataValue),
            "GetModuleTypePath" => Result<DataValue>.OK(moduleTypesPathDataValue),
            "GetModuleTypes" => Result<DataValue>.OK(GetModuleTypes()),
            _ => await base.OnMethodCall(origin, methodName, parameters),
        };
    }

    private Result<DataValue> SaveBlockLib(NamedValue[] parameters) {
        NamedValue nvBlockLibJson = parameters[0];
        string json = nvBlockLibJson.Value;
        var config = info.GetConfigReader();
        string blockLibFile = Path.GetFullPath(config.GetString("block-library-file"));
        File.WriteAllText(blockLibFile, json, Encoding.UTF8);
        blockLibraryDataValue = DataValue.FromJSON(json);
        return Result<DataValue>.OK(DataValue.Empty);
    }

    private DataValue GetModuleTypes() {
        try {
            if (!Directory.Exists(moduleTypesPath)) {
                return DataValue.FromObject(new string[0]);
            }

            var jsFiles = Directory.GetFiles(moduleTypesPath, "module.*.js", SearchOption.TopDirectoryOnly);
            var moduleTypeIds = jsFiles
                .Select(file => Path.GetFileNameWithoutExtension(file).Substring(7))
                .ToArray();

            return DataValue.FromObject(moduleTypeIds);
        }
        catch (Exception) {
            return DataValue.FromObject(new string[0]);
        }
    }

    private readonly Dictionary<VariableRef, Config.Tag> variableToTag = [];

    Task EventListener.OnConfigChanged(List<ObjectRef> changedObjects) {

        variableToTag.Clear();
        foreach (Config.Tag tag in model.GetAllTags()) {
            VariableRef? vr = tag.GetSourceTagVarRef();
            if (vr.HasValue) {
                variableToTag[vr.Value] = tag;
            }
        }

        return Task.FromResult(true);
    }

    Task EventListener.OnVariableValueChanged(VariableValues variables) {
        VariableValues list = [];
        foreach (VariableValue vv in variables) {
            VariableRef vr = vv.Variable;
            if (variableToTag.TryGetValue(vr, out Config.Tag? tag)) {
                VTQ vtq = vv.Value;
                double? value = vtq.V.AsDouble();
                if (value.HasValue && vtq.NotBad) {
                    DataValue converted = UnitConversion(tag, value.Value, vtq.V);
                    vtq = vtq.WithValue(converted);
                    ObjectRef tagID = ObjectRef.Make(moduleID, tag.ID);
                    list.Add(VariableValue.Make(VariableRef.Make(tagID, "Value"), vtq));
                }
            }
        }
        SetVariableValues(list);
        return Task.FromResult(true);
    }

    private DataValue UnitConversion(Config.Tag tag, double value, DataValue dv) {
        // TODO: Implement unit conversion logic based on tag.Unit and tag.UnitSource
        return dv;
    }

    private void SetVariableValues(VariableValues vv) {
        notifier?.Notify_VariableValuesChanged(vv);
    }

    Task EventListener.OnVariableHistoryChanged(List<HistoryChange> changes) {
        return Task.FromResult(true);
    }

    Task EventListener.OnAlarmOrEvents(List<AlarmOrEvent> alarmOrEvents) {
        return Task.FromResult(true);
    }

    Task EventListener.OnConnectionClosed() {
        _ = CheckNeedConnectionRestart();
        return Task.FromResult(true);
    }

    private async Task CheckNeedConnectionRestart() {

        await Task.Delay(1000);

        if (running) {

            Console.Error.WriteLine($"{Timestamp.Now}: EventListener.OnConnectionClosed. Restarting connection...");
            Console.Error.Flush();

            try {
                await Connect();
                await EnableEvents();
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                Console.Error.WriteLine($"{Timestamp.Now}: Restarting connection failed: {e.Message}");
                Console.Error.WriteLine($"{Timestamp.Now}: Terminating in 5 seconds...");
                Console.Error.Flush();
                await Task.Delay(5000);
                Environment.Exit(1);
            }
        }
    }
}
