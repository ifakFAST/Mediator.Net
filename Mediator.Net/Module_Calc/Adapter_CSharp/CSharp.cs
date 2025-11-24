// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp;

[Identify(id: "CSharp", showWindowVisible: false, showDefinition: true, definitionLabel: "Script", definitionIsCode: true, codeLang: "csharp")]
public class CSharp : CalculationBase, EventSink, ConnectionConsumer {
    private InputBase[] inputs = [];
    private OutputBase[] outputs = [];
    private AbstractState[] states = [];
    private Calculation[] calculations = [];
    private Action<Timestamp, Duration> stepAction = (t, dt) => { };
    private Action shutdownAction = () => { };
    private AdapterCallback? callback;

    private List<Api> apis = [];

    private static readonly object handleInitLock = new();

    public override async Task<InitResult> Initialize(InitParameter parameter, AdapterCallback callback) {

        this.callback = callback;
        string code = parameter.Calculation.Definition;

        if (!string.IsNullOrWhiteSpace(code)) {

            // We need to lock the init code in order to prevent concurrent compilation of csharp-libraries!
            bool lockWasTaken = false;
            object lockObj = handleInitLock;
            try {
                Monitor.Enter(lockObj, ref lockWasTaken);
                return await DoInit(parameter, code);
            }
            finally {
                if (lockWasTaken) { Monitor.Exit(lockObj); }
            }
        }
        else {

            return new InitResult() {
                Inputs = new InputDef[0],
                Outputs = new OutputDef[0],
                States = new StateDef[0],
                ExternalStatePersistence = true
            };
        }
    }

    private Func<Task<Connection>> retriever = () => Task.FromResult((Connection)new ClosedConnection());

    public void SetConnectionRetriever(Func<Task<Connection>> retriever) {
        this.retriever = retriever;
    }

