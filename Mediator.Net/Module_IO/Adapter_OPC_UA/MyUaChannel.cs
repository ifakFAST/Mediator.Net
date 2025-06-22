// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Workstation.ServiceModel.Ua.Channels;
using Workstation.ServiceModel.Ua;

namespace Ifak.Fast.Mediator.IO.Adapter_OPC_UA;

// We need this MyUaChannel class in order to early fail when the client certificate is not trusted by the server
// Otherwise the connection will timeout
public sealed class MyUaChannel : ClientSessionChannel
{

    public MyUaChannel(
        ApplicationDescription localDescription,
        ICertificateStore? certificateStore,
        IUserIdentity userIdentity,
        EndpointDescription remoteEndpoint,
        ILoggerFactory? loggerFactory = null,
        ClientSessionChannelOptions? options = null) :

        base(localDescription, certificateStore, userIdentity, remoteEndpoint, loggerFactory, options) {

        long timeoutMS = (options ?? new ClientSessionChannelOptions()).TimeoutHint;
        timeoutOpen = TimeSpan.FromMilliseconds(timeoutMS) + TimeSpan.FromSeconds(2);
    }

    private readonly TimeSpan timeoutOpen;

    internal delegate void MyEventHandler();

    private event MyEventHandler? FaultedWhileOpening;

    protected override Task OnFaulted(CancellationToken token = default) {
        FaultedWhileOpening?.Invoke();
        return base.OnFaulted(token);
    }

    public async Task OpenAsync() {

        using var cancelOpenAsync = new CancellationTokenSource();

        async Task DelayAbort() {
            await Task.Delay(1000);
            cancelOpenAsync.Cancel(); // will throw Exception when disposed; no problem here
        }

        void OnFaultedWhileOpening() {
            Task _ = DelayAbort();
        }

        Task taskTimeoutOpen = Task.Delay(timeoutOpen);

        FaultedWhileOpening += OnFaultedWhileOpening;
        Task taskOpen = base.OpenAsync(); // this OpenAsync Task may not fail until timeout even if channel is faulted already
        Task taskFaulted = Task.Delay(Timeout.Infinite, cancelOpenAsync.Token);
        Task firstCompleted = await Task.WhenAny(taskOpen, taskFaulted, taskTimeoutOpen);
        FaultedWhileOpening -= OnFaultedWhileOpening;

        if (firstCompleted == taskFaulted) {
            string msg = GetPendingException()?.Message ?? "Fault on connect.";
            throw new Exception(msg);
        }

        if (firstCompleted == taskTimeoutOpen) {
            Task ignored = AbortAsync();
            throw new Exception("Timeout on connect.");
        }

        await taskOpen;
    }
}
