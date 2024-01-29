// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Calc.Adapter_CSharp;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Calc.Adapter_Python;

public class PythonExternal : CalculationBase, EventSink, ConnectionConsumer {

    private InputBase[] inputs = Array.Empty<Input>();
    private OutputBase[] outputs = Array.Empty<Output>();
    private AbstractState[] states = Array.Empty<AbstractState>();
    private Action<Timestamp, Duration> stepAction = (t, dt) => { };
    private Action shutdownAction = () => { };
    private Duration cycle = Duration.FromSeconds(1);
    private AdapterCallback? callback;

    private PyModule? moduleOuter = null;
    private PyModule? module = null;

    public override async Task<InitResult> Initialize(InitParameter parameter, AdapterCallback callback) {

        this.callback = callback;
        string code = parameter.Calculation.Definition;
        cycle = parameter.Calculation.Cycle;

        if (!string.IsNullOrWhiteSpace(code)) {

            return await DoInit(parameter, code);
        }
        else {

            return new InitResult() {
                Inputs = Array.Empty<InputDef>(),
                Outputs = Array.Empty<OutputDef>(),
                States = Array.Empty<StateDef>(),
                ExternalStatePersistence = true
            };
        }
    }

    private Func<Task<Connection>> retriever = () => Task.FromResult((Connection)new ClosedConnection());

    public void SetConnectionRetriever(Func<Task<Connection>> retriever) {
        this.retriever = retriever;
    }

