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
using System.Reflection;

namespace Ifak.Fast.Mediator.Dashboard
{
    public class Session : EventListener, ViewContext
    {
        public string ID { get; private set; }
        private WebSocket? WebSocket { get; set; }

        private Connection connection = new ClosedConnection();
        private readonly Dictionary<string, ViewBase> views = new Dictionary<string, ViewBase>();
        private string moduleID = "";

        private ViewBase? currentView = null;
        private bool closed = false;

        public Timestamp lastActivity = Timestamp.Now;

        private DashboardModel model = new DashboardModel();
        private ViewType[] viewTypes = new ViewType[0];

        public Session() {
            ID = Guid.NewGuid().ToString().Replace("-", "");
        }

        public async Task SetConnection(Connection connection, DashboardModel model, string moduleID, ViewType[] viewTypes) {

            this.model = model;
            this.viewTypes = viewTypes;
            this.connection = connection;
            this.moduleID = moduleID;

            foreach (View view in model.Views) {
                await CreateView(view);
            }

            _ = DoRegularGetNaviAugmentation();
        }

        private async Task DoRegularGetNaviAugmentation() {

            await Task.Delay(5000);

            while (!closed) {

                foreach(var entry in views) {
                    string viewID = entry.Key;
                    ViewBase view = entry.Value;
                    try {
                        NaviAugmentation? aug = await view.GetNaviAugmentation();
                        if (aug.HasValue) {
                            var para = new {
                                viewID,
                                iconColor = aug.Value.IconColor
                            };
                            await SendEventToUI("NaviAugmentation", para);
                        }
                    }
                    catch (Exception exception) {
                        if (closed) {
                            return;
                        }
                        await Task.Delay(2000);
                        if (closed) {
                            return;
                        }
                        Exception exp = exception.GetBaseException();
                        Console.Out.WriteLine($"Exception in GetNaviAugmentation of view {viewID}: {exp.Message}");
                        await Task.Delay(10000);
                    }
                }

                await Task.Delay(2000);
            }
        }

        private async Task CreateView(View view) {
            ViewBase viewImpl = CreateViewInstance(view, viewTypes);
            views[view.ID] = viewImpl;
            await viewImpl.OnInit(connection, this, view.Config);
        }

        private async Task DeactivateCurrentView() {
            if (currentView != null) {
                await currentView.OnDeactivate();
                await connection.DisableChangeEvents(true, true, true);
                currentView = null;
            }
        }

        public bool IsAbandoned => closed || (Timestamp.Now - lastActivity) > Duration.FromMinutes(15);

        public async Task OnActivateView(string viewID) {

            if (!views.ContainsKey(viewID))
                throw new Exception("Unknown viewID " + viewID);

            await DeactivateCurrentView();

            lastActivity = Timestamp.Now;

            ViewBase view = views[viewID];
            await view.OnActivate();
            currentView = view;
        }

        public async Task<string> OnDuplicateView(string viewID) {

            lastActivity = Timestamp.Now;

            View? view = model.Views.Find(v => v.ID == viewID);
            if (view == null)
                throw new Exception("Unknown viewID " + viewID);

            View newView = new View() {
                ID = GernerateID(6),
                Name = view.Name,
                Group = view.Group,
                Config = view.Config,
                Type = view.Type,
            };

            model.Views.Add(newView);
            DataValue dv = DataValue.FromObject(model.Views);
            MemberValue member = MemberValue.Make(moduleID, model.ID, nameof(DashboardModel.Views), dv);
            await connection.UpdateConfig(member);

            await CreateView(newView);

            return newView.ID;
        }

        public async Task<string> OnDuplicateConvertHistoryPlot(string viewID) {

            lastActivity = Timestamp.Now;

            View? view = model.Views.Find(v => v.ID == viewID);
            if (view == null)
                throw new Exception("Unknown viewID " + viewID);

            Identify? identify = typeof(View_HistoryPlots).GetCustomAttribute<Identify>();

            if (identify == null || view.Type != identify.ID)
                throw new Exception("View is not a HistoryPlot!");

            var config = view.Config.Object<View_HistoryPlots.ViewConfig>();
            var newConfig = Pages.ConfigFromHistoryPlot.Convert(config);

            View newView = new View() {
                ID = GernerateID(6),
                Name = view.Name,
                Group = view.Group,
                Config = DataValue.FromObject(newConfig, indented: true),
                Type = typeof(Pages.View).GetCustomAttribute<Identify>()!.ID,
            };

            model.Views.Add(newView);
            DataValue dv = DataValue.FromObject(model.Views);
            MemberValue member = MemberValue.Make(moduleID, model.ID, nameof(DashboardModel.Views), dv);
            await connection.UpdateConfig(member);

            await CreateView(newView);

            return newView.ID;
        }

        public async Task OnDeleteView(string viewID) {

            lastActivity = Timestamp.Now;

            View? view = model.Views.Find(v => v.ID == viewID);
            if (view == null)
                throw new Exception("Unknown viewID " + viewID);

            if (views[viewID] == currentView) {
                await DeactivateCurrentView();
            }

            model.Views.Remove(view);

            ObjectValue objDelete = ObjectValue.Make(moduleID, view.ID, DataValue.Empty);
            await connection.UpdateConfig(objDelete);
        }

        public async Task OnRenameView(string viewID, string newName) {

            lastActivity = Timestamp.Now;

            View? view = model.Views.Find(v => v.ID == viewID);
            if (view == null)
                throw new Exception("Unknown viewID " + viewID);

            view.Name = newName;

            DataValue dv = DataValue.FromObject(view.Name);
            MemberValue member = MemberValue.Make(moduleID, view.ID, nameof(View.Name), dv);
            await connection.UpdateConfig(member);
        }

