// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO.Adapter_Modbus
{
    [Identify("ModbusTCP")]
    public class ModbusTCP : AdapterBase
    {
        private TcpClient connection = null;
        private NetworkStream networkStream = null;
        private Adapter config;
        private AdapterCallback callback;
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

        protected virtual byte GetModbusFunctionCode(Adapter adapter, DataItem item) => 4; // 4 = Read Input Register

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

            if (string.IsNullOrWhiteSpace(config.Address)) return false;

            try {

                var (host, port) = GetHostAndPort(config);

                connection = new TcpClient();
                connection.SendTimeout = TimeOutMS;
                connection.ReceiveTimeout = TimeOutMS;
                await connection.ConnectAsync(host, port);
                networkStream = connection.GetStream();

                this.mapId2Info = config.GetAllDataItems().Where(di => !string.IsNullOrEmpty(di.Address)).ToDictionary(
                    item => /* key */ item.ID,
                    item => /* val */ new ItemInfo(item, GetModbusAddress(item)));

                return true;
            }
            catch (Exception exp) {
                Exception baseExp = exp.GetBaseException() ?? exp;
                LogWarn("Connect", "Connection error: " + baseExp.Message, details: baseExp.StackTrace);
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

        public override Task<string[]> BrowseDataItemAddress(string idOrNull) {
            return Task.FromResult(new string[0]);
        }

        public override async Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {

            int N = items.Count;

            if (!await TryConnect()) {
                return GetBadVTQs(items);
            }

            VTQ[] vtqs = new VTQ[N];

            byte[] writeBuffer = new byte[7+5]; // 7: Header, 5: PDU

            for (int i = 0; i < N; ++i) {
                ReadRequest request = items[i];
                if (mapId2Info.ContainsKey(request.ID)) {
                    ItemInfo item = mapId2Info[request.ID];
                    ModbusAddress address = item.Address;
                    WriteUShort(writeBuffer, 0, (ushort)i); // Transaction-ID
                    WriteUShort(writeBuffer, 2, 0); // Protocol-ID
                    WriteUShort(writeBuffer, 4, 6); // Length
                    writeBuffer[6] = GetModbusHeaderAddress(config, item.Item);
                    writeBuffer[7] = GetModbusFunctionCode(config, item.Item);
                    WriteUShort(writeBuffer, 8, (ushort)(address.Start - 1));
                    WriteUShort(writeBuffer, 10, address.Count);
                    try {
                        await networkStream.WriteAsync(writeBuffer);
                        ushort[] words = await ReadResponse(address.Count);
                        vtqs[i] = ParseModbusResponse(item.Item, words, Timestamp.Now);
                    }
                    catch (Exception exp) {
                        Exception e = exp.GetBaseException() ?? exp;
                        LogWarn("ReadExcept", $"Failed to read item {item.Item.Name}: {e.Message}");
                        vtqs[i] = VTQ.Make(request.LastValue.V, Timestamp.Now, Quality.Bad);
                        CloseConnection();
                    }
                }
                else {
                    vtqs[i] = VTQ.Make(request.LastValue.V, Timestamp.Now, Quality.Bad);
                }
            }

            return vtqs;
        }

        private async Task<ushort[]> ReadResponse(int wordCount) {

            const int ResponseHeadLen = 9;
            int ResponseLen = ResponseHeadLen + 2 * wordCount;
            byte[] readBuffer = new byte[ResponseLen];

            int readCount = await networkStream.ReadAsync(readBuffer, 0, ResponseLen);
            if (readCount == 0) throw new Exception("Failed to read response.");

            while (readCount < ResponseLen) {
                int responseInc = await networkStream.ReadAsync(readBuffer, readCount, ResponseLen - readCount);
                if (responseInc == 0)
                    throw new Exception("Failed to read response."); ;
                readCount += responseInc;
            }

            ushort[] res = new ushort[wordCount];
            int off = ResponseHeadLen;
            for (int i = 0; i < wordCount; ++i) {
                res[i] = (ushort)(((readBuffer[off] & 0xFF) << 8) | ((readBuffer[off+1] & 0xFF)));
                off += 2;
            }
            return res;
        }

        private static void WriteUShort(byte[] bytes, int offset, ushort value) {
            bytes[offset]   = (byte)((value & 0xFF00) >> 8);
            bytes[offset+1] = (byte)(value & 0x00FF);
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

        public override Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> values, Duration? timeout) {
            var failed = values.Select(div => new FailedDataItemWrite(div.ID, "Write not implemented")).ToArray();
            return Task.FromResult(WriteDataItemsResult.Failure(failed));
        }

        private void PrintLine(string msg) {
            Console.WriteLine(config.Name + ": " + msg);
        }

        private void LogWarn(string type, string msg, string[] dataItems = null, string details = null) {

            var ae = new AdapterAlarmOrEvent() {
                Time = Timestamp.Now,
                Severity = Severity.Warning,
                Type = type,
                Message = msg,
                Details = details ?? "",
                AffectedDataItems = dataItems ?? new string[0]
            };

            callback.Notify_AlarmOrEvent(ae);
        }

        private void LogError(string type, string msg, string[] dataItems = null, string details = null) {

            var ae = new AdapterAlarmOrEvent() {
                Time = Timestamp.Now,
                Severity = Severity.Alarm,
                Type = type,
                Message = msg,
                Details = details ?? "",
                AffectedDataItems = dataItems ?? new string[0]
            };

            callback.Notify_AlarmOrEvent(ae);
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
}
