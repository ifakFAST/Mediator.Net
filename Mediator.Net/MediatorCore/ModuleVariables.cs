// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace Ifak.Fast.Mediator
{
    class ModuleVariables
    {
        private static readonly Logger logger = LogManager.GetLogger("Mediator.Core");

        private readonly string moduleID;
        private readonly string moduleName;
        private readonly string fileName;
        private bool changed = false;

        private bool closed = false;
        private long updateCounter = 0;

        private readonly Dictionary<VariableRef, VTQ> map = new();
        private IList<ObjectInfo> allObjects = Array.Empty<ObjectInfo>();

        private Util.FileStore? fileStore = null;

        public ModuleVariables(string moduleID, string moduleName, string fileName) {
            this.moduleID = moduleID;
            this.moduleName = moduleName;
            this.fileName = fileName;
        }

        public void Sync(IList<ObjectInfo> allObjects) {

            var validVarRefs = new HashSet<VariableRef>();
            var tEmpty = Timestamp.Empty;

            foreach (ObjectInfo obj in allObjects) {
                if (obj.Variables == null || obj.Variables.Length == 0) continue;
                foreach (Variable v in obj.Variables) {
                    var vref = new VariableRef(obj.ID, v.Name);
                    validVarRefs.Add(vref);
                    if (!map.ContainsKey(vref)) {
                        map[vref] = new VTQ(tEmpty, Quality.Bad, v.DefaultValue);
                    }
                }
            }

            List<VariableRef>? varsToRemove = null;
            foreach (VariableRef vref in map.Keys) {
                if (!validVarRefs.Contains(vref)) {
                    varsToRemove ??= new List<VariableRef>();
                    varsToRemove.Add(vref);
                }
            }

            if (varsToRemove != null) {
                foreach (VariableRef vref in varsToRemove) {
                    map.Remove(vref);
                }
            }

            this.allObjects = allObjects;
        }

        public VTQ GetVarValue(VariableRef varRef) {
            try {
                return map[varRef];
            }
            catch (Exception) {
                throw new Exception($"No value found for variable reference '{varRef}'.");
            }
        }

        public bool HasVarValue(VariableRef varRef) {
            return map.ContainsKey(varRef);
        }

        public void ValidateVariableValuesOrThrow(IList<VariableValue> values) {
            foreach (VariableValue value in values) {
                VariableRef varRef = value.Variable;
                if (!map.ContainsKey(varRef))
                    throw new Exception("Invalid variable reference: " + varRef.ToString());
            }
        }

        public void ValidateVariableRefsOrThrow(IList<VariableRef> values) {
            foreach (VariableRef varRef in values) {
                if (!map.ContainsKey(varRef))
                    throw new Exception("Invalid variable reference: " + varRef.ToString());
            }
        }

        public VariableValuePrev[] UpdateVariableValues(IList<VariableValue> values) {

            VariableValuePrev[] previousValues = new VariableValuePrev[values.Count];

            for (int i = 0; i < values.Count; ++i) {
                VariableValue vv = values[i];
                var varRef = vv.Variable;
                try {
                    previousValues[i] = new VariableValuePrev(vv, map[varRef]);
                }
                catch (Exception) {
                    previousValues[i] = new VariableValuePrev(vv, new VTQ(Timestamp.Empty, Quality.Bad, DataValue.Empty));
                    logger.Warn("Update of undeclared variable: " + varRef);
                }
                map[varRef] = vv.Value;
            }

            changed = true;
            updateCounter += 1;

            return previousValues;
        }

        private async Task FlushTask(long initialCounter) {
            long prevUpdateCounter = initialCounter;
            long writeCounter = 0;
            while (!closed) {
                if (updateCounter != prevUpdateCounter) {
                    prevUpdateCounter = updateCounter;
                    await Write(updateCounter);
                    writeCounter += 1;
                }
                int waitSeconds = writeCounter < 100 ? 5 : 60;
                await Task.Delay(waitSeconds * 1000);
            }
        }

        public async Task Flush() {
            if (changed) {
                await Write(updateCounter);
            }
        }

        public void Shutdown() {
            closed = true;
            _ = fileStore?.Terminate();
        }

        public void StartAndLoad() {

            closed = false;
            this.fileStore = new Util.FileStore(fileName);
            _ = FlushTask(updateCounter);

            try {
                map.Clear();
                if (!string.IsNullOrEmpty(fileName)) {
                    if (File.Exists(fileName)) {
                        using var reader = new XmlTextReader(fileName);
                        ReadModuleVariables(reader);
                    }
                    else {
                        logger.Info($"No file {fileName} found for variables of module {moduleName}");
                    }
                }
            }
            catch (Exception exp) {
                logger.Error(exp, "Failed to load variables for module " + moduleName);
            }
        }

        public async Task Write(long counter) {

            if (string.IsNullOrEmpty(fileName)) return;

            string content = "";

            using (var sw = new StringWriter()) {

                try {
                    using var writer = new XmlTextWriter(sw);
                    writer.Formatting = Formatting.Indented;
                    WriteVariables(writer, moduleID, moduleName);
                }
                catch (Exception exp) {
                    logger.Error(exp, "Failed to persist variables for module " + moduleName);
                    return;
                }
                content = sw.ToString();
            }

            try {

                var fs = fileStore;
                if (fs != null) {
                    await fs.Save(content);
                }

                if (updateCounter == counter) {
                    changed = false;
                }
            }
            catch (Exception exp) {
                logger.Error(exp, "Failed to persist variables for module " + moduleName);
            }
        }

        public VariableValue[] GetVariableValues() => map.Select(kv => new VariableValue(kv.Key, kv.Value)).ToArray();

        private void WriteVariables(XmlWriter writer, string moduleID, string moduleName) {

            writer.WriteStartElement("Module");
            writer.WriteAttributeString("name", moduleName);
            writer.WriteAttributeString("id", moduleID);

            foreach (ObjectInfo obj in allObjects) {

                if (obj.Variables == null || obj.Variables.Length == 0 || obj.Variables.All(v => !v.Remember)) continue;

                writer.WriteStartElement("Obj");
                writer.WriteAttributeString("name", obj.Name);
                writer.WriteAttributeString("id", obj.ID.LocalObjectID);

                foreach (Variable v in obj.Variables) {

                    if (v.Remember) {
                        var vref = new VariableRef(obj.ID, v.Name);
                        if (map.TryGetValue(vref, out VTQ vtq)) {
                            writer.WriteStartElement("Var");
                            writer.WriteAttributeString("name", v.Name);
                            writer.WriteAttributeString("time", vtq.T.ToString());
                            writer.WriteAttributeString("quality", vtq.Q.ToString());
                            writer.WriteValue(vtq.V.ToString());
                            writer.WriteEndElement();
                        }
                    }
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private void ReadModuleVariables(XmlReader reader) {

            map.Clear();

            if (reader.IsEmptyElement) return;

            ObjectRef currentObj = new ObjectRef();

            string var_Name = "";
            string var_Time = "";
            string var_Quality = "";
            bool inVar = false;

            while (reader.Read()) {

                switch (reader.NodeType) {

                    case XmlNodeType.Element:

                        inVar = false;

                        switch (reader.Name) {
                            case "Obj":
                                string id = reader.GetAttribute("id") ?? throw new Exception($"Missing id attribute for object");
                                currentObj = ObjectRef.Make(moduleID, id);                                
                                break;

                            case "Var":
                                var_Name = reader.GetAttribute("name") ?? throw new Exception($"Missing name attribute for object {currentObj}");
                                var_Time = reader.GetAttribute("time") ?? throw new Exception($"Missing time attribute for object {currentObj}");
                                var_Quality = reader.GetAttribute("quality") ?? throw new Exception($"Missing quality attribute for object {currentObj}");
                                inVar = true;
                                break;
                        }
                        break;

                    case XmlNodeType.Text:

                        if (inVar) {
                            var t = TimestampFromString(var_Time);
                            var q = QualityFromString(var_Quality);
                            var v = DataValue.FromJSON(reader.Value);

                            var varRef = new VariableRef(currentObj, var_Name);
                            map[varRef] = new VTQ(t, q, v);

                            inVar = false;
                        }
                        break;

                    case XmlNodeType.EndElement:
                        inVar = false;
                        if (reader.Name == "Module")
                            return;
                        break;
                }
            }
        }

        private static Quality QualityFromString(string q) {
            return q switch {
                "Good" => Quality.Good,
                "Uncertain" => Quality.Uncertain,
                _ => Quality.Bad,
            };
        }

        private static Timestamp TimestampFromString(string time) {
            try {
                return Timestamp.FromISO8601(time);
            }
            catch (Exception) {
                return Timestamp.Empty;
            }
        }
    }

    public class VariableValuePrev
    {
        public VariableValue Value { get; private set; }
        public VTQ PreviousValue { get; private set; }

        public VariableValuePrev(VariableValue value, VTQ previousValue) {
            this.Value = value;
            this.PreviousValue = previousValue;
        }
    }
}
