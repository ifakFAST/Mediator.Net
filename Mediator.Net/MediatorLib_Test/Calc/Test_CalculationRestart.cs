// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Calc;
using Xunit;
using CalcModule = Ifak.Fast.Mediator.Calc.Module;
using Config = Ifak.Fast.Mediator.Calc.Config;

namespace MediatorLib_Test.Calc;

// Restarting a calculation whose step is still running: the restart must wait for the step
// to return, never start a replacement that overlaps the old instance, and must not resurrect
// a calculation that was removed while waiting.
public class Test_CalculationRestart
{
    private static TaskCompletionSource<T> Promise<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Bounded(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5));
    private static Task<T> Bounded<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(5));

    [Fact]
    public void Restart_waits_for_running_step_and_never_overlaps() => SingleThreadedAsync.Run(async () => {
        var h = new Harness();
        await h.Start();
        var old = h.Current;
        await h.Trigger();

        Task restart = h.Restart();
        await Task.Delay(300);

        // Still waiting: no replacement has been created while the step is running.
        Assert.False(restart.IsCompleted);
        Assert.Single(FakeCalculation.Created);
        Assert.Equal(State.ShutdownStarted, h.Adapter.State);
        Assert.True(h.Adapter.IsStepRunning);
        Assert.Equal(0, old.Shutdowns);

        old.Result.SetResult(new StepResult {
            State = [new StateValue { StateID = "final", Value = DataValue.FromInt(7) }]
        });
        await Bounded(restart);

        var replacement = h.Current;
        Assert.Equal(2, FakeCalculation.Created.Count);
        Assert.NotSame(old, replacement);
        Assert.Equal(1, old.Steps);
        Assert.Equal(1, old.Shutdowns);
        Assert.Equal(State.Running, h.Adapter.State);
        Assert.False(h.Adapter.IsRestarting);
        Assert.False(h.Adapter.IsStepRunning);
        Assert.Same(replacement, GetWrapped(h.Adapter.Instance));

        // The final step of the old instance is a regular step: its results are kept.
        Assert.Single(h.Adapter.LastStateValues);
        Assert.NotNull(h.Adapter.LastRunTimestamp);

        // Only the replacement's loop is driven by triggers now.
        await h.Trigger();
        Assert.Equal(1, replacement.Steps);
        Assert.Equal(1, old.Steps);

        await h.Finish();
    });

    [Fact]
    public void Removal_during_pending_restart_does_not_resurrect() => SingleThreadedAsync.Run(async () => {
        var h = new Harness();
        await h.Start();
        var old = h.Current;
        await h.Trigger();

        Task restart = h.Restart();
        await Task.Delay(100);
        Assert.False(restart.IsCompleted);

        // Config change removes the calculation while its restart is waiting for the step.
        h.Unregister(h.Adapter);

        old.Result.SetResult(new StepResult());
        await Bounded(restart);

        Assert.Single(FakeCalculation.Created);
        Assert.Equal(1, old.Shutdowns);
        Assert.Null(h.Adapter.Instance);
        Assert.Equal(State.ShutdownCompleted, h.Adapter.State);
        Assert.False(h.Adapter.IsRestarting);
    });

    [Fact]
    public void Module_shutdown_during_pending_restart_does_not_resurrect() => SingleThreadedAsync.Run(async () => {
        var h = new Harness();
        await h.Start();
        var old = h.Current;
        await h.Trigger();

        Task restart = h.Restart();
        await Task.Delay(100);
        Assert.False(restart.IsCompleted);

        Task shutdown = h.Call("Shutdown");

        old.Result.SetResult(new StepResult());
        await Bounded(restart);
        await Bounded(shutdown);

        Assert.Single(FakeCalculation.Created);
        Assert.Equal(1, old.Shutdowns);
        Assert.Null(h.Adapter.Instance);
        Assert.False(h.Adapter.IsRestarting);
    });

    [Fact]
    public void Restart_of_removed_calculation_is_ignored() => SingleThreadedAsync.Run(async () => {
        var h = new Harness();
        await h.Start();
        var old = h.Current;
        await h.Trigger();

        h.Unregister(h.Adapter);
        Task restart = h.Restart();
        Assert.True(restart.IsCompleted);

        Assert.Single(FakeCalculation.Created);
        Assert.Equal(State.Running, h.Adapter.State);
        Assert.False(h.Adapter.IsRestarting);

        old.Result.SetResult(new StepResult());
        await Bounded(h.Stop());
        Assert.Equal(1, old.Shutdowns);
    });

    [Fact]
    public void Slow_shutdown_is_reported_once_and_restart_still_completes() => SingleThreadedAsync.Run(async () => {
        var h = new Harness();
        h.Module.SlowShutdownWarnDelay = TimeSpan.FromMilliseconds(20);
        await h.Start();
        var old = h.Current;
        await h.Trigger();

        Task restart = h.Restart();
        await Task.Delay(300);
        Assert.False(restart.IsCompleted);
        Assert.Single(h.Events.Alarms, a => a.Type == "CalcShutdownSlow");

        old.Result.SetResult(new StepResult());
        await Bounded(restart);

        Assert.Equal(2, FakeCalculation.Created.Count);
        Assert.Equal(1, old.Shutdowns);
        Assert.Single(h.Events.Alarms, a => a.Type == "CalcShutdownSlow");
        Assert.Equal(State.Running, h.Adapter.State);

        await h.Finish();
    });

    [Fact]
    public void Fast_shutdown_is_not_reported() => SingleThreadedAsync.Run(async () => {
        var h = new Harness();
        await h.Start();
        var old = h.Current;
        await h.Trigger();

        Task restart = h.Restart();
        old.Result.SetResult(new StepResult());
        await Bounded(restart);

        Assert.DoesNotContain(h.Events.Alarms, a => a.Type == "CalcShutdownSlow");
        Assert.Equal(2, FakeCalculation.Created.Count);

        await h.Finish();
    });

    private static CalculationBase GetWrapped(SingleThreadCalculation instance) {
        FieldInfo field = typeof(SingleThreadCalculation).GetField("adapter", BindingFlags.Instance | BindingFlags.NonPublic);
        return (CalculationBase)field.GetValue(instance);
    }

    public sealed class FakeCalculation : CalculationBase
    {
        internal static readonly List<FakeCalculation> Created = [];
        internal readonly TaskCompletionSource<bool> Entered = Promise<bool>();
        internal readonly TaskCompletionSource<StepResult> Result = Promise<StepResult>();
        internal int Steps, Shutdowns;
        public FakeCalculation() => Created.Add(this);
        public override Task<InitResult> Initialize(InitParameter parameter, AdapterCallback callback) => Task.FromResult(new InitResult());
        public override Task<StepResult> Step(Timestamp t, Duration dt, InputValue[] inputs) {
            ++Steps;
            Entered.TrySetResult(true);
            return Result.Task;
        }
        public override Task Shutdown() { ++Shutdowns; return Task.CompletedTask; }
    }

    private sealed class Harness
    {
        internal readonly CalcModule Module = new();
        internal readonly CalcInstance Adapter;
        internal readonly RecordingNotifier Events = new();
        private readonly List<CalcInstance> adapters;
        internal FakeCalculation Current => FakeCalculation.Created.Last();

        internal Harness() {
            FakeCalculation.Created.Clear();
            Adapter = new CalcInstance(new Config.Calculation {
                ID = "calc", Name = "Calculation", Type = "Fake", Enabled = true, Definition = "fake",
                RunMode = Config.RunMode.Triggered, Cycle = Duration.FromSeconds(60),
                InitialStartTime = Timestamp.FromJavaTicks(0),
            }, "test");
            adapters = (List<CalcInstance>)Field("adapters").GetValue(Module);
            adapters.Add(Adapter);
            var types = (Dictionary<string, Type>)Field("mapAdapterTypes").GetValue(Module);
            types.Add("Fake", typeof(FakeCalculation));
            Field("notifier").SetValue(Module, Events);
            Field("moduleID").SetValue(Module, "test");
            Field("connection").SetValue(Module, new FakeConnection());
            Field("moduleThread").SetValue(Module, new InlineThread());
            Adapter.CreateInstance(types, new ModuleInitInfo());
        }

        internal void Unregister(CalcInstance instance) => adapters.Remove(instance);

        private static FieldInfo Field(string name) {
            for (var type = typeof(CalcModule); type != null; type = type.BaseType) {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new Exception(name);
        }

        internal Task Call(string method, params object[] args) =>
            (Task)typeof(CalcModule).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Module, args);

        internal async Task Start() {
            var contextType = typeof(CalcModule).GetNestedType("InitContext", BindingFlags.NonPublic);
            await Call("InitAdapter", Adapter, Enum.Parse(contextType, "ConfigChanged"));
            typeof(CalcModule).GetMethod("StartRunLoopTaskIfInitCompleted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Module, [Adapter]);
            Assert.Equal(State.Running, Adapter.State);
        }

        internal async Task Trigger() {
            Adapter.Triggered_t = Timestamp.Now;
            await Bounded(Current.Entered.Task);
        }

        internal Task Restart() {
            Adapter.CalcConfig.Cycle = Duration.FromSeconds(61);
            return Call("RestartAdapter", Adapter, "Config changed", false, 0);
        }

        internal Task Stop() => Call("ShutdownAdapter", Adapter);

        internal async Task Finish() {
            Task shutdown = Call("Shutdown");
            foreach (var instance in FakeCalculation.Created) instance.Result.TrySetResult(new StepResult());
            await Bounded(shutdown);
        }
    }

    private sealed class FakeConnection : ClosedConnection
    {
        public override Task<List<VTQ>> ReadVariables(List<VariableRef> variables) => Task.FromResult(new List<VTQ>());
        public override Task<WriteResult> WriteVariablesIgnoreMissing(List<VariableValue> values) => Task.FromResult(WriteResult.OK);
    }

    private sealed class RecordingNotifier : Notifier
    {
        internal readonly List<VariableValue> Values = [];
        internal readonly List<AlarmOrEventInfo> Alarms = [];
        public void Notify_VariableValuesChanged(List<VariableValue> values) => Values.AddRange(values);
        public void Notify_ConfigChanged(List<ObjectRef> objects) { }
        public void Notify_AlarmOrEvent(AlarmOrEventInfo info) => Alarms.Add(info);
    }

    private sealed class InlineThread : ModuleThread
    {
        public void Post(Action action) => action();
        public void Post<T>(Action<T> action, T value) => action(value);
        public void Post<T1, T2>(Action<T1, T2> action, T1 a, T2 b) => action(a, b);
        public void Post<T1, T2, T3>(Action<T1, T2, T3> action, T1 a, T2 b, T3 c) => action(a, b, c);
    }
}
