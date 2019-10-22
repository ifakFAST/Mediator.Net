// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator;
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

        public virtual Task OnInit(Connection connection, ViewContext context, DataValue config) {
            Connection = connection;
            Context = context;
            Config = config;
            return Task.FromResult(true);
        }

        public abstract Task OnActivate();

        public virtual Task OnDeactivate() => Task.FromResult(true);

        public virtual Task OnDestroy() => Task.FromResult(true);

        public abstract Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters);

        public virtual Task OnConfigChanged(ObjectRef[] changedObjects) { return Task.FromResult(true); }

        public virtual Task OnVariableValueChanged(VariableValue[] variables) { return Task.FromResult(true); }

        public virtual Task OnVariableHistoryChanged(HistoryChange[] changes) { return Task.FromResult(true); }

        public virtual Task OnAlarmOrEvents(AlarmOrEvent[] alarmOrEvents) { return Task.FromResult(true); }

        public virtual Task OnConnectionClosed() { return Task.FromResult(true); }
    }
}
