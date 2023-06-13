using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using MQTTnet.Client;
using MQTTnet;
using System.Threading;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using MQTTnet.Protocol;

namespace Ifak.Fast.Mediator.Publish
{
    public partial class MqttPublisher
    {
        private static List<MqttApplicationMessage> MakeMessages(string payload, string topicBase, int maxPayloadSize) {

            List<ReadOnlyMemory<byte>> listData = LargePayloadWriter.GetPayloadAndBuckets(payload, maxPayloadSize);

            var msgInfo = new MqttApplicationMessage() {
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                Retain = true,
                Topic = $"{topicBase}/info",
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                PayloadSegment = listData[0].ToArray(),
            };

            var res = new List<MqttApplicationMessage>(listData.Count);

            res.Add(msgInfo);

            for (int i = 1; i < listData.Count; ++i) {
                var msgBucket = new MqttApplicationMessage() {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                    Retain = true,
                    Topic = $"{topicBase}/data{i - 1}",
                    PayloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified,
                    PayloadSegment = listData[i].ToArray(),
                };
                res.Add(msgBucket);
            }

            return res;
        }

        private static async Task<Connection> EnsureConnectOrThrow(ModuleInitInfo info, Connection? client, Action? onConfigChanged = null, string? moduleID = null) {

            if (client != null && !client.IsClosed) {
                try {
                    await client.Ping();
                    return client;
                }
                catch (Exception) {
                    Task _ = client.Close();
                    return await EnsureConnectOrThrow(info, null);
                }
            }

            try {
                EventListener? listener = onConfigChanged == null ? null : new MyEventListener(onConfigChanged);
                Connection con = await HttpConnection.ConnectWithModuleLogin(info, listener);
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

        private static Task<IMqttClient?> EnsureConnect(MqttClientOptions? mqttOptions, IMqttClient? mqttClient) {
            return EnsureConnectIntern(mqttOptions, mqttClient, 0);
        }

        private static async Task<IMqttClient?> EnsureConnectIntern(MqttClientOptions? mqttOptions, IMqttClient? mqttClient, int retry) {

            const int MaxRetry = 2;

            if (mqttClient != null && mqttClient.IsConnected) {
                return mqttClient;
            }

            Close(mqttClient);

            var factory = new MqttFactory();
            var client = factory.CreateMqttClient();

            try {
                await client.ConnectAsync(mqttOptions, CancellationToken.None);
                return client;
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                if (retry > 0) {
                    Console.Error.WriteLine($"Failed MQTT connection: {e.Message} (retry {retry} of {MaxRetry})");
                }
                client.Dispose();
                if (retry < MaxRetry) {
                    return await EnsureConnectIntern(mqttOptions, null, retry + 1);
                }
                else {
                    return null;
                }
            }
        }

        private static void Close(IMqttClient? mqttClient) {

            if (mqttClient != null) {
                Task _ = CloseIntern(mqttClient);
            }
        }

        private static async Task CloseIntern(IMqttClient client) {
            try {
                await client.DisconnectAsync();
            }
            catch (Exception) { }
            try {
                client.Dispose();
            }
            catch (Exception) { }
        }

        private static readonly string TheGuid = Guid.NewGuid().ToString().Replace("-", "");

        private static MqttClientOptions MakeMqttOptions(string certDir, MqttConfig config, string suffix) {

            string clientID = $"{config.ClientIDPrefix}_{suffix}_{TheGuid}";

            var builder = new MqttClientOptionsBuilder()
                .WithClientId(clientID)
                .WithTcpServer(config.Endpoint);

            if (config.CertFileCA != "") {

                var caCert = X509Certificate.CreateFromCertFile(Path.Combine(certDir, config.CertFileCA));
                var clientCert = new X509Certificate2(Path.Combine(certDir, config.CertFileClient), "");

                builder = builder
                .WithTls(new MqttClientOptionsBuilderTlsParameters() {
                    UseTls = true,
                    SslProtocol = System.Security.Authentication.SslProtocols.Tls12,
                    Certificates = new List<X509Certificate>()
                    {
                        clientCert,
                        caCert
                    },
                    IgnoreCertificateRevocationErrors = config.IgnoreCertificateRevocationErrors,
                    IgnoreCertificateChainErrors = config.IgnoreCertificateChainErrors,
                    AllowUntrustedCertificates = config.AllowUntrustedCertificates,
                });
            }

            return builder
                .WithCleanSession()
                .Build();
        }

    }

    public class MyEventListener : EventListener
    {
        private readonly Action onConfigChanged;

        public MyEventListener(Action onConfigChanged) {
            this.onConfigChanged = onConfigChanged;
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

    internal static class TaskUtil
    {
        internal static void CompleteFromTask(this TaskCompletionSource<bool> promise, Task completedTask) {

            if (completedTask.IsCompletedSuccessfully) {
                promise.SetResult(true);
            }
            else if (completedTask.IsFaulted) {
                promise.SetException(completedTask.Exception!);
            }
            else {
                promise.SetCanceled();
            }
        }
    }
}
