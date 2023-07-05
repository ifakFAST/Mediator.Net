// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;

namespace Ifak.Fast.Mediator.Publish; 

internal class Util {

    private sealed class MyEventListener : EventListener {

        public Connection? Client = null;

        private readonly Action onConfigChanged;

        public MyEventListener(Action? onConfigChanged) {
            this.onConfigChanged = onConfigChanged ?? (() => { });
        }

        public Task OnAlarmOrEvents(List<AlarmOrEvent> alarmOrEvents) {
            return Task.FromResult(true);
        }

        public Task OnConfigChanged(List<ObjectRef> changedObjects) {
            onConfigChanged();
            return Task.FromResult(true);
        }

        public Task OnConnectionClosed() {
            return Task.FromResult(true);
        }

        public Task OnVariableHistoryChanged(List<HistoryChange> changes) {
            return Task.FromResult(true);
        }

        public Task OnVariableValueChanged(VariableValues variables) {
            return Task.FromResult(true);
        }
    }

    public static async Task<Connection> EnsureConnectOrThrow(
        ModuleInitInfo info, 
        Connection? client, 
        Action? onConfigChanged = null,
        string? moduleID = null) {

        if (client != null && !client.IsClosed) {
            try {
                await client.Ping();
                return client;
            }
            catch (Exception) {
                Task _ = client.Close();
                return await EnsureConnectOrThrow(info, client: null, onConfigChanged, moduleID);
            }
        }

        try {
            bool hasEvents = onConfigChanged != null;
            MyEventListener? listener = hasEvents ? new MyEventListener(onConfigChanged) : null;
            Connection con = await HttpConnection.ConnectWithModuleLogin(info, listener);
            if (listener != null) {
                listener.Client = con;
            }
            if (onConfigChanged != null && moduleID != null) {
                var objRoot = await con.GetRootObject(moduleID);
                await con.EnableConfigChangedEvents(objRoot.ID);
            }
            return con;
        }
        catch (Exception exp) {
            Exception e = exp.GetBaseException() ?? exp;
            string msg = $"Failed ifakFAST connection: {e.GetType().FullName} {e.Message}";
            if (!e.Message.Contains("request because system is shutting down")) {
                Console.Error.WriteLine(msg);
            }
            throw new Exception(msg);
        }
    }

    public static int MapQualityToNumber(Quality q) {
        return q switch {
            Quality.Good => 1,
            Quality.Bad => 0,
            Quality.Uncertain => 2,
            _ => 0,
        };
    }

    public record FilterCriteria(
        bool SimpleTagsOnly, 
        bool NumericTagsOnly,
        bool SendTagsWithNull, 
        bool RemoveEmptyTimestamp
     );

    public static VariableValues Filter(VariableValues values, FilterCriteria criteria) {

        bool numericOnly = criteria.NumericTagsOnly;
        bool simpleOnly = criteria.SimpleTagsOnly || criteria.NumericTagsOnly;
        bool sendNull = criteria.SendTagsWithNull;
        bool removeEmptyTimestamp = criteria.RemoveEmptyTimestamp;

        if (!simpleOnly && sendNull) {
            return removeEmptyTimestamp ? RemoveEmptyTimestamp(values) : values;
        }

        var res = new VariableValues(values.Count);
        foreach (var vv in values) {

            DataValue v = vv.Value.V;

            if (simpleOnly && !sendNull) {
                if (!v.IsArray && !v.IsObject && v.NonEmpty) {
                    if (!numericOnly || v.AsDouble().HasValue) {
                        res.Add(vv);
                    }
                }
            }
            else if (simpleOnly && sendNull) {
                if (!v.IsArray && !v.IsObject) {
                    if (!numericOnly || v.IsEmpty || v.AsDouble().HasValue) {
                        res.Add(vv);
                    }
                }
            }
            else if (!simpleOnly && !sendNull) {
                if (v.NonEmpty) {
                    res.Add(vv);
                }
            }
        }
        return removeEmptyTimestamp ? RemoveEmptyTimestamp(res) : res;
    }

    public static VariableValues RemoveEmptyTimestamp(VariableValues values) {
        if (values.All(vv => vv.Value.T.NonEmpty)) {
            return values;
        }
        return values.Where(vv => vv.Value.T.NonEmpty).ToList();
    }
}
