// Licensed to ifak e.V. under one or more agreements.
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
    }

    public abstract class ViewBase : EventListener
    {
        public DataValue Config { get; set; }
        protected Connection Connection { get; set; }
        protected ViewContext Context { get; set; }

        protected virtual string RequestMethodNamePrefix => "UiReq_";

        protected Dictionary<string, UiReqMethod> mapUiReqMethods = new Dictionary<string, UiReqMethod>();
        public delegate Task<ReqResult> UIReqDelegate(object[] paras);

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

                UIReqDelegate theDelegate = (object[] args) => {
                    return (Task<ReqResult>)m.Invoke(this, args);
                };

                UiReqPara[] uiReqParameters = parameters.Select(MakeParameter).ToArray();

                string key = m.Name.Substring(N);
                mapUiReqMethods[key] = new UiReqMethod(theDelegate, uiReqParameters);
            }

            return Task.FromResult(true);
        }

        private static UiReqPara MakeParameter(ParameterInfo p) {
            return new UiReqPara() {
                Name = p.Name,
                Type = p.ParameterType,
                HasDefaultValue = p.HasDefaultValue,
                DefaultValue = p.HasDefaultValue ? p.DefaultValue : null
            };
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

                object[] paramValues = method.Parameters.Select(p => p.Value).ToArray();
                return await method.TheDelegate(paramValues);
            }

            return ReqResult.Bad("Unknown command: " + command);
        }

        public virtual Task OnConfigChanged(ObjectRef[] changedObjects) { return Task.FromResult(true); }

        public virtual Task OnVariableValueChanged(VariableValue[] variables) { return Task.FromResult(true); }

        public virtual Task OnVariableHistoryChanged(HistoryChange[] changes) { return Task.FromResult(true); }

        public virtual Task OnAlarmOrEvents(AlarmOrEvent[] alarmOrEvents) { return Task.FromResult(true); }

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
            public string Name { get; set; }
            public Type Type { get; set; }
            public bool HasDefaultValue { get; set; }
            public object DefaultValue { get; set; }
            public object Value { get; set; }
        }
    }

    public struct NaviAugmentation
    {
        public string IconColor { get; set; }
    }
}
