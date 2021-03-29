// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator
{
    public class TcpConnectorServer
    {
        private readonly TcpListener listener;

        public static TcpConnectorServer ListenOnFreePort() => ListenOnFreePort(IPAddress.Loopback);

        public static TcpConnectorServer ListenOnFreePort(IPAddress localaddr) {
            var listener = new TcpListener(localaddr, 0);
            listener.Start();
            return new TcpConnectorServer(listener);
        }

        private TcpConnectorServer(TcpListener listener) {
            this.listener = listener;
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public async Task<TcpConnectorMaster> WaitForConnect(TimeSpan? timeout) {

            Task<TcpClient> taskClient = listener.AcceptTcpClientAsync();

            if (timeout.HasValue) {
                Task t = await Task.WhenAny(Task.Delay(timeout.Value), taskClient);
                if (t != taskClient) throw new Exception("Timeout");
                return new TcpConnectorMaster(taskClient.Result);
            }
            else {
                return new TcpConnectorMaster(await taskClient);
            }
        }

        public void StopListening() {
            try {
                listener.Stop();
            }
            catch (Exception exp) {
                Console.Error.WriteLine(exp.Message);
            }
        }
    }

    public abstract class NoRestartException : Exception
    {
        public NoRestartException(string msg) : base(msg) { }
    }

    public class ConnectionCloseException : NoRestartException
    {
        public ConnectionCloseException(string msg) : base(msg) { }
    }

    public class TcpConnectorMaster : TcpConnectorCommon
    {
        private readonly TcpClient client;
        private readonly NetworkStream networkStream;
        private readonly Dictionary<int, TaskCompletionSource<Response>> mapPendingRequests = new Dictionary<int, TaskCompletionSource<Response>>();

        private int nextRequestID = 1;
        private bool closed = false;

        public TcpConnectorMaster(TcpClient client) {
            this.client = client;
            this.networkStream = client.GetStream();
        }

        public void Close(string? reason = null) {
            if (!closed) {
                closed = true;
                try {
                    networkStream.Close(0);
                    client.Close();
                }
                catch (Exception) { }

                if (mapPendingRequests.Count > 0) {

                    foreach (var entry in mapPendingRequests) {
                        TaskCompletionSource<Response> promise = entry.Value;
                        string msg = reason != null && !string.IsNullOrEmpty(reason) ? reason : "Connection closed!";
                        promise.TrySetException(new ConnectionCloseException(msg));
                    }
                    mapPendingRequests.Clear();
                }
            }
        }

        public Task<Response> SendRequest(byte code, Action<Stream> writeData) {

            int reqID = GetNextReqID();

            using (var stream = MemoryManager.GetMemoryStream("TcpConnectorMaster.SendRequest")) {

                stream.Seek(HeadLen, SeekOrigin.Begin);

                stream.WriteByte((byte)((reqID & 0xFF000000) >> 24));
                stream.WriteByte((byte)((reqID & 0x00FF0000) >> 16));
                stream.WriteByte((byte)((reqID & 0x0000FF00) >> 8));
                stream.WriteByte((byte)((reqID & 0x000000FF)));

                stream.WriteByte(code);

                writeData(stream);

                int frameLen = (int)stream.Position - HeadLen;
                stream.Seek(0, SeekOrigin.Begin);

                stream.WriteByte(MagicByte_Request);
                stream.WriteByte((byte)((frameLen & 0xFF000000) >> 24));
                stream.WriteByte((byte)((frameLen & 0x00FF0000) >> 16));
                stream.WriteByte((byte)((frameLen & 0x0000FF00) >> 8));
                stream.WriteByte((byte)((frameLen & 0x000000FF)));

                stream.Seek(0, SeekOrigin.Begin);

                stream.CopyTo(networkStream);

                var promise = new TaskCompletionSource<Response>();

                mapPendingRequests[reqID] = promise;

                return promise.Task;
            }
        }

        public async Task ReceiveAndDistribute(Action<Event> onEvent) {

            // When TCP connection is closed by peer, this Task will complete faulted because of Exception raised by networkStream

            while (!closed) {

                ResponseOrEvent it = await ReceiveResponseOrEvent();

                if (it.IsResponse) {
                    Response response = (Response)it;
                    int reqID = response.RequestID;
                    if (mapPendingRequests.ContainsKey(reqID)) {
                        TaskCompletionSource<Response> promise = mapPendingRequests[reqID];
                        promise.SetResult(response);
                        mapPendingRequests.Remove(reqID);
                    }
                    else {
                        Console.Error.WriteLine("Response with unexpected request ID: " + reqID);
                        response.Dispose();
                    }
                }
                else {
                    using (Event evt = (Event)it) {
                        onEvent(evt);
                    }
                }
            }
        }

        private async Task<ResponseOrEvent> ReceiveResponseOrEvent() {

            byte[] buffer = new byte[4 * 1024];
            int headLenRead = await networkStream.ReadAsync(buffer, 0, HeadLen);

            if (headLenRead == 0)
                throw new IOException("No frame start. Connection closed?");

            byte t = buffer[0];

            if (t != MagicByte_Event && t != MagicByte_ResponseSuccess && t != MagicByte_ResponseError)
                throw new IOException("Frame error");

            while (headLenRead < HeadLen) {
                int headInc = await networkStream.ReadAsync(buffer, headLenRead, HeadLen - headLenRead);
                if (headInc == 0)
                    throw new IOException("Incomplete frame header. Connection closed?");
                headLenRead += headInc;
            }

            int len = (buffer[1] << 24) | (buffer[2] << 16) | (buffer[3] << 8) | buffer[4];
            int offset = 0;

            var stream = MemoryManager.GetMemoryStream("TcpConnectorMaster.ReceiveResponseOrEvent");

            try {

                do {
                    int read = await networkStream.ReadAsync(buffer, 0, Math.Min(buffer.Length, len - offset));

                    if (read == 0)
                        throw new IOException("Incomplete frame. Connection closed?");

                    offset += read;
                    stream.Write(buffer, 0, read);

                } while (offset < len);

                stream.Seek(0, SeekOrigin.Begin);

                if (t == MagicByte_Event) {
                    byte eventCode = (byte)stream.ReadByte();
                    return new Event(eventCode, stream);
                }
                else {

                    byte b0 = (byte)stream.ReadByte();
                    byte b1 = (byte)stream.ReadByte();
                    byte b2 = (byte)stream.ReadByte();
                    byte b3 = (byte)stream.ReadByte();

                    int requestID = (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;

                    if (t == MagicByte_ResponseSuccess) {
                        return Response.MakeSuccess(requestID, stream);
                    }
                    else {
                        using (var reader = new BinaryReader(stream, Encoding.UTF8)) {
                            string msg = reader.ReadString();
                            return Response.MakeError(requestID, msg);
                        }
                    }
                }
            }
            catch (Exception) {
                stream.Dispose();
                throw;
            }
        }

        private int GetNextReqID() {
            var res = nextRequestID;
            nextRequestID += 1;
            return res;
        }
    }

    public class TcpConnectorSlave : TcpConnectorCommon
    {
        private const int SendTimeout = 10000; // ms

        private TcpClient? connection = null;
        private NetworkStream? networkStream = null;
        private volatile bool closed = true;

        public void Connect(string host, int port) {
            closed = false;
            connection = new TcpClient();
            connection.SendTimeout = SendTimeout;
            connection.ReceiveTimeout = 0;
            connection.Connect(host, port);
            networkStream = connection.GetStream();
        }

        public async Task ConnectAsync(string host, int port) {
            closed = false;
            connection = new TcpClient();
            connection.SendTimeout = SendTimeout;
            connection.ReceiveTimeout = 0;
            await connection.ConnectAsync(host, port);
            networkStream = connection.GetStream();
        }

        public bool IsConnected
        {
            get
            {
                try {
                    return connection != null && !closed && connection.Connected;
                }
                catch (Exception) {
                    return false;
                }
            }
        }

        public void Close() {
            if (connection != null && !closed) {
                try {
                    closed = true;
                    networkStream?.Close(0);
                    connection.Close();
                    networkStream = null;
                    connection = null;
                }
                catch (Exception) { }
            }
        }

        public async Task<Request> ReceiveRequest(int timeoutMS = 0) {

            if (timeoutMS == 0)
                return await ReceiveRequestImpl();

            var t = ReceiveRequestImpl();
            if (t == await Task.WhenAny(t, Task.Delay(timeoutMS))) {
                return await t;
            }

            Close();
            throw new Exception("Timeout");
        }

        public void SendEvent(byte eventID, Action<Stream> writeData) {
            sendFrame(s => {
                s.WriteByte(eventID);
                writeData(s);
            }, MagicByte_Event);
        }

        public void SendResponseSuccess(int reqID, Action<Stream> writeData) {

            sendFrame(stream => {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                    writer.Write(IPAddress.HostToNetworkOrder(reqID));
                }
                writeData(stream);
            }, MagicByte_ResponseSuccess);
        }

        public void SendResponseError(int reqID, Exception exp) {

            sendFrame(stream => {

                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                    writer.Write(IPAddress.HostToNetworkOrder(reqID));
                    writer.Write(exp.Message);
                }

            }, MagicByte_ResponseError);
        }

        private async Task<Request> ReceiveRequestImpl() {

            var stream = MemoryManager.GetMemoryStream("TcpConnectorSlave.ReceiveRequestImpl");

            try {

                int len = await ReceiveRequestRaw(stream);

                if (len < 5) throw new IOException("Request frame too small. Connection closed?");

                byte b0 = (byte)stream.ReadByte();
                byte b1 = (byte)stream.ReadByte();
                byte b2 = (byte)stream.ReadByte();
                byte b3 = (byte)stream.ReadByte();

                int id = (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
                byte code = (byte)stream.ReadByte();

                return new Request(id, code, stream);
            }
            catch (Exception) {
                stream.Dispose();
                throw;
            }
        }

        private async Task<int> ReceiveRequestRaw(MemoryStream stream) {

            if (closed || networkStream == null) throw new Exception("Connection is closed");

            byte[] buffer = new byte[4 * 1024];

            int headLenRead = await networkStream.ReadAsync(buffer, 0, HeadLen);

            if (headLenRead == 0)
                throw new IOException("No frame start. Connection closed?");

            byte t = buffer[0];

            if (t != MagicByte_Request)
                throw new IOException("Frame error");

            while (headLenRead < HeadLen) {
                int headInc = await networkStream.ReadAsync(buffer, headLenRead, HeadLen - headLenRead);
                if (headInc == 0)
                    throw new IOException("Incomplete frame header. Connection closed?");
                headLenRead += headInc;
            }

            int len = (buffer[1] << 24) | (buffer[2] << 16) | (buffer[3] << 8) | buffer[4];
            int offset = 0;

            do {

                int read = await networkStream.ReadAsync(buffer, 0, Math.Min(buffer.Length, len - offset));

                if (read == 0)
                    throw new IOException("Incomplete frame. Connection closed?");

                offset += read;
                stream.Write(buffer, 0, read);

            } while (offset < len);

            stream.Seek(0, SeekOrigin.Begin);

            return len;
        }

        private readonly object writeLock = new object();

        private void sendFrame(Action<Stream> writeData, byte respReq) {

            if (closed) throw new IOException("Connection is closed");

            using (var stream = MemoryManager.GetMemoryStream("TcpConnectorSlave.sendFrame")) {
                stream.Seek(HeadLen, SeekOrigin.Begin);
                writeData(stream);
                int len = (int)stream.Position - HeadLen;
                stream.Seek(0, SeekOrigin.Begin);

                stream.WriteByte(respReq);
                stream.WriteByte((byte)((len & 0xFF000000) >> 24));
                stream.WriteByte((byte)((len & 0x00FF0000) >> 16));
                stream.WriteByte((byte)((len & 0x0000FF00) >> 8));
                stream.WriteByte((byte)((len & 0x000000FF)));

                stream.Seek(0, SeekOrigin.Begin);

                lock (writeLock) {
                    stream.CopyTo(networkStream);
                }
            }
        }
    }

    public abstract class TcpConnectorCommon
    {
        public const int HeadLen = 5;
        public const byte MagicByte_Request = 0x42;
        public const byte MagicByte_ResponseSuccess = 0x43;
        public const byte MagicByte_ResponseError = 0x44;
        public const byte MagicByte_Event = 0x45;
    }

    public abstract class ResponseOrEvent: IDisposable
    {
        public abstract bool IsResponse { get; }

        public abstract void Dispose();
    }

    public sealed class Response : ResponseOrEvent
    {
        private Response(bool success, int requestID, string? errorMsg, MemoryStream? successPayload) {
            Success = success;
            RequestID = requestID;
            ErrorMsg = errorMsg;
            SuccessPayload = successPayload;
        }

        public static Response MakeSuccess(int requestID, MemoryStream successPayload) {
            return new Response(true, requestID, null, successPayload);
        }

        public static Response MakeError(int requestID, string errMsg) {
            return new Response(false, requestID, errMsg, null);
        }

        public override void Dispose() {
            if (SuccessPayload != null) {
                SuccessPayload.Dispose();
            }
        }

        public bool Success { get; private set; }
        public int RequestID { get; private set; }
        public string? ErrorMsg { get; private set; }
        public MemoryStream? SuccessPayload { get; private set; }

        public override bool IsResponse => true;
    }

    public sealed class Event : ResponseOrEvent
    {
        public Event(byte code, MemoryStream payload) {
            if (payload == null) throw new ArgumentNullException("payload");
            Code = code;
            Payload = payload;
        }

        public byte Code { get; private set; }
        public MemoryStream Payload { get; private set; }

        public override bool IsResponse => false;

        public override void Dispose() {
            Payload.Dispose();
        }
    }

    public sealed class Request : IDisposable
    {
        public Request(int id, byte code, MemoryStream payload) {
            if (payload == null) throw new ArgumentNullException("payload");
            RequestID = id;
            Code = code;
            Payload = payload;
        }

        public int RequestID { get; private set; }
        public byte Code { get; private set; }
        public MemoryStream Payload { get; private set; }

        public void Dispose() {
            Payload.Dispose();
        }
    }
}