    private async Task<InitResult> DoInit(InitParameter parameter, string code) {

        await Task.CompletedTask;

        var config = new Mediator.Config(parameter.ModuleConfig);
        string pythonDLL = config.GetString("python-dll");
        string libDirs = config.GetOptionalString("python-library-directories", "");

        string[] libraryDirs = libDirs
            .Split(new char[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToArray();

        string[] absoluteLibDirs = libraryDirs.Select(Path.GetFullPath).ToArray();
        foreach (string libDir in absoluteLibDirs) {
            if (!Directory.Exists(libDir)) throw new Exception($"python-library-directory does not exist: {libDir}");
        }

        if (!PythonEngine.IsInitialized) {
            PythonEngine.DebugGIL = true;
            Runtime.PythonDLL = pythonDLL;
            PythonEngine.Initialize();
        }

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string filePath = Path.Combine(baseDir, "Adapter_Python/FastISO.py");
        string header = File.ReadAllText(filePath, Encoding.UTF8);

        using (Py.GIL()) {

            try {
                dynamic sys = Py.Import("sys");
                sys.stdout.reconfigure(line_buffering: true);
                sys.stderr.reconfigure(line_buffering: true);
            }
            catch (Exception) { 
                Console.Error.WriteLine("Failed to reconfigure Python stdout for line_buffereing");
            }

            if (absoluteLibDirs.Length > 0) {
                dynamic sys = Py.Import("sys");
                PyObject pyObj = sys.path;
                var sysPath = pyObj.As<string[]>().ToHashSet();
                foreach (string dir in absoluteLibDirs) {
                    if (!sysPath.Contains(dir)) {
                        sys.path.append(dir);
                    }
                }
            }

            moduleOuter = PyModule.FromString(name: "fastimports", code: header);
            module = moduleOuter.NewScope();
            module.Exec(code);

            inputs = GetIdentifiableMembers<InputBase>(module, "").ToArray();
            outputs = GetIdentifiableMembers<OutputBase>(module, "").ToArray();
            states = GetIdentifiableMembers<AbstractState>(module, "").ToArray();

            var eventProviders = GetEventProviderMembers(module);
            foreach (EventProvider provider in eventProviders) {
                provider.EventSinkRef = this;
            }
            
            PyObject stepWrap = GetAttrOrNull(moduleOuter, "_wrapStepCall") ?? throw new Exception("Helper function _wrapStepCall not found");
            PyObject stepMethod = GetAttrOrNull(module, "step") ?? throw new Exception("Python script must contain 'def step(t, dt):'");

            stepAction = (t, dt) => {
                using (Py.GIL()) {

                    PyObject pyT = (t.JavaTicks/1000.0).ToPython();
                    PyObject pyDT = dt.TotalSeconds.ToPython();
                    Console.WriteLine($"Python step({t}, {dt})...");

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    try {
                        stepWrap.Invoke(stepMethod, pyT, pyDT);
                    }
                    catch (PythonException ex) {
                        PyType type = ex.Type;
                        string name = type.Name;
                        string msg = ex.Message;
                        string stackTrace = ex.StackTrace;
                        throw new Exception($"{name}: {msg}, {FirstLine(stackTrace).Trim()}");
                    }
                    sw.Stop();
                    Console.WriteLine($"Python step({t}, {dt}) completed in {sw.ElapsedMilliseconds} ms");
                    Console.WriteLine();
                }
            };

            foreach (StateValue v in parameter.LastState) {
                AbstractState? state = states.FirstOrDefault(s => s.ID == v.StateID);
                state?.SetValueFromDataValue(v.Value);
            }

            PyObject? initializeMethod = GetAttrOrNull(module, "initialize");
            if (initializeMethod != null) {
                PyObject pyParameter = parameter.ToPython();
                initializeMethod.Invoke(pyParameter);
            }

            PyObject? shutdownMethod = GetAttrOrNull(module, "shutdown");
            if (shutdownMethod != null) {
                shutdownAction = DoShutdown;
            }
        }

        foreach (InputBase input in inputs) {
            input.connectionGetter = retriever;
        }

        return new InitResult() {
            Inputs = inputs.Select(MakeInputDef).ToArray(),
            Outputs = outputs.Select(MakeOutputDef).ToArray(),
            States = states.Select(MakeStateDef).ToArray(),
            ExternalStatePersistence = true
        };
    }

    private static PyObject? GetAttrOrNull(PyObject obj, string name) {
        try {
            return obj.GetAttr(name);
        }
        catch (Exception) {
            return null;
        }
    }

    private static string FirstLine(string s) {
        int pos = s.IndexOfAny(new char[] { '\r', '\n' });
        if (pos >= 0) {
            return s.Substring(0, pos);
        }
        else {
            return s;
        }
    }

    private void DoShutdown() {
        using (Py.GIL()) {
            module?.InvokeMethod("shutdown");
        }
    }

    public void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo) {
        callback?.Notify_AlarmOrEvent(eventInfo);
    }

    public override Task Shutdown() {

        try {
            shutdownAction();
        } catch (Exception exp) {
            Console.Error.WriteLine("shutdownAction: " + exp.Message);
        }

        try {
            PythonEngine.Shutdown();
        }
        catch (Exception exp) {
            Console.Error.WriteLine("PythonEngine.Shutdown: " + exp.Message);
        }

        return Task.FromResult(true);
    }

    public override Task<StepResult> Step(Timestamp t, Duration dt, InputValue[] inputValues) {

        foreach (InputValue v in inputValues) {
            InputBase? input = inputs.FirstOrDefault(inn => inn.ID == v.InputID);
            if (input != null) {
                input.VTQ = v.Value;
                input.AttachedVariable = v.AttachedVariable;
            }
        }

        foreach (var output in outputs) {
            output.VTQ = VTQ.Make(DataValue.Empty, t, Quality.Good);
            output.ValueHasBeenAssigned = false;
        }

        stepAction(t, dt);

        StateValue[] resStates = states.Select(kv => new StateValue() {
            StateID = kv.ID,
            Value = kv.GetValue()
        }).ToArray();

        var outputValues = new List<OutputValue>(outputs.Length);
        foreach (OutputBase output in outputs) {
            if (output.ValueHasBeenAssigned) {
                outputValues.Add(new OutputValue() {
                    OutputID = output.ID,
                    Value = output.VTQ
                });
            }
        }

        var stepRes = new StepResult() {
            Output = outputValues.ToArray(),
            State = resStates,
        };

        return Task.FromResult(stepRes);
    }

    record MemberInfo(string Name, PyObject Value) {

        public bool TryConvertTo<T>(out T? result) where T : class {
            try {
                result = Value.As<T>();
                return true;
            }
            catch (Exception) {
                result = null;
                return false;
            }
        }
    }

    private static List<T> GetIdentifiableMembers<T>(PyObject obj, string idChain) where T : class, Identifiable {
        List<T> result = new();
        MemberInfo[] fields = GetPyObjectMember(obj);
        foreach (MemberInfo f in fields) {
            string id = f.Name;
            bool isIdentifiable = f.TryConvertTo(out Identifiable? identifiable);
            if (isIdentifiable && identifiable is T x) {
                x.ID = idChain + id;
                x.Name = idChain + x.Name;
                result.Add(x);
            }
            else if (!isIdentifiable) {
                result.AddRange(GetIdentifiableMembers<T>(f.Value, idChain + id + "."));
            }
        }
        return result;
    }

    private static List<EventProvider> GetEventProviderMembers(PyObject obj) {
        List<EventProvider> result = new();
        MemberInfo[] fields = GetPyObjectMember(obj);
        foreach (MemberInfo f in fields) {
            bool isIdentifiable = f.TryConvertTo(out Identifiable? _);
            bool isEventProvider = f.TryConvertTo(out EventProvider? x);
            if (isEventProvider) {
                result.Add(x!);
            }
            else if (!isIdentifiable) {
                result.AddRange(GetEventProviderMembers(f.Value));
            }
        }
        return result;
    }

    static MemberInfo[] GetPyObjectMember(PyObject obj) {
        var results = new List<MemberInfo>();
        var members = new PyList(obj.Dir());
        foreach (PyObject member in members) {
            string name = member.ToString()!;
            if (name.StartsWith("__")) continue;
            try {
                PyObject value = obj.GetAttr(name);
                string type = value.GetPythonType().Name;
                if (type.Length == 0 || !char.IsUpper(type[0])) continue;
                if (type == "NoneType") continue;
                if (type == "CLRMetatype") continue;
                results.Add(new MemberInfo(name, value));
            }
            catch (Exception) { }
        }
        return results.ToArray();
    }

    //private static List<T> GetMembers<T>(object obj, bool recursive) where T : class {
    //    List<T> result = new List<T>();
    //    FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
    //    foreach (FieldInfo f in fields) {
    //        object? fieldValue = f.GetValue(obj);
    //        if (fieldValue is T x) {
    //            result.Add(x);
    //        }
    //        else if (recursive && f.FieldType.IsClass && fieldValue != null) {
    //            result.AddRange(GetMembers<T>(fieldValue, recursive));
    //        }
    //    }
    //    return result;
    //}

    private static InputDef MakeInputDef(InputBase m) {
        return new InputDef() {
            ID = m.ID,
            Name = m.Name,
            Description = m.Name,
            Unit = m.Unit,
            Dimension = m.Dimension,
            Type = m.Type,
            DefaultValue = m.GetDefaultValue()
        };
    }

    private static StateDef MakeStateDef(AbstractState m) {
        return new StateDef() {
            ID = m.ID,
            Name = m.Name,
            Description = m.Name,
            Unit = m.Unit,
            Dimension = m.GetDimension(),
            Type = m.GetDataType(),
            DefaultValue = m.GetDefaultValue()
        };
    }

    private static OutputDef MakeOutputDef(OutputBase m) {
        return new OutputDef() {
            ID = m.ID,
            Name = m.Name,
            Description = m.Name,
            Unit = m.Unit,
            Dimension = m.Dimension,
            Type = m.Type,
        };
    }
}
