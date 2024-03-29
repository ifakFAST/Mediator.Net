﻿// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO.Adapter_Modbus
{
    [Identify("ModbusTCP")]
    public class ModbusTCP : AdapterBase
    {
        private TcpClient? connection = null;
        private NetworkStream? networkStream = null;
        private Adapter? config;
        private AdapterCallback? callback;
        private Dictionary<string, ItemInfo> mapId2Info = new Dictionary<string, ItemInfo>();
        private const int TimeOutMS = 2000;

        public override bool SupportsScheduledReading => true;

        protected virtual ModbusAddress GetModbusAddress(DataItem item) {
            bool is8Bit = item.Type == DataType.Byte || item.Type == DataType.SByte;
            int dimension = Math.Max(1, item.Dimension);
            string address = item.Address;

            ushort count = is8Bit ?
                (ushort)(dimension / 2 + dimension % 2) :
                (ushort)(RegisterPerType(item.Type) * dimension);

            if (is8Bit && (address.EndsWith('L') || address.EndsWith('H'))) {
                address = address.Substring(0, address.Length - 1);
            }
            ushort add = ushort.Parse(address);
            if (add == 0) throw new Exception("Modbus register may not be zero. First register starts with 1.");
            return ModbusAddress.Make(add, count);
        }

        protected virtual VTQ ParseModbusResponse(DataItem item, ushort[] words, Timestamp now) {

            if (item.Dimension <= 1) {

                switch (item.Type) {

                    case DataType.Float32: {

                            if (IsLittleEndianWordOrder(item)) {
                                ushort temp = words[0];
                                words[0] = words[1];
                                words[1] = temp;
                            }

                            float v = ReadFloat32FromWords(words, 0);
                            return VTQ.Make(v, now, Quality.Good);
                        }

                    case DataType.Int16: {
                            int v = (short)words[0];
                            return VTQ.Make(v, now, Quality.Good);
                        }

                    case DataType.UInt16: {
                            int v = words[0];
                            return VTQ.Make(v, now, Quality.Good);
                        }

                    case DataType.Byte: {
                            bool low = item.Address.EndsWith('L');
                            int v = words[0];
                            v = low ? (v & 0xFF) : (v >> 8);
                            return VTQ.Make(v, now, Quality.Good);
                        }

                    case DataType.SByte: {
                            bool low = item.Address.EndsWith('L');
                            int v = words[0];
                            v = (sbyte)(low ? (v & 0xFF) : (v >> 8));
                            return VTQ.Make(v, now, Quality.Good);
                        }
                }
            }
            else {
                int n = item.Dimension;
                int offset = 0;
                switch (item.Type) {

                    case DataType.Float32: {

                            float[] values = new float[n];
                            for (int i = 0; i < n; ++i) {
                                values[i] = ReadFloat32FromWords(words, offset);
                                offset += 2;
                            }
                            return VTQ.Make(DataValue.FromFloatArray(values), now, Quality.Good);
                        }

                    case DataType.Int16: {

                            int[] values = new int[n];
                            for (int i = 0; i < n; ++i) {
                                values[i] = (short)words[offset];
                                offset += 1;
                            }
                            return VTQ.Make(DataValue.FromIntArray(values), now, Quality.Good);

                        }

                    case DataType.UInt16: {

                            int[] values = new int[n];
                            for (int i = 0; i < n; ++i) {
                                values[i] = words[offset];
                                offset += 1;
                            }
                            return VTQ.Make(DataValue.FromIntArray(values), now, Quality.Good);

                        }

                    case DataType.Byte: {

                            int[] values = new int[n];
                            bool low = false;
                            for (int i = 0; i < n; ++i) {
                                int word = words[offset];
                                values[i] = low ? (word & 0xFF) : (word >> 8);
                                offset += (i % 2);
                                low = !low;
                            }
                            return VTQ.Make(DataValue.FromIntArray(values), now, Quality.Good);

                        }

                    case DataType.SByte: {

                            int[] values = new int[n];
                            bool low = false;
                            for (int i = 0; i < n; ++i) {
                                int word = words[offset];
                                values[i] = (sbyte)(low ? (word & 0xFF) : (word >> 8));
                                offset += (i % 2);
                                low = !low;
                            }
                            return VTQ.Make(DataValue.FromIntArray(values), now, Quality.Good);

                        }
                }
            }

            return new VTQ();
        }

        protected virtual byte GetModbusFunctionCode(Adapter adapter, DataItem item) { //=> 3 = Read Holding Registers; // 4 = Read Input Register
            foreach (var it in item.Config) {
                if (it.Name == "FunctionCode") {
                    return byte.Parse(it.Value);
                }
            }
            return 4;
        }

        protected bool IsLittleEndianWordOrder(DataItem item) {
            foreach (var it in item.Config) {
                if (it.Name == "WordOrder") {
                    return it.Value == "little-endian";
                }
            }
            return false;
        }

        protected virtual byte GetModbusHeaderAddress(Adapter adapter, DataItem item) => 1; // doesn't seem to matter

        public override async Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

            this.config = config;
            this.callback = callback;

            PrintLine(config.Address);

            foreach (var di in config.GetAllDataItems()) {
                try {
                    if (!string.IsNullOrEmpty(di.Address)) {
                        GetModbusAddress(di);
                    }
                }
                catch (Exception exp) {
                    throw new Exception($"Invalid Address '{di.Address}' for DataItem {di.Name}: {exp.Message}");
                }
            }

            await TryConnect();

            return new Group[0];
        }

        private async Task<bool> TryConnect() {

            if (connection != null) {
                return true;
            }

            if (config == null || string.IsNullOrWhiteSpace(config.Address)) return false;

            try {

                var (host, port) = GetHostAndPort(config);

                connection = new TcpClient();
                connection.SendTimeout = TimeOutMS;
                connection.ReceiveTimeout = TimeOutMS;

                using var cancelSource = new CancellationTokenSource(TimeOutMS);

                PrintLine($"Connecting to ModbusTCP device {host}:{port}...");

                await connection.ConnectAsync(host, port, cancelSource.Token);
                networkStream = connection.GetStream();

                this.mapId2Info = config.GetAllDataItems().Where(di => !string.IsNullOrEmpty(di.Address)).ToDictionary(
                    item => /* key */ item.ID,
                    item => /* val */ new ItemInfo(item, GetModbusAddress(item)));

                PrintLine($"Connected to ModbusTCP device {host}:{port}.");
                ReturnToNormal("Connect", $"Reconnected to ModbusTCP device {host}:{port}.");

                return true;
            }
            catch (Exception exp) {
                Exception baseExp = exp.GetBaseException() ?? exp;

                string msg = baseExp.Message;
                if (baseExp is OperationCanceledException) {
                    msg = "Timeout";
                }

                LogWarn("Connect", $"Connection error: {msg}", details: baseExp.StackTrace);
                CloseConnection();
                return false;
            }
        }

        protected float ReadFloat32FromWords(ushort[] words, int offset) {
            ushort w0 = words[offset];
            ushort w1 = words[offset + 1];
            Span<byte> bytes = stackalloc byte[4];
            bytes[3] = (byte)((w0 & 0xFF00) >> 8);
            bytes[2] = (byte)((w0 & 0xFF));
            bytes[1] = (byte)((w1 & 0xFF00) >> 8);
            bytes[0] = (byte)((w1 & 0xFF));
            return BitConverter.ToSingle(bytes);
        }

        protected int RegisterPerType(DataType t) {
            switch (t) {
                case DataType.Bool: return 1;
                case DataType.Byte: return 1;
                case DataType.SByte: return 1;
                case DataType.Int16: return 1;
                case DataType.UInt16: return 1;
                case DataType.Int32: return 2;
                case DataType.UInt32: return 2;
                case DataType.Int64: return 4;
                case DataType.UInt64: return 4;
                case DataType.Float32: return 2;
                case DataType.Float64: return 4;
                default: return 1;
            }
        }

        private void CloseConnection() {
            if (connection == null) return;

            try {
                networkStream?.Close(0);
            }
            catch (Exception) { }
            networkStream = null;

            try {
                connection.Close();
            }
            catch (Exception) { }
            connection = null;
        }

        public override Task Shutdown() {
            CloseConnection();
            return Task.FromResult(true);
        }

        private static (string host, int port) GetHostAndPort(Adapter config) {

            string address = config.Address;

            int port = 502;
            string host = address;

            int idxPortSep = address.IndexOf(':');
            if (idxPortSep < 0) return (host, port);

            host = address.Substring(0, idxPortSep);
            string strPort = address.Substring(idxPortSep + 1);
            if (!int.TryParse(strPort, out port)) {
                throw new Exception("Port part of address is not a number: " + address);
            }

            return (host, port);
        }

        public override Task<string[]> BrowseAdapterAddress() {
            return Task.FromResult(new string[0]);
        }

        public override Task<string[]> BrowseDataItemAddress(string? idOrNull) {
            return Task.FromResult(new string[0]);
        }

        public override async Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {

            int N = items.Count;

            if (!await TryConnect() || networkStream == null || config == null) {
                return GetBadVTQs(items);
            }

            VTQ[] vtqs = new VTQ[N];

            byte[] writeBuffer = new byte[7+5]; // 7: Header, 5: PDU

            for (int i = 0; i < N; ++i) {
                ReadRequest request = items[i];
                if (networkStream != null  && mapId2Info.ContainsKey(request.ID)) {

                    ItemInfo item = mapId2Info[request.ID];
                    ModbusAddress address = item.Address;
                    WriteUShort(writeBuffer, 0, (ushort)i); // Transaction-ID
                    WriteUShort(writeBuffer, 2, 0); // Protocol-ID
                    WriteUShort(writeBuffer, 4, 6); // Length
                    writeBuffer[6] = GetModbusHeaderAddress(config, item.Item);
                    writeBuffer[7] = GetModbusFunctionCode(config, item.Item);
                    WriteUShort(writeBuffer, 8, (ushort)(address.Start - 1));
                    WriteUShort(writeBuffer, 10, address.Count);

                    //PrintLine("Sending read request: " + BitConverter.ToString(writeBuffer));

                    ushort[] words;
                    try {
                        await networkStream.WriteAsync(writeBuffer);
                        words = await ConsumeReadResponse(networkStream, address.Count);                        
                    }
                    catch (Exception exp) {
                        Exception e = exp.GetBaseException() ?? exp;
                        if (e is ModbusException) {
                            LogWarn("ReadExcept", $"Got exception response for reading {item.Item.Name}: {e.Message}");
                        }
                        else {
                            CloseConnection();
                        }
                        vtqs[i] = VTQ.Make(request.LastValue.V, Timestamp.Now, Quality.Bad);                        
                        continue;
                    }

                    try {
                        vtqs[i] = ParseModbusResponse(item.Item, words, Timestamp.Now);
                    }
                    catch (Exception exp) {
                        Exception e = exp.GetBaseException() ?? exp;
                        LogWarn("ReadExcept", $"Failed to convert read response for {item.Item.Name}: {e.Message}");
                        vtqs[i] = VTQ.Make(request.LastValue.V, Timestamp.Now, Quality.Bad);
                    }

                }
                else {
                    vtqs[i] = VTQ.Make(request.LastValue.V, Timestamp.Now, Quality.Bad);
                }
            }

            return vtqs;
        }

        

        private async Task<ushort[]> ConsumeReadResponse(NetworkStream networkStream, int wordCount) {

            const int ResponseHeadLen = 9; // 2b transaction ID, 2b protocol ID, 2b message length, 1b dev. address, 1b func. code, 1b exception code or length of content
            byte[] header = new byte[ResponseHeadLen];
            await networkStream.ReadExactlyAsync(header);

            byte fc = header[7];
            if ((fc & 0x80) != 0) {
                byte exceptionCode = header[8];
                string error = GetExceptionDescription(exceptionCode);
                throw new ModbusException(error);
            }

            int messageLength = (header[4] << 8) | header[5];
            int remainingBytes = messageLength - 3;

            if (remainingBytes != (2 * wordCount)) {
                throw new Exception($"Response length {remainingBytes} does not match expected number of bytes: {2 * wordCount}.");
            }

            byte[] buffer = new byte[remainingBytes];
            await networkStream.ReadExactlyAsync(buffer);

            ushort[] res = new ushort[wordCount];
            int off = 0;
            for (int i = 0; i < wordCount; ++i) {
                res[i] = (ushort)(((buffer[off] & 0xFF) << 8) | (buffer[off + 1] & 0xFF));
                off += 2;
            }

            return res;
        }

        private async Task ConsumeWriteResponse(NetworkStream networkStream) {

            const int ResponseHeadLen = 8; // 2b transaction ID, 2b protocol ID, 2b message length, 1b dev. address, 1b func. code
            byte[] header = new byte[ResponseHeadLen];
            await networkStream.ReadExactlyAsync(header);

            byte fc = header[7];
            if ((fc & 0x80) != 0) {
                byte[] exceptionCode = new byte[1];
                await networkStream.ReadExactlyAsync(exceptionCode);
                string error = GetExceptionDescription(exceptionCode[0]);
                throw new ModbusException(error);
            }

            int messageLength = (header[4] << 8) | header[5];
            int remainingBytes = messageLength - 2;
            if (remainingBytes != 4) throw new Exception("Unexpected message length");
            byte[] buffer = new byte[4];
            await networkStream.ReadExactlyAsync(buffer);
        }

        private static string GetExceptionDescription(byte exceptionCode) => exceptionCode switch {
            1 => "Illegal Function - The function code in the request is not supported by the server",
            2 => "Illegal Data Address - The data address specified in the request is not valid",
            3 => "Illegal Data Value - The value specified in the request is not valid",
            4 => "Server Device Failure - An unrecoverable error occurred while the server was attempting to perform the requested action",
            5 => "Acknowledge - The server has received the request but is currently busy processing other requests",
            6 => "Server Device Busy - The server is currently unable to perform the requested action",
            8 => "Memory Parity Error - The server has detected a parity error in memory",
            10 => "Gateway Path Unavailable - The gateway device is unable to establish a connection to the target device",
            11 => "Gateway Target Device Failed to Respond - The gateway device has not received a response from the target device",
            _ => "Unknown Exception Code",
        };

        private static void WriteUShort(byte[] bytes, int offset, ushort value) {
            bytes[offset + 0] = (byte)((value & 0xFF00) >> 8);
            bytes[offset + 1] = (byte) (value & 0x00FF);
        }

        private static VTQ[] GetBadVTQs(IList<ReadRequest> items) {
            int N = items.Count;
            var t = Timestamp.Now;
            VTQ[] res = new VTQ[N];
            for (int i = 0; i < N; ++i) {
                VTQ vtq = items[i].LastValue;
                vtq.Q = Quality.Bad;
                vtq.T = t;
                res[i] = vtq;
            }
            return res;
        }

        public override async Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> values, Duration? timeout) {

            int N = values.Count;
            bool connected = await TryConnect();

            // return error if connection is not OK
            if (!connected || networkStream == null || config == null) {
                var failed = new FailedDataItemWrite[N];
                for (int i = 0; i < N; ++i) {
                    DataItemValue request = values[i];
                    failed[i] = new FailedDataItemWrite(request.ID, "No connection to Modbus TCP server");
                }
                return WriteDataItemsResult.Failure(failed);
            }

            // assign variables for write buffer and status
            byte[] writeBuffer = new byte[7 + 5]; // 7: Header, 5: PDU
            byte[] writeBuffer_float = new byte[7 + 10]; // 7: Header, 10: PDU
            List<FailedDataItemWrite>? listFailed = null;

            // loop through values to write
            for (int i = 0; i < N; ++i) {
                DataItemValue request = values[i];
                string id = request.ID;
                VTQ value = request.Value;

                if (mapId2Info.ContainsKey(id)) {
                    ItemInfo item = mapId2Info[id];
                    ModbusAddress address = item.Address;

                    // here come the differences between floats and ints
                    byte funcCode;
                    ushort length;

                    if (item.Item.Type == DataType.Float32) // more than 16 bit = multiple registers
                    {
                        length = 11; // message length
                        funcCode = 16; // function code
                        // value to write
                        float fl_val = (float)value.V.GetValue(dt: item.Item.Type, dimension: item.Item.Dimension)!;
                        byte[] bytes = BitConverter.GetBytes(fl_val);

                        if (IsLittleEndianWordOrder(item.Item)) {
                            // badc
                            writeBuffer_float[13] = bytes[1];
                            writeBuffer_float[14] = bytes[0];
                            writeBuffer_float[15] = bytes[3];
                            writeBuffer_float[16] = bytes[2];
                        }
                        else // big-endian
                        {
                            // dcba
                            writeBuffer_float[13] = bytes[3];
                            writeBuffer_float[14] = bytes[2];
                            writeBuffer_float[15] = bytes[1];
                            writeBuffer_float[16] = bytes[0];
                        }

                        // put Modbus message together
                        WriteUShort(writeBuffer_float, 0, (ushort)(i + 1000)); // Transaction-ID
                        WriteUShort(writeBuffer_float, 2, 0); // Protocol-ID
                        WriteUShort(writeBuffer_float, 4, length); // length
                        writeBuffer_float[6] = GetModbusHeaderAddress(config, item.Item);
                        writeBuffer_float[7] = funcCode; // function code
                        WriteUShort(writeBuffer_float, 8, (ushort)(address.Start - 1)); // start address
                        writeBuffer_float[10] = 0; // number of registers hi byte
                        writeBuffer_float[11] = 2; // number of registers lo byte
                        writeBuffer_float[12] = 4; // number of bytes until end of message

                    }
                    else {
                        length = 6; // message length
                        funcCode = 6; // function code
                        // value to write
                        ushort val = (ushort)value.V.GetValue(dt: item.Item.Type, dimension: item.Item.Dimension)!;

                        // put Modbus message together
                        WriteUShort(writeBuffer, 0, (ushort)(i + 1000)); // Transaction-ID
                        WriteUShort(writeBuffer, 2, 0); // Protocol-ID
                        WriteUShort(writeBuffer, 4, length); // length
                        writeBuffer[6] = GetModbusHeaderAddress(config, item.Item);
                        writeBuffer[7] = funcCode; // function code
                        WriteUShort(writeBuffer, 8, (ushort)(address.Start - 1)); // start address
                        WriteUShort(writeBuffer, 10, val);
                    }

                    try {
                        if (item.Item.Type == DataType.Float32) {
                            //PrintLine("Sending write request: " + BitConverter.ToString(writeBuffer_float));
                            await networkStream.WriteAsync(writeBuffer_float);
                            await ConsumeWriteResponse(networkStream);
                            //PrintLine("Response received for write request: " + BitConverter.ToString(writeBuffer_float));
                        }
                        else {
                            //PrintLine("Sending write request: " + BitConverter.ToString(writeBuffer));
                            await networkStream.WriteAsync(writeBuffer);
                            await ConsumeWriteResponse(networkStream);
                            //PrintLine("Response received for write request: " + BitConverter.ToString(writeBuffer));
                        }
                    }
                    catch (Exception exp) {
                        Exception e = exp.GetBaseException() ?? exp;

                        if (e is ModbusException) {
                            // keep connection
                        }
                        else {
                            CloseConnection();
                        }

                        if (listFailed == null) {
                            listFailed = new List<FailedDataItemWrite>();
                        }
                        listFailed.Add(new FailedDataItemWrite(id, exp.Message));
                        CloseConnection();
                    }
                }
                else {
                    if (listFailed == null) {
                        listFailed = new List<FailedDataItemWrite>();
                    }
                    listFailed.Add(new FailedDataItemWrite(id, $"No writeable data item with id '{id}' found."));
                }
            }

            if (listFailed == null)
                return WriteDataItemsResult.OK;
            else
                return WriteDataItemsResult.Failure(listFailed.ToArray());
        }

        private void PrintLine(string msg) {
            string name = config?.Name ?? "";
            Console.WriteLine(name + ": " + msg);
        }

        private void LogWarn(string type, string msg, string[]? dataItems = null, string? details = null) {

            var ae = new AdapterAlarmOrEvent() {
                Time = Timestamp.Now,
                Severity = Severity.Warning,
                Type = type,
                Message = msg,
                Details = details ?? "",
                AffectedDataItems = dataItems ?? new string[0]
            };

            callback?.Notify_AlarmOrEvent(ae);
        }

        private void ReturnToNormal(string type, string msg, params string[] affectedDataItems) {
            callback?.Notify_AlarmOrEvent(AdapterAlarmOrEvent.MakeReturnToNormal(type, msg, affectedDataItems));
        }

        private void LogError(string type, string msg, string[]? dataItems = null, string? details = null) {

            var ae = new AdapterAlarmOrEvent() {
                Time = Timestamp.Now,
                Severity = Severity.Alarm,
                Type = type,
                Message = msg,
                Details = details ?? "",
                AffectedDataItems = dataItems ?? new string[0]
            };

            callback?.Notify_AlarmOrEvent(ae);
        }
    }

    internal class ItemInfo
    {
        public DataItem Item { get; private set; }
        public ModbusAddress Address { get; private set; }

        public ItemInfo(DataItem item, ModbusAddress address) {
            Item = item;
            Address = address;
        }
    }

    public class ModbusAddress
    {
        public ushort Start { get; set; }
        public ushort Count { get; set; }

        public ModbusAddress(ushort startRegister, ushort count) {
            Start = startRegister;
            Count = count;
        }

        public static ModbusAddress Make(int startRegister, int count) {
            if (startRegister < 1) throw new Exception("Modbus register start at 1");
            if (startRegister > 0xFFFF) throw new Exception("Modbus register must be smaller than 0xFFFF");
            if (count < 1) throw new Exception("Count must be greater than 0");
            if (count > 0xFFFF) throw new Exception("Count must be smaller than 0xFFFF");
            return new ModbusAddress((ushort)startRegister, (ushort)count);
        }
    }

    public sealed class ModbusException : Exception {
        public ModbusException(string message) : base(message) { }
    }
}
