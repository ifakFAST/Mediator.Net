// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using ObjectRefs = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectRef>;
using HistoryChanges = System.Collections.Generic.List<Ifak.Fast.Mediator.HistoryChange>;
using AlarmOrEvents = System.Collections.Generic.List<Ifak.Fast.Mediator.AlarmOrEvent>;

namespace Ifak.Fast.Mediator {

    public sealed class ConnectionWrapper {

        private readonly ModuleInitInfo info;

        private Connection? client = null;

        public ConnectionWrapper(ModuleInitInfo info) {
            this.info = info;
        }

        public Func<Connection, Task>?                 OnConnectionCreated { get; set; } = null;
        public Func<Connection, ObjectRefs, Task>?     OnConfigurationChanged { get; set; } = null;
        public Func<Connection, VariableValues, Task>? OnVariableValueChanged { get; set; } = null;
        public Func<Connection, HistoryChanges, Task>? OnVariableHistoryChanged { get; set; } = null;
        public Func<Connection, AlarmOrEvents, Task>?  OnAlarmOrEvents { get; set; } = null;

        public Task Close() {
            Connection? client = this.client;
            if (client != null) {
                this.client = null;
                return client.Close();
            }
            else {
                return Task.FromResult(true);
            }
        }

        public async Task<Connection> EnsureConnection() {

            if (client != null && !client.IsClosed) {
                try {
                    await client.Ping();
                    return client;
                }
                catch (Exception) {
                    Task _ = client.Close();
                    client = null;
                    return await EnsureConnection();
                }
            }

            try {
                bool hasEvents = OnConfigurationChanged != null || OnVariableValueChanged != null || OnVariableHistoryChanged != null || OnAlarmOrEvents != null;
                MyEventListener? listener = hasEvents ? new MyEventListener(OnConfigurationChanged, OnVariableValueChanged, OnVariableHistoryChanged, OnAlarmOrEvents) : null;
                client = await HttpConnection.ConnectWithModuleLogin(info, listener);
                if (listener != null) {
                    listener.Client = client;
                }
                if (OnConnectionCreated != null) {
                    await OnConnectionCreated(client);
                }
                return client;
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                string msg = $"Failed ifakFAST connection: {e.GetType().FullName} {e.Message}";
                if (!e.Message.Contains("request because system is shutting down")) {
                    Console.Error.WriteLine(msg);
                }
                //throw new Exception(msg);
                return new ClosedConnection();
            }
        }

        private sealed class MyEventListener : EventListener {

            public Connection? Client = null;

            private readonly Func<Connection, ObjectRefs, Task> onConfigChanged;
            private readonly Func<Connection, VariableValues, Task> onVariableValueChanged;
            private readonly Func<Connection, HistoryChanges, Task> onVariableHistoryChanged;
            private readonly Func<Connection, AlarmOrEvents, Task> onAlarmOrEvents;

            public MyEventListener(
                Func<Connection, ObjectRefs, Task>? onConfigChanged, 
                Func<Connection, VariableValues, Task>? onVariableValueChanged,
                Func<Connection, HistoryChanges, Task>? onVariableHistoryChanged,
                Func<Connection, AlarmOrEvents, Task>? onAlarmOrEvents) {

                this.onConfigChanged          = onConfigChanged ?? ((_, _) => { return Task.FromResult(true); });
                this.onVariableValueChanged   = onVariableValueChanged ?? ((_, _) => { return Task.FromResult(true); });
                this.onVariableHistoryChanged = onVariableHistoryChanged ?? ((_, _) => { return Task.FromResult(true); });
                this.onAlarmOrEvents          = onAlarmOrEvents ?? ((_, _) => { return Task.FromResult(true); });
            }

            public Task OnAlarmOrEvents(AlarmOrEvents alarmOrEvents) {
                return onAlarmOrEvents(Client!, alarmOrEvents);
            }

            public Task OnConfigChanged(ObjectRefs changedObjects) {
                return onConfigChanged(Client!, changedObjects);
            }

            public Task OnVariableValueChanged(VariableValues variables) {
                return onVariableValueChanged(Client!, variables);
            }

            public Task OnVariableHistoryChanged(HistoryChanges changes) {
                return onVariableHistoryChanged(Client!, changes);
            }

            public Task OnConnectionClosed() {
                return Task.FromResult(true);
            }
        }
    }
}