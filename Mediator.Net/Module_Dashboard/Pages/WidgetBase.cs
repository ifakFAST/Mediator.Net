// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard.Pages
{
    public interface WidgetContext
    {
        Task SendEventToUI(string eventName, object payload);
        Task SaveWidgetConfiguration(object newConfig);
    }

    public class EmptyWidgetContext : WidgetContext
    {
        public Task SaveWidgetConfiguration(object newConfig) {
            throw new InvalidOperationException("SaveViewConfiguration on empty WidgetContext");
        }

        public Task SendEventToUI(string eventName, object payload) {
            throw new InvalidOperationException("SendEventToUI on empty WidgetContext");
        }
    }

    public abstract class WidgetBaseWithConfig<CONFIG> : WidgetBase where CONFIG : class
    {
        private CONFIG? config;

        public CONFIG Config => config!;

        protected override void SetConfig(object config) {
            this.config = (CONFIG)config;
        }

        protected override Type ConfigType => typeof(CONFIG);
    }

    public abstract class WidgetBase: EventListener
    {
        protected string ID { get; private set; } = "";

        protected Connection Connection { get; set; } = new ClosedConnection();
        protected WidgetContext Context { get; set; } = new EmptyWidgetContext();

        public abstract string DefaultHeight { get; }
        public abstract string DefaultWidth { get; }

        protected abstract Type ConfigType { get; }
        protected abstract void SetConfig(object config);

        internal void OnInit(Connection connection, WidgetContext context, Widget widget) {

            ID = widget.ID;

            Connection = connection;
            Context = context;

            object objConfig = widget.Config.ToObject(ConfigType)!;
            widget.Config = StdJson.ObjectToJObject(objConfig);
            SetConfig(objConfig);

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
        }

        public abstract Task OnActivate();

        public virtual Task OnDeactivate() => Task.FromResult(true);

        private static UiReqPara MakeParameter(ParameterInfo p) {
            return new UiReqPara(
                name: p.Name ?? "",
                type: p.ParameterType,
                hasDefaultValue: p.HasDefaultValue,
                defaultValue: p.HasDefaultValue ? p.DefaultValue : null);
        }

        protected virtual string RequestMethodNamePrefix => "UiReq_";

        protected Dictionary<string, UiReqMethod> mapUiReqMethods = new Dictionary<string, UiReqMethod>();
        public delegate Task<ReqResult> UIReqDelegate(object?[] paras);

        public virtual async Task<ReqResult> OnUiRequestAsync(string command, JObject parameters) {

            if (mapUiReqMethods.ContainsKey(command)) {

                UiReqMethod method = mapUiReqMethods[command];
                method.ResetValues();

                foreach (JProperty p in parameters.Properties()) {
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
            public object? DefaultValue { get; set; }
            public object? Value { get; set; }

            public UiReqPara(string name, Type type, bool hasDefaultValue, object? defaultValue) {
                Name = name;
                Type = type;
                HasDefaultValue = hasDefaultValue;
                DefaultValue = defaultValue;
            }
        }

        public virtual Task OnConfigChanged(List<ObjectRef> changedObjects) { return Task.FromResult(true); }

        public virtual Task OnVariableValueChanged(List<VariableValue> variables) { return Task.FromResult(true); }

        public virtual Task OnVariableHistoryChanged(List<HistoryChange> changes) { return Task.FromResult(true); }

        public virtual Task OnAlarmOrEvents(List<AlarmOrEvent> alarmOrEvents) { return Task.FromResult(true); }

        Task EventListener.OnConnectionClosed() { return Task.FromResult(true); }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class IdentifyWidget : Attribute
    {
        public string ID { get; set; }

        public IdentifyWidget(string id) {
            ID = id;
        }
    }
}