        public async Task OnMoveView(string viewID, bool up) {

            lastActivity = Timestamp.Now;

            View? view = model.Views.Find(v => v.ID == viewID);
            if (view == null)
                throw new Exception("Unknown viewID " + viewID);

            int i = model.Views.IndexOf(view);
            if (up && i > 0) {
                model.Views[i] = model.Views[i - 1];
                model.Views[i - 1] = view;
            }
            else if (!up && i < model.Views.Count - 1) {
                model.Views[i] = model.Views[i + 1];
                model.Views[i + 1] = view;
            }

            DataValue dv = DataValue.FromObject(model.Views);
            MemberValue member = MemberValue.Make(moduleID, model.ID, nameof(DashboardModel.Views), dv);
            await connection.UpdateConfig(member);
        }

        private static string GernerateID(int len) {
            var builder = new StringBuilder();
            Enumerable
               .Range(65, 26)
                .Select(e => ((char)e).ToString())
                .Concat(Enumerable.Range(97, 26).Select(e => ((char)e).ToString()))
                .Concat(Enumerable.Range(0, 10).Select(e => e.ToString()))
                .OrderBy(e => Guid.NewGuid())
                .Take(len)
                .ToList().ForEach(e => builder.Append(e));
            return builder.ToString();
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

            ViewType? viewType = viewTypes.FirstOrDefault(vt => vt.Name.Equals(view.Type, StringComparison.InvariantCultureIgnoreCase));
            if (viewType == null) throw new Exception($"No view type '{view.Type}' found!");

            object? viewObj = viewType.Type == null ? null : Activator.CreateInstance(viewType.Type);
            if (viewObj == null) throw new Exception($"Failed to create instance of view type '{view.Type}'!");

            return (ViewBase)viewObj;
        }

        async Task EventListener.OnConfigChanged(List<ObjectRef> changedObjects) {
            if (currentView != null && !closed) {
                ViewBase view = currentView;
                try {
                    await currentView.OnConfigChanged(changedObjects);
                }
                catch (Exception exp) {
                    ReportEventException(view, nameof(currentView.OnConfigChanged), exp);
                }
            }
        }

        async Task EventListener.OnVariableValueChanged(List<VariableValue> variables) {
            if (currentView != null && !closed) {
                ViewBase view = currentView;
                try {
                    await currentView.OnVariableValueChanged(variables);
                }
                catch (Exception exp) {
                    ReportEventException(view, nameof(currentView.OnVariableValueChanged), exp);
                }
            }
        }

        async Task EventListener.OnVariableHistoryChanged(List<HistoryChange> changes) {
            if (currentView != null && !closed) {
                ViewBase view = currentView;
                try {
                    await currentView.OnVariableHistoryChanged(changes);
                }
                catch(Exception exp) {
                    ReportEventException(view, nameof(currentView.OnVariableHistoryChanged), exp);
                }
            }
        }

        async Task EventListener.OnAlarmOrEvents(List<AlarmOrEvent> alarmOrEvents) {
            if (currentView != null && !closed) {
                ViewBase view = currentView;
                try {
                    await currentView.OnAlarmOrEvents(alarmOrEvents);
                }
                catch (Exception exp) {
                    ReportEventException(view, nameof(currentView.OnAlarmOrEvents), exp);
                }
            }
        }

        private void ReportEventException(ViewBase view, string eventName, Exception exp) {
            Exception e = exp.GetBaseException() ?? exp;
            if (!(e is ConnectivityException)) {
                string msg = $"{view.GetType().Name}.{eventName}: {e.Message}";
                Console.Error.WriteLine(msg);
            }
        }

        Task EventListener.OnConnectionClosed() {
            _ = Close();
            return Task.FromResult(true);
        }

        public Task SendEventToUI(string eventName, object payload) {
            if (WebSocket == null || WebSocket.State != WebSocketState.Open) return Task.FromResult(true);
            return SendWebSocket(WebSocket, "{ \"event\": \"" + eventName + "\", \"payload\": ", payload);
        }

        async Task ViewContext.SaveViewConfiguration(DataValue newConfig) {
            ViewBase? view = currentView;
            if (view == null) return;
            var entry = views.FirstOrDefault(p => p.Value == view);
            if (entry.Value != view) return;
            string viewID = entry.Key;
            MemberValue member = MemberValue.Make(moduleID, viewID, nameof(View.Config), newConfig);
            await connection.UpdateConfig(member);
            view.Config = newConfig;
        }

        private readonly static Encoding UTF8_NoBOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private bool eventAcked = false;

        private readonly MemoryStream streamSend = new MemoryStream(512);

        private async Task SendWebSocket(WebSocket socket, string msgStart, object content) {
            try {

                var stream = streamSend;
                stream.Position = 0;
                stream.SetLength(0);

                // Console.WriteLine($"SendWebSocket: {stream.Capacity}  Thread: {Thread.CurrentThread.ManagedThreadId}");

                using (var writer = new StreamWriter(stream, UTF8_NoBOM, 1024, leaveOpen: true)) {
                    writer.Write(msgStart);
                    StdJson.ObjectToWriter(content, writer);
                    writer.Write("}");
                }

                ArraySegment<byte> segment;
                stream.TryGetBuffer(out segment);

                eventAcked = false;
                await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);


                int counter = 0;
                while (!eventAcked && !closed && counter < 100) {
                    await Task.Delay(50);
                    counter += 1;
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

        private void logInfo(string msg, Exception? exp = null) {
            Exception? exception = exp != null ? (exp.GetBaseException() ?? exp) : null;
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
                    if (receiveResult.Count == 2 && receiveBuffer[0] == (byte)'O' && receiveBuffer[1] == (byte)'K') {
                        eventAcked = true;
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
