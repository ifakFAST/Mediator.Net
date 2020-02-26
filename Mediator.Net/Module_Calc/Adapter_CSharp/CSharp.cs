// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp
{
    [Identify("CSharp")]
    public class CSharp : CalculationBase
    {
        private Input[] inputs = new Input[0];
        private Output[] outputs = new Output[0];
        private Action runAction = () => { };

        public override async Task<InitResult> Initialize(InitParameter parameter, AdapterCallback callback) {

            string code = parameter.Calculation.Definition;

            if (!string.IsNullOrWhiteSpace(code)) {

                var options = ScriptOptions.Default
                    .WithImports("Ifak.Fast.Mediator.Calc.Adapter_CSharp")
                    .WithReferences(typeof(Input).Assembly);

                const string className = "ScriptContainer";
                string codeClass = WrapCodeInClass(code, className);

                var script = CSharpScript.
                    Create<object>(codeClass, options).
                    ContinueWith($"new {className}()");

                ScriptState<Object> state = await script.RunAsync();
                object obj = state.ReturnValue;

                Type type = obj.GetType();
                FieldInfo[] fields = type.GetFields();

                inputs = GetMembers<Input>(obj, fields);
                outputs = GetMembers<Output>(obj, fields);

                MethodInfo run = type.GetMethod("Run");
                if (run == null) throw new Exception("No Run() method found.");

                runAction = (Action)run.CreateDelegate(typeof(Action), obj);

                return new InitResult() {
                    Inputs = inputs.Select(MakeInputDef).ToArray(),
                    Outputs = outputs.Select(MakeOutputDef).ToArray(),
                    States = new StateDef[0],
                    ExternalStatePersistence = true
                };

            }
            else {

                return new InitResult() {
                    Inputs = new InputDef[0],
                    Outputs= new OutputDef[0],
                    States = new StateDef[0],
                    ExternalStatePersistence = true
                };
            }
        }

        public override Task Shutdown() {
            return Task.FromResult(true);
        }

        public override Task<StepResult> Step(Timestamp t, InputValue[] inputValues) {

            foreach (InputValue v in inputValues) {
                Input input = inputs.FirstOrDefault(inn => inn.Name == v.InputID);
                if (input != null) {
                    double? value = v.Value.V.AsDouble();
                    input.Value = value.HasValue ? value.Value : 0;
                }
            }

            runAction();

            OutputValue[] result = outputs.Select(kv => new OutputValue() {
                OutputID = kv.Name,
                Value = VTQ.Make(kv.Value, t, Quality.Good)
            }).ToArray();

            var stepRes = new StepResult() {
                Output = result
            };

            return Task.FromResult(stepRes);
        }

        private static string WrapCodeInClass(string code, string className) {
            var sb = new StringBuilder();
            sb.Append("public class ");
            sb.Append(className);
            sb.AppendLine(" {");
            sb.AppendLine(code);
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static T[] GetMembers<T>(object obj, FieldInfo[] fields) where T : class {
            Type t = typeof(T);
            return fields.Where(f => f.FieldType == t)
                .Select(f => f.GetValue(obj) as T)
                .ToArray();
        }

        private static InputDef MakeInputDef(Input m) {
            return new InputDef() {
                ID = m.Name,
                Name = m.Name,
                Description = m.Name,
                Unit = m.Unit,
                Dimension = 1,
                Type = DataType.Float64,
                DefaultValue = DataValue.FromDouble(m.DefaultValue)
            };
        }

        private static OutputDef MakeOutputDef(Output m) {
            return new OutputDef() {
                ID = m.Name,
                Name = m.Name,
                Description = m.Name,
                Unit = m.Unit,
                Dimension = 1,
                Type = DataType.Float64,
            };
        }
    }

    public class Input
    {
        public string Name { get; private set; }
        public string Unit { get; private set; }
        public double DefaultValue { get; private set; }
        public double Value { get; internal set; }

        public Input(string name, string unit, double defaultValue) {
            Name = name;
            Unit = unit;
            DefaultValue = defaultValue;
            Value = 12;
        }
    }

    public class Output
    {
        public string Name { get; private set; }
        public string Unit { get; private set; }
        public double Value { get; set; }

        public Output(string name, string unit) {
            Name = name;
            Unit = unit;
        }
    }
}
