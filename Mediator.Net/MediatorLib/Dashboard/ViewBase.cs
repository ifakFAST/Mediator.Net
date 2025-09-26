﻿// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard
{
    public interface ViewContext
    {
        Task SendEventToUI(string eventName, object payload);
        Task SaveViewConfiguration(DataValue newConfig);
        Task<string> SaveWebAsset(string fileExtension, byte[] data);
        void SetGetRequestMapping(string path, string directory);
    }

    public class EmptyViewContext : ViewContext
    {
        public Task SaveViewConfiguration(DataValue newConfig) {
            throw new InvalidOperationException("SaveViewConfiguration on empty ViewContext");
        }

        public Task SendEventToUI(string eventName, object payload) {
            throw new InvalidOperationException("SendEventToUI on empty ViewContext");
        }

        public Task<string> SaveWebAsset(string fileExtension, byte[] data) {
            throw new InvalidOperationException("SaveWebAsset on empty ViewContext");
        }

        public void SetGetRequestMapping(string path, string directory) {
            throw new InvalidOperationException("SetGetRequestMapping on empty ViewContext");
        }
    }

    public abstract class ViewBase : EventListener
    {
        public ObjectRef ID { get; set; }
        public DataValue Config { get; set; }
        protected Connection Connection { get; set; } = new ClosedConnection();
        protected ViewContext Context { get; set; } = new EmptyViewContext();

        protected virtual string RequestMethodNamePrefix => "UiReq_";

        protected Dictionary<string, UiReqMethod> mapUiReqMethods = new Dictionary<string, UiReqMethod>();
        public delegate Task<ReqResult> UIReqDelegate(object?[] paras);

        public virtual Task OnInit(Connection connection, ViewContext context, DataValue config) {
            Connection = connection;
            Context = context;
            Config = config;

            Type type = GetType();
            string prefix = RequestMethodNamePrefix;
            int N = prefix.Length;

            MethodInfo[] methods =
                    type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name.StartsWith(prefix))
                    .ToArray();

            foreach (MethodInfo m in methods) {

                ParameterInfo[] parameters = m.GetParameters();

                UIReqDelegate theDelegate = (object?[] args) => {
                    return (Task<ReqResult>)m.Invoke(this, args)!;
                };

                UiReqPara[] uiReqParameters = parameters.Select(MakeParameter).ToArray();

                string key = m.Name.Substring(N);
                mapUiReqMethods[key] = new UiReqMethod(theDelegate, uiReqParameters);
            }

            return Task.FromResult(true);
        }

        private static UiReqPara MakeParameter(ParameterInfo p) {
            return new UiReqPara(
                name: p.Name,
                type: p.ParameterType,
                hasDefaultValue: p.HasDefaultValue,
                defaultValue: p.HasDefaultValue ? p.DefaultValue : null);
        }

        public virtual Task<NaviAugmentation?> GetNaviAugmentation() => Task.FromResult((NaviAugmentation?)null);

        public abstract Task OnActivate();

        public virtual Task OnDeactivate() => Task.FromResult(true);

        public virtual Task OnDestroy() => Task.FromResult(true);

        public virtual async Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters) {

            if (mapUiReqMethods.ContainsKey(command)) {

                UiReqMethod method = mapUiReqMethods[command];
                method.ResetValues();

                JObject obj = StdJson.JObjectFromString(parameters.JSON);
                foreach (JProperty p in obj.Properties()) {
                    if (method.ParameterMap.ContainsKey(p.Name)) {
                        UiReqPara para = method.ParameterMap[p.Name];
                        para.Value = p.Value.ToObject(para.Type);
                    }
                }

                object?[] paramValues = method.Parameters.Select(p => p.Value).ToArray();
                return await method.TheDelegate(paramValues);
            }

            return ReqResult.Bad("Unknown command: " + command);
        }

        public virtual Task OnConfigChanged(List<ObjectRef> changedObjects) { return Task.FromResult(true); }

        public virtual Task OnVariableValueChanged(List<VariableValue> variables) { return Task.FromResult(true); }

        public virtual Task OnVariableHistoryChanged(List<HistoryChange> changes) { return Task.FromResult(true); }

        public virtual Task OnAlarmOrEvents(List<AlarmOrEvent> alarmOrEvents) { return Task.FromResult(true); }

        Task EventListener.OnConnectionClosed() { return Task.FromResult(true); }

        public class UiReqMethod
        {
            public UiReqMethod(UIReqDelegate theDelegate, UiReqPara[] parameters) {
                TheDelegate = theDelegate;
                Parameters = parameters;
                ParameterMap = parameters.ToDictionary(p => p.Name);
            }

            public UIReqDelegate TheDelegate { get; private set; }
            public UiReqPara[] Parameters { get; private set; }
            public Dictionary<string, UiReqPara> ParameterMap { get; private set; }

            public void ResetValues() {
                foreach (var p in Parameters) {
                    p.Value = p.DefaultValue;
                }
            }
        }

        public class UiReqPara
        {
            public string Name { get; private set; }
            public Type Type { get; private set; }
            public bool HasDefaultValue { get; private set; }
            public object? DefaultValue { get; private set; }
            public object? Value { get; set; }

            public UiReqPara(string name, Type type, bool hasDefaultValue, object? defaultValue) {
                Name = name;
                Type = type;
                HasDefaultValue = hasDefaultValue;
                DefaultValue = defaultValue;
            }
        }
    }

    public struct NaviAugmentation
    {
        public string IconColor { get; set; }
    }
}