    private async Task<InitResult> DoInit(InitParameter parameter, string code) {

        var config = new Mediator.Config(parameter.ModuleConfig);
        string libs = config.GetOptionalString("csharp-libraries", "");
        bool cache = config.GetOptionalBool("csharp-cache-scripts", true);

        string[] assemblies = libs
            .Split(new char[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToArray();

        string[] absoluteAssemblies = assemblies.Select(d => Path.GetFullPath(d)).ToArray();
        foreach (string assembly in absoluteAssemblies) {
            if (!File.Exists(assembly)) throw new Exception($"csharp-library does not exist: {assembly}");
        }

        absoluteAssemblies = absoluteAssemblies.Select(assembly => {
            if (assembly.ToLowerInvariant().EndsWith(".cs")) {
                CompileResult compileRes = CompileLib.CSharpFile2Assembly(assembly);
                Print(compileRes, assembly);
                return compileRes.AssemblyFileName;
            }
            return assembly;
        }).ToArray();

        var referencedAssemblies = new List<Assembly>();

        foreach (string assembly in absoluteAssemblies) {
            try {
                Assembly ass = Assembly.LoadFrom(assembly);
                referencedAssemblies.Add(ass);
            }
            catch (Exception exp) {
                throw new Exception($"Failed to load csharp-library {assembly}: {exp.Message}");
            }
        }

        CodeToObjectBase objMaker;
        if (cache)
            objMaker = new CodeToObjectCompile();
        else
            objMaker = new CodeToObjectScripting();

        object obj = await objMaker.MakeObjectFromCode(parameter.Calculation.Name, code, referencedAssemblies);

        inputs = GetIdentifiableMembers<InputBase>(obj, "", recursive: true, []).ToArray();
        outputs = GetIdentifiableMembers<OutputBase>(obj, "", recursive: true, []).ToArray();
        states = GetIdentifiableMembers<AbstractState>(obj, "", recursive: true, []).ToArray();
        calculations = GetIdentifiableMembers<Calculation>(obj, "", recursive: true, []).ToArray();

        var eventProviders = GetMembers<EventProvider>(obj, recursive: true, []);
        foreach (EventProvider provider in eventProviders) {
            provider.EventSinkRef = this;
        }

        apis = GetMembers<Api>(obj, recursive: true, []);
        foreach (Api api in apis) {
            api.moduleID = parameter.ModuleID;
            api.calculationName = parameter.Calculation.Name;
            api.connectionGetter = retriever;
        }

        Type type = obj.GetType();

        MethodInfo[] methods =
            type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Step" && IsStepSignature(m))
            .ToArray();

        if (methods.Length == 0) throw new Exception("No Step(Timestamp t, TimeSpan dt) method found.");
        MethodInfo step = methods[0];

        stepAction = (Action<Timestamp, Duration>)step.CreateDelegate(typeof(Action<Timestamp, Duration>), obj);

        foreach (StateValue v in parameter.LastState) {
            AbstractState? state = states.FirstOrDefault(s => s.ID == v.StateID);
            if (state != null) {
                state.SetValueFromDataValue(v.Value);
            }
        }

        MethodInfo[] methodsInit =
           type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
           .Where(m => m.Name == "Initialize" && IsInitializeSignature(m))
           .ToArray();

        if (methodsInit.Length == 1) {
            MethodInfo init = methodsInit[0];
            if (init.GetParameters().Length == 0) {
                var initAction = (Action)init.CreateDelegate(typeof(Action), obj);
                initAction();
            }
            else {
                var initAction = (Action<InitParameter>)init.CreateDelegate(typeof(Action<InitParameter>), obj);
                initAction(parameter);
            }
        }

        MethodInfo[] methodsShutdown =
           type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
           .Where(m => m.Name == "Shutdown" && IsInitializeShutdown(m))
           .ToArray();

        if (methodsShutdown.Length == 1) {
            MethodInfo shutdown = methodsShutdown[0];
            shutdownAction = (Action)shutdown.CreateDelegate(typeof(Action), obj);
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

    private static readonly HashSet<string> reportedLibs = new HashSet<string>();

    private void Print(CompileResult compileRes, string fileName) {

        if (reportedLibs.Contains(fileName)) return;
        reportedLibs.Add(fileName);

        string name = Path.GetFileName(fileName);
        string assemblyFileName = Path.GetFileName(compileRes.AssemblyFileName);
        string assemblyDir = Path.GetDirectoryName(compileRes.AssemblyFileName) ?? "";
        var buffer = new StringBuilder();
        if (compileRes.IsUsingCachedAssembly) {
            buffer.AppendLine($"C# lib {name}: Using cached assembly");
            buffer.AppendLine($"   Directory: {assemblyDir}");
            buffer.AppendLine($"   Assembly:  {assemblyFileName}");
            buffer.AppendLine($"   Created:   {File.GetCreationTime(compileRes.AssemblyFileName)}");
        }
        else {
            buffer.AppendLine($"C# lib {name}: Compiled C# source file to cached assembly");
            buffer.AppendLine($"   Directory:   {assemblyDir}");
            buffer.AppendLine($"   Assembly:    {assemblyFileName}");
            buffer.AppendLine($"   CompileTime: {compileRes.CompileTime}");
        }
        Console.Out.WriteLine(buffer.ToString());
        Console.Out.Flush();
    }

    public void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo) {
        callback?.Notify_AlarmOrEvent(eventInfo);
    }

    private static bool IsStepSignature(MethodInfo m) {
        ParameterInfo[] parameters = m.GetParameters();
        if (parameters.Length != 2) return false;
        ParameterInfo p1 = parameters[0];
        ParameterInfo p2 = parameters[1];
        if (p1.ParameterType != typeof(Timestamp)) return false;
        if (p2.ParameterType != typeof(Duration)) return false;
        if (m.ReturnType != typeof(void)) return false;
        return true;
    }

    private static bool IsInitializeSignature(MethodInfo m) {
        ParameterInfo[] parameters = m.GetParameters();
        if (parameters.Length > 1) return false;
        if (parameters.Length == 1) {
            ParameterInfo p1 = parameters[0];
            if (p1.ParameterType != typeof(InitParameter)) return false;
        }            
        if (m.ReturnType != typeof(void)) return false;
        return true;
    }

    private static bool IsInitializeShutdown(MethodInfo m) {
        ParameterInfo[] parameters = m.GetParameters();
        if (parameters.Length != 0) return false;
        if (m.ReturnType != typeof(void)) return false;
        return true;
    }

    public override Task Shutdown() {
        shutdownAction();
        return Task.FromResult(true);
    }

    // Called from a different thread!
    public override void SignalStepAbort() {
        foreach (Api api in apis) {
            api.abortStep = true;
        }
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

        foreach (var calc in calculations) {
            calc.TriggerStep_t = null;
            calc.TriggerStep_dt = null;
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

        var calcSteps = calculations
            .Where(c => c.TriggerStep_t.HasValue)
            .Select(c => new TriggerCalculation() {
                CalcID = c.CalcID,
                TriggerStep_t = c.TriggerStep_t!.Value,
                TriggerStep_dt = c.TriggerStep_dt
            })
            .ToArray();

        var stepRes = new StepResult() {
            Output = outputValues.ToArray(),
            State = resStates,
            TriggeredCalculations = calcSteps
        };

        return Task.FromResult(stepRes);
    }

    internal static List<T> GetIdentifiableMembers<T>(object obj, string idChain, bool recursive, HashSet<object> visited) where T : class, Identifiable {

        if (visited.Contains(obj)) return [];
        visited.Add(obj);

        List<T> result = [];
        FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        foreach (FieldInfo f in fields) {
            string id = f.Name;
            object? fieldValue = f.GetValue(obj);

            if (fieldValue is Delegate) {
                continue;
            }
            if (fieldValue is MethodInfo) {
                continue;
            }
            if (fieldValue is string) {
                continue;
            }

            if (fieldValue is T x) {
                x.ID = idChain + id;
                string name = string.IsNullOrWhiteSpace(x.Name) ? id : x.Name;
                x.Name = idChain + name;
                result.Add(x);
            }
            else if (fieldValue is IReadOnlyCollection<T> arr) {
                for (int i = 0; i < arr.Count; ++i) {
                    T it = arr.ElementAt(i);
                    it.ID   = idChain + id + "." + it.ID;
                    it.Name = idChain + id + "." + it.Name;
                    result.Add(it);
                }
            }
            else if (recursive && f.FieldType.IsClass && fieldValue != null) {
                result.AddRange(GetIdentifiableMembers<T>(fieldValue, idChain + id + ".", recursive, visited));
            }
        }
        return result;
    }

    private static List<T> GetMembers<T>(object obj, bool recursive, HashSet<object> visited) where T : class {

        if (visited.Contains(obj)) return [];
        visited.Add(obj);

        List<T> result = [];
        FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        foreach (FieldInfo f in fields) {
            object? fieldValue = f.GetValue(obj);

            if (fieldValue is Delegate) {
                continue;
            }
            if (fieldValue is MethodInfo) {
                continue;
            }
            if (fieldValue is string) {
                continue;
            }

            if (fieldValue is T x) {
                result.Add(x);
            }
            else if (recursive && f.FieldType.IsClass && fieldValue != null) {
                result.AddRange(GetMembers<T>(fieldValue, recursive, visited));
            }
        }
        return result;
    }

    private static InputDef MakeInputDef(InputBase m) {
        return new InputDef() {
            ID = m.ID,
            Name = m.Name,
            Description = m.Name,
            Unit = m.Unit,
            Dimension = m.Dimension,
            Type = m.Type,
            DefaultValue = m.GetDefaultValue(),
            DefaultVariable = m.GetDefaultVariable()
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
