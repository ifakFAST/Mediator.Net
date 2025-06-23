// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using MQTTnet.Client;
using MQTTnet;
using System.Threading;
using MQTTnet.Protocol;

namespace Ifak.Fast.Mediator.Publish.MQTT;

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

        var res = new List<MqttApplicationMessage>(listData.Count) {
            msgInfo
        };

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

    public static Task<IMqttClient?> EnsureConnect(MqttClientOptions? mqttOptions, IMqttClient? mqttClient) {
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

    private static (string host, int? port) ParseEndpoint(string endpoint) {

        string strUri = endpoint.Contains("://") ? endpoint : "mqtt://" + endpoint;

        Uri uri = new(strUri);
        string host = uri.Host;
        int? port = uri.Port < 0 ? null : uri.Port;

        return (host, port);
    }

    public static MqttClientOptions MakeMqttOptions(string certDir, MqttConfig config, string suffix) {

        string clientID = $"{config.ClientIDPrefix}_{suffix}_{TheGuid}";

        var (host, port) = ParseEndpoint(config.Endpoint);

        var builder = new MqttClientOptionsBuilder()
            .WithClientId(clientID)
            .WithTimeout(TimeSpan.FromSeconds(10))
            .WithTcpServer(host, port);

        bool hasUser = !string.IsNullOrEmpty(config.User);

        if (hasUser) {
            builder = builder.WithCredentials(config.User, config.Pass);
        }

        if (config.CertFileCA != "") {

            var caCert = new X509Certificate2(Path.Combine(certDir, config.CertFileCA));
            var clientCert = new X509Certificate2(Path.Combine(certDir, config.CertFileClient), "");

            builder = builder
             .WithTlsOptions(o => {
                 o
                 .UseTls(true)
                 .WithClientCertificates(new X509Certificate2[] {
                     clientCert,
                     caCert
                 })
                 .WithIgnoreCertificateRevocationErrors(config.IgnoreCertificateRevocationErrors)
                 .WithIgnoreCertificateChainErrors(config.IgnoreCertificateChainErrors)
                 .WithAllowUntrustedCertificates(config.AllowUntrustedCertificates);

                 if (config.NoCertificateValidation) {
                     // Will accept any certificate, even if hostname/IP does not match
                     o.WithCertificateValidationHandler(_ => true);
                 }

             });
        }
        else {

            bool useTLS = port != 1883;
            if (useTLS) {
                builder = builder
                 .WithTlsOptions(o => {
                     o
                      .UseTls(true)
                      .WithIgnoreCertificateRevocationErrors(config.IgnoreCertificateRevocationErrors)
                      .WithIgnoreCertificateChainErrors(config.IgnoreCertificateChainErrors)
                      .WithAllowUntrustedCertificates(config.AllowUntrustedCertificates);

                     if (config.NoCertificateValidation) {
                         // Will accept any certificate, even if hostname/IP does not match
                         o.WithCertificateValidationHandler(_ => true);
                     }
                 });
            }
        }

        return builder
            .WithCleanSession()
            .Build();
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
