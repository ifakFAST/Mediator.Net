// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard
{
    public class Session : EventListener, ViewContext
    {
        public string ID { get; private set; }
        private WebSocket WebSocket { get; set; }

        private Connection connection;
        private readonly Dictionary<string, ViewBase> views = new Dictionary<string, ViewBase>();
        private string moduleID = "";

        private ViewBase currentView = null;
        private bool closed = false;

        public Timestamp lastActivity = Timestamp.Now;

        public Session() {
            ID = Guid.NewGuid().ToString().Replace("-", "");
        }

        public async Task SetConnection(Connection connection, DashboardModel model, string moduleID, ViewType[] viewTypes) {

            this.connection = connection;
            this.moduleID = moduleID;

            foreach (View view in model.Views) {
                ViewBase viewImpl = CreateViewInstance(view, viewTypes);
                views[view.ID] = viewImpl;
                await viewImpl.OnInit(connection, this, view.Config);
            }
        }

        public bool IsAbandoned => closed || (Timestamp.Now - lastActivity) > Duration.FromMinutes(15);

        public async Task OnActivateView(string viewID) {

            if (!views.ContainsKey(viewID))
                throw new Exception("Unknown viewID " + viewID);

            if (currentView != null) {
                await currentView.OnDeactivate();
                await connection.DisableChangeEvents(true, true, true);
                currentView = null;
            }

            lastActivity = Timestamp.Now;

            ViewBase view = views[viewID];
            await view.OnActivate();
            currentView = view;
        }

        public Task<ReqResult> OnViewCommand(string viewID, string command, DataValue parameters) {

            if (!views.ContainsKey(viewID))
                return Task.FromResult(ReqResult.Bad("Unknown viewID " + viewID));

            ViewBase view = views[viewID];
            if (view != currentView)
                return Task.FromResult(ReqResult.Bad($"View {viewID} is not the active view"));

            lastActivity = Timestamp.Now;

            return view.OnUiRequestAsync(command, parameters);
        }

        private ViewBase CreateViewInstance(View view, ViewType[] viewTypes) {

            ViewType viewType = viewTypes.FirstOrDefault(vt => vt.Name.Equals(view.Type, StringComparison.InvariantCultureIgnoreCase));
            if (viewType == null) throw new Exception($"No view type '{view.Type}' found!");

            object viewObj = Activator.CreateInstance(viewType.Type);
            return (ViewBase)viewObj;
        }

        void EventListener.OnConfigChanged(ObjectRef[] changedObjects) {
            currentView?.OnConfigChanged(changedObjects);
        }

        void EventListener.OnVariableValueChanged(VariableValue[] variables) {
            currentView?.OnVariableValueChanged(variables);
        }

        void EventListener.OnVariableHistoryChanged(HistoryChange[] changes) {
            currentView?.OnVariableHistoryChanged(changes);
        }

        void EventListener.OnAlarmOrEvent(AlarmOrEvent alarmOrEvent) {
            currentView?.OnAlarmOrEvent(alarmOrEvent);
        }

        void EventListener.OnConnectionClosed() {
            currentView?.OnConnectionClosed();
        }

        void ViewContext.SendEventToUI(string eventName, object payload) {
            if (WebSocket == null || WebSocket.State != WebSocketState.Open) return;
            var ignored = SendWebSocket(WebSocket, "{ \"event\": \"" + eventName + "\", \"payload\": ", payload);
        }

        async Task ViewContext.SaveViewConfiguration(DataValue newConfig) {
            ViewBase view = currentView;
            if (view == null) return;
            var entry = views.FirstOrDefault(p => p.Value == view);
            if (entry.Value != view) return;
            string viewID = entry.Key;
            MemberValue member = MemberValue.Make(moduleID, viewID, nameof(View.Config), newConfig);
            await connection.UpdateConfig(member);
            view.Config = newConfig;
        }

        private readonly static Encoding UTF8_NoBOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private async Task SendWebSocket(WebSocket socket, string msgStart, object content) {
            try {
                using (var stream = MemoryManager.GetMemoryStream("Session.SendWebSocket")) {
                    using (var writer = new StreamWriter(stream, UTF8_NoBOM, 1024, leaveOpen: true)) {
                        writer.Write(msgStart);
                        StdJson.ObjectToWriter(content, writer);
                        writer.Write("}");
                    }
                    byte[] bytes = stream.GetBuffer();
                    int count = (int)stream.Length;
                    var segment = new ArraySegment<byte>(bytes, 0, count);
                    await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (Exception exp) {
                logInfo("SendWebSocket:", exp);
            }
        }

        internal async Task Close() {

            if (closed) return;

            closed = true;

            if (WebSocket != null) {
                var socket = WebSocket;
                WebSocket = null;
                try {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
                catch (Exception) { }
            }

            if (currentView != null) {
                try {
                    await currentView.OnDeactivate();
                }
                catch (Exception) { }
                currentView = null;
            }

            foreach  (ViewBase view in views.Values) {
                try {
                    await view.OnDestroy();
                }
                catch (Exception) { }
            }

            await connection.Close();
        }

        private void logInfo(string msg, Exception exp = null) {
            Exception exception = exp != null ? (exp.GetBaseException() ?? exp) : null;
            if (exception != null)
                Console.Out.WriteLine(msg + " " + exception.Message);
            else
                Console.Out.WriteLine(msg);
        }

        public async Task ReadWebSocket(WebSocket socket) {

            if (WebSocket != null) {
                try {
                    Task ignored = WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                catch (Exception) { }
            }

            WebSocket = socket;

            const int maxMessageSize = 1024;
            byte[] receiveBuffer = new byte[maxMessageSize];

            while (!closed) {

                try {

                    WebSocketReceiveResult receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                    if (receiveResult.MessageType == WebSocketMessageType.Close) {
                        await Close();
                        return;
                    }
                    lastActivity = Timestamp.Now;
                }
                catch (Exception) {
                    try {
                        Task ignored = socket.CloseAsync(WebSocketCloseStatus.ProtocolError, string.Empty, CancellationToken.None);
                    }
                    catch (Exception) { }
                    if (socket == WebSocket) {
                        WebSocket = null;
                    }
                    return;
                }
            }
        }
    }
}
