using MQTTnet;
using MQTTnet.Adapter;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO.Adapter_MQTT;

public static class MQTT_Util {

    private static readonly string TheGuid = Guid.NewGuid().ToString().Replace("-", "");

    public static MqttClientOptions MakeMqttOptions(string certDir, MqttConfig config) {

        string prefix = config.ClientIDPrefix;
        string clientID = string.IsNullOrEmpty(prefix) ? TheGuid : $"{prefix}_{TheGuid}";

        var builder = new MqttClientOptionsBuilder()
            .WithClientId(clientID)
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithTcpServer(config.Endpoint);

        if (!string.IsNullOrEmpty(config.User)) {
            builder = builder.WithCredentials(config.User, config.Pass);
        }

        if (config.CertFileCA != "") {

            var caCert = X509Certificate.CreateFromCertFile(Path.Combine(certDir, config.CertFileCA));
            var clientCert = new X509Certificate2(Path.Combine(certDir, config.CertFileClient), "");

            builder = builder
            .WithTls(new MqttClientOptionsBuilderTlsParameters() {
                UseTls = true,
                SslProtocol = System.Security.Authentication.SslProtocols.None, // None = Use system default
                Certificates = new List<X509Certificate>() {
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


    public static Task<IMqttClient> ConnectOrThrow(MqttClientOptions mqttOptions, CancellationToken cancellationToken) {
        return ConnectOrThrowIntern(mqttOptions, 0, cancellationToken);
    }

    private static async Task<IMqttClient> ConnectOrThrowIntern(MqttClientOptions mqttOptions, int retry, CancellationToken cancellationToken) {

        const int MaxRetry = 1;

        MqttFactory factory = new MqttFactory();
        IMqttClient client = factory.CreateMqttClient();

        using var cancelSourceTimeout = new CancellationTokenSource(5000);
        using var combi = CancellationTokenSource.CreateLinkedTokenSource(cancelSourceTimeout.Token, cancellationToken);

        try {
            await client.ConnectAsync(mqttOptions, combi.Token);
            return client;
        }
        catch (Exception exp) {

            Exception e = exp.GetBaseException() ?? exp;

            string? errMsg = null;

            if (e is MqttConnectingFailedException mm && mm.Result != null) {
                errMsg = mm.Result.ResultCode.ToString();
            }
            else if (e is OperationCanceledException) {
                if (cancellationToken.IsCancellationRequested)
                    errMsg = "Canceled";
                else
                    errMsg = "Timeout";
            }
            else {
                errMsg = e.Message;
            }

            client.Dispose();

            if (cancellationToken.IsCancellationRequested) {
                throw new Exception("Canceled");
            }

            if (retry < MaxRetry) {
                return await ConnectOrThrowIntern(mqttOptions, retry + 1, cancellationToken);
            }
            else {
                throw new Exception(errMsg);
            }
        }
    }

    public static void Close(IMqttClient? mqttClient) {

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

}

public sealed class MqttConfig {

    public string Endpoint { get; set; } = "";
    public string ClientIDPrefix { get; set; } = "";
    public string CertFileCA { get; set; } = "";
    public string CertFileClient { get; set; } = "";
    public string User { get; set; } = "";
    public string Pass { get; set; } = "";
    public bool IgnoreCertificateRevocationErrors { get; set; } = false;
    public bool IgnoreCertificateChainErrors { get; set; } = false;
    public bool AllowUntrustedCertificates { get; set; } = false;
}