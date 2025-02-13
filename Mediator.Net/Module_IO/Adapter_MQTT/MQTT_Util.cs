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

    private static (string host, int? port) ParseEndpoint(string endpoint) {

        string strUri = endpoint.Contains("://") ? endpoint : "mqtt://" + endpoint;

        Uri uri = new(strUri);
        string host = uri.Host;
        int? port = uri.Port < 0 ? null : uri.Port;

        return (host, port);
    }

    public static MqttClientOptions MakeMqttOptions(string certDir, MqttConfig config) {

        string prefix = config.ClientIDPrefix;
        string clientID = string.IsNullOrEmpty(prefix) ? TheGuid : $"{prefix}_{TheGuid}";

        var (host, port) = ParseEndpoint(config.Endpoint);

        var builder = new MqttClientOptionsBuilder()
            .WithClientId(clientID)
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithTcpServer(host, port);

        bool hasUser = !string.IsNullOrEmpty(config.User);

        if (hasUser) {
            builder = builder.WithCredentials(config.User, config.Pass);
        }

        List<X509Certificate2> certificates = [];

        if (config.CertFileCA != "") {
            string certFileCA = Path.Combine(certDir, config.CertFileCA);
            if (!File.Exists(certFileCA)) {
                throw new Exception($"CA certificate file not found: {certFileCA}");
            }
            X509Certificate2 caCert = new(certFileCA);
            certificates.Add(caCert);
        }

        if (config.CertFileClient != "") {

            string certFile = Path.Combine(certDir, config.CertFileClient);
            string keyFile = Path.Combine(certDir, config.KeyFileClient);
            bool hasKeyFile = !string.IsNullOrEmpty(config.KeyFileClient);

            if (!File.Exists(certFile)) {
                throw new Exception($"Client certificate file not found: {certFile}");
            }

            if (hasKeyFile && !File.Exists(keyFile)) {
                throw new Exception($"Client certificate key file not found: {keyFile}");
            }

            bool isPfx = certFile.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase);

            if (isPfx && hasKeyFile) {
                throw new Exception($"No key file required if client certificate is in .pfx file format");
            }

            X509Certificate2 clientCert = isPfx ?
                new X509Certificate2(certFile, "") :
                new X509Certificate2(X509Certificate2.CreateFromPemFile(certFile, keyFile).Export(X509ContentType.Pkcs12));

            certificates.Insert(0, clientCert);
        }

        bool useTLS = certificates.Count > 0 || port != 1883;

        if (useTLS) {
            builder = builder
             .WithTlsOptions(o => o
                 .UseTls(true)
                 .WithClientCertificates(certificates)
                 .WithIgnoreCertificateRevocationErrors(config.IgnoreCertificateRevocationErrors)
                 .WithIgnoreCertificateChainErrors(config.IgnoreCertificateChainErrors)
                 .WithAllowUntrustedCertificates(config.AllowUntrustedCertificates));
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

        MqttFactory factory = new();
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
    public string KeyFileClient { get; set; } = "";
    public string User { get; set; } = "";
    public string Pass { get; set; } = "";
    public bool IgnoreCertificateRevocationErrors { get; set; } = false;
    public bool IgnoreCertificateChainErrors { get; set; } = false;
    public bool AllowUntrustedCertificates { get; set; } = false;
}