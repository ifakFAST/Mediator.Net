using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OpcUaServerNet {

    public class OPC_UA_Server {

        private IntPtr pServer;
        private readonly string appName;
        private readonly ushort port;

        private bool startUpCompletedOK = false;

        private OPC_UA_Server(IntPtr pServer, string appName, ushort port) {
            this.pServer = pServer;
            this.appName = appName;
            this.port = port;
        }

        public bool NodeExists(string nodeId) {
            return mapNodeIDs.ContainsKey(nodeId);
        }

        public void AddFolderNode(string nodeId, string name, string? parentNodeId) {

            if (pServer == IntPtr.Zero) {
                throw new Exception($"AddFolderNode failed: OPC UA server is closed!");
            }

            IntPtr parentNode = IntPtr.Zero;
            if (parentNodeId != null) {
                if (!mapNodeIDs.TryGetValue(parentNodeId, out parentNode)) {
                    throw new Exception($"Faild to add folder {nodeId}. Parent node {parentNodeId} not found.");
                }
            }

            IntPtr pNodeId = IntPtr.Zero;
            uint res = UA_Server_AddFolderNode(pServer, parentNode, GetUTF8NullTerminated(nodeId), GetUTF8NullTerminated(name), ref pNodeId);
            if (res != 0) {
                throw new Exception($"Faild to add folder {nodeId}, Error: {res:X}");
            }
            mapNodeIDs[nodeId] = pNodeId;
        }

        private static byte[] GetUTF8NullTerminated(string s) {
            int byteCount = Encoding.UTF8.GetByteCount(s);
            byte[] array = new byte[byteCount + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, array, 0);
            array[byteCount] = 0;
            return array;
        }

        private static byte[] GetUTF8(string s) {
            int byteCount = Encoding.UTF8.GetByteCount(s);
            byte[] array = new byte[byteCount];
            Encoding.UTF8.GetBytes(s, 0, s.Length, array, 0);
            return array;
        }

        public static OPC_UA_Server Create(string appName, string host = "", ushort port = 4840, LogLevel logLevel = LogLevel.Info, bool allowAnonymous = true, string user = "", string pass = "", string fileCertificate = "", string fileKey = "") {

            byte[] certificate = new byte[0];
            byte[] key = new byte[0];

            if (!string.IsNullOrEmpty(fileCertificate)) {
                fileCertificate = Path.GetFullPath(fileCertificate);
                if (!File.Exists(fileCertificate)) {
                    throw new FileNotFoundException($"Certificate file not found: {fileCertificate}");
                }
                certificate = File.ReadAllBytes(fileCertificate);
            }

            if (!string.IsNullOrEmpty(fileKey)) {
                if (!File.Exists(fileKey)) {
                    throw new FileNotFoundException($"Certificate key file not found: {fileKey}");
                }
                key = File.ReadAllBytes(fileKey);
            }

            IntPtr pServer = UA_Server_Create(
                GetUTF8NullTerminated(appName),
                GetUTF8NullTerminated(host),
                port,
                logLevel,
                allowAnonymous,
                GetUTF8NullTerminated(user),
                GetUTF8NullTerminated(pass),
                certificate,
                (uint)certificate.Length,
                key,
                (uint)key.Length);

            if (pServer == IntPtr.Zero) {
                throw new Exception($"Failed to create OPC UA server {appName} on port {port}");
            }
            return new OPC_UA_Server(pServer, appName, port);
        }

        private Dictionary<string, IntPtr> mapNodeIDs = new Dictionary<string, IntPtr>();

        private delegate uint AddVariableDelegate<T>(IntPtr server, IntPtr parentNode, byte[] nodeIdUtf8, byte[] nameUtf8, bool writable, T value, ushort type, ref IntPtr nodeId);

        private void AddVariableNode<T>(string nodeId, string name, bool writable, T initialValue, AddVariableDelegate<T> addVariableMethod, string? parentNodeId) {
            if (pServer == IntPtr.Zero) {
                throw new Exception($"AddVariableNode failed: OPC UA server is closed!");
            }

            IntPtr parentNode = IntPtr.Zero;
            if (parentNodeId != null) {
                if (!mapNodeIDs.TryGetValue(parentNodeId, out parentNode)) {
                    throw new Exception($"Faild to add variable {nodeId}. Parent node {parentNodeId} not found.");
                }
            }

            IntPtr pNodeId = IntPtr.Zero;
            uint res = addVariableMethod(pServer, parentNode, GetUTF8NullTerminated(nodeId), GetUTF8NullTerminated(name), writable, initialValue, (ushort)UaVariableType.BaseDataVariableType, ref pNodeId);
            if (res != 0) {
                throw new Exception($"Faild to add variable {nodeId}, Error: {res:X}");
            }
            mapNodeIDs[nodeId] = pNodeId;
        }

        private delegate uint AddVariableStringDelegate(IntPtr server, IntPtr parentNode, byte[] nodeIdUtf8, byte[] nameUtf8, bool writable, byte[] value, uint len, ushort type, ref IntPtr nodeId);

        private void AddVariableNodeBytes(string nodeId, string name, bool writable, byte[] initialValue, AddVariableStringDelegate addVariableMethod, string? parentNodeId) {
            if (pServer == IntPtr.Zero) {
                throw new Exception($"AddVariableNode failed: OPC UA server is closed!");
            }
            
            IntPtr parentNode = IntPtr.Zero;
            if (parentNodeId != null) {
                if (!mapNodeIDs.TryGetValue(parentNodeId, out parentNode)) {
                    throw new Exception($"Faild to add variable {nodeId}. Parent node {parentNodeId} not found.");
                }
            }

            IntPtr pNodeId = IntPtr.Zero;
            uint res = addVariableMethod(pServer, parentNode, GetUTF8NullTerminated(nodeId), GetUTF8NullTerminated(name), writable, initialValue, (uint)initialValue.Length, (ushort)UaVariableType.BaseDataVariableType, ref pNodeId);
            if (res != 0) {
                throw new Exception($"Faild to add variable {nodeId}, Error: {res:X}");
            }
            mapNodeIDs[nodeId] = pNodeId;
        }

        public void AddVariableNode_Boolean(string nodeId, string name, bool writable, bool initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, initialValue, UA_Server_AddVariable_Boolean, parentNodeId);
        }

        public void AddVariableNode_SByte(string nodeId, string name, bool writable, sbyte initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, initialValue, UA_Server_AddVariable_SByte, parentNodeId);
        }

        public void AddVariableNode_Byte(string nodeId, string name, bool writable, byte initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, initialValue, UA_Server_AddVariable_Byte, parentNodeId);
        }

        public void AddVariableNode_Int16(string nodeId, string name, bool writable, short initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, initialValue, UA_Server_AddVariable_Int16, parentNodeId);
        }

        public void AddVariableNode_UInt16(string nodeId, string name, bool writable, ushort initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, initialValue, UA_Server_AddVariable_UInt16, parentNodeId);
        }

        public void AddVariableNode_Int32(string nodeId, string name, bool writable, int initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, initialValue, UA_Server_AddVariable_Int32, parentNodeId);
        }

        public void AddVariableNode_UInt32(string nodeId, string name, bool writable, uint initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, initialValue, UA_Server_AddVariable_UInt32, parentNodeId);
        }

        public void AddVariableNode_Int64(string nodeId, string name, bool writable, long initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, initialValue, UA_Server_AddVariable_Int64, parentNodeId);
        }

        public void AddVariableNode_UInt64(string nodeId, string name, bool writable, ulong initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, initialValue, UA_Server_AddVariable_UInt64, parentNodeId);
        }

        public void AddVariableNode_Float(string nodeId, string name, bool writable, float initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, initialValue, UA_Server_AddVariable_Float, parentNodeId);
        }

        public void AddVariableNode_Double(string nodeId, string name, bool writable, double initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, initialValue, UA_Server_AddVariable_Double, parentNodeId);
        }

        public void AddVariableNode_String(string nodeId, string name, bool writable, string initialValue, string? parentNodeId = null) {
            AddVariableNodeBytes(nodeId, name, writable, GetUTF8(initialValue), UA_Server_AddVariable_String, parentNodeId);
        }

        public void AddVariableNode_DateTime(string nodeId, string name, bool writable, DateTime initialValue, string? parentNodeId = null) {
            AddVariableNode(nodeId, name, writable, DateTimeToOpcUaTicks(initialValue), UA_Server_AddVariable_DateTime, parentNodeId);
        }

        public void AddVariableNode_Guid(string nodeId, string name, bool writable, Guid initialValue, string? parentNodeId = null) {
            AddVariableNodeBytes(nodeId, name, writable, initialValue.ToByteArray(), UA_Server_AddVariable_Guid, parentNodeId);
        }

        public void AddVariableNode_ByteString(string nodeId, string name, bool writable, byte[] initialValue, string? parentNodeId = null) {
            AddVariableNodeBytes(nodeId, name, writable, initialValue, UA_Server_AddVariable_ByteString, parentNodeId);
        }


        public void RunStartup() {
            if (pServer == IntPtr.Zero) {
                throw new Exception($"RunStartup failed: OPC UA server is closed!");
            }
            uint res = UA_Server_RunStartup(pServer);
            if (res != 0) {
                UA_Server_Delete(pServer);
                pServer = IntPtr.Zero;
                throw new Exception($"Failed to start OPC UA server {appName} on port {port}. Error: {res:X}");
            }
            startUpCompletedOK = true;
        }

        public void RunStep() {
            if (pServer == IntPtr.Zero) {
                throw new Exception($"RunStep failed: OPC UA server is closed!");
            }
            UA_Server_RunIterate(pServer, waitInternal: true);
        }

        
        private static long DateTimeToOpcUaTicks(DateTime dateTime) {
            DateTime referenceDate = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan duration = dateTime.ToUniversalTime() - referenceDate;
            return duration.Ticks;
        }

        public static DateTime OpcUaTicksToDateTime(long ticks) {
            DateTime referenceDate = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return referenceDate.AddTicks(ticks);
        }


        private delegate uint WriteVariableDelegate<T>(IntPtr server, IntPtr nodeId, T value, int quality, long time);

        private void WriteVariableValue<T>(string nodeId, T value, DateTime time, UA_Quality quality, WriteVariableDelegate<T> writeVariableMethod) {
            if (pServer == IntPtr.Zero) {
                throw new Exception($"WriteVariableValue failed: OPC UA server is closed!");
            }

            if (!mapNodeIDs.TryGetValue(nodeId, out IntPtr pNodeId)) {
                throw new Exception($"Faild to write OPC UA variable {nodeId}. NodeId not found.");
            }

            uint res = writeVariableMethod(pServer, pNodeId, value, (int)quality, DateTimeToOpcUaTicks(time));

            if (res != 0) {
                throw new Exception($"Faild to write OPC UA variable {nodeId}. Error: {res:X}");
            }
        }

        private delegate uint WriteByteArrayVariableDelegate(IntPtr server, IntPtr nodeId, byte[] value, uint len, int quality, long time);

        private void WriteByteArrayVariableValue(string nodeId, byte[] value, DateTime time, UA_Quality quality, WriteByteArrayVariableDelegate writeVariableMethod) {
            if (pServer == IntPtr.Zero) {
                throw new Exception($"WriteVariableValue failed: OPC UA server is closed!");
            }

            if (!mapNodeIDs.TryGetValue(nodeId, out IntPtr pNodeId)) {
                throw new Exception($"Faild to write OPC UA variable {nodeId}. NodeId not found.");
            }

            uint res = writeVariableMethod(pServer, pNodeId, value, (uint)value.Length, (int)quality, DateTimeToOpcUaTicks(time));

            if (res != 0) {
                throw new Exception($"Faild to write OPC UA variable {nodeId}. Error: {res:X}");
            }
        }

        public void WriteVariableValue_Boolean(string nodeId, bool value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_Boolean);
        }

        public void WriteVariableValue_SByte(string nodeId, sbyte value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_SByte);
        }

        public void WriteVariableValue_Byte(string nodeId, byte value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_Byte);
        }

        public void WriteVariableValue_Int16(string nodeId, short value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_Int16);
        }

        public void WriteVariableValue_UInt16(string nodeId, ushort value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_UInt16);
        }

        public void WriteVariableValue_Int32(string nodeId, int value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_Int32);
        }

        public void WriteVariableValue_UInt32(string nodeId, uint value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_UInt32);
        }

        public void WriteVariableValue_Int64(string nodeId, long value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_Int64);
        }

        public void WriteVariableValue_UInt64(string nodeId, ulong value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_UInt64);
        }

        public void WriteVariableValue_Float(string nodeId, float value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_Float);
        }

        public void WriteVariableValue_Double(string nodeId, double value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_Double);
        }

        public void WriteVariableValue_String(string nodeId, string value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            byte[] bytes = GetUTF8(value);
            WriteByteArrayVariableValue(nodeId, bytes, time, quality, UA_Server_WriteVariable_String);
        }

        public void WriteVariableValue_DateTime(string nodeId, DateTime value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteVariableValue(nodeId, DateTimeToOpcUaTicks(value), time, quality, UA_Server_WriteVariable_DateTime);
        }

        public void WriteVariableValue_Guid(string nodeId, Guid value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            byte[] bytes = value.ToByteArray();
            WriteByteArrayVariableValue(nodeId, bytes, time, quality, UA_Server_WriteVariable_Guid);
        }

        public void WriteVariableValue_ByteString(string nodeId, byte[] value, DateTime time, UA_Quality quality = UA_Quality.Good) {
            WriteByteArrayVariableValue(nodeId, value, time, quality, UA_Server_WriteVariable_ByteString);
        }


        private delegate uint ReadVariableDelegate<T>(IntPtr server, IntPtr nodeId, ref T value, ref int quality, ref long timestamp);

        private T ReadVariableValue<T>(string nodeId, out UA_Quality quality, out DateTime timestamp, ReadVariableDelegate<T> readVariableMethod) where T: struct {
            if (pServer == IntPtr.Zero) {
                throw new Exception($"ReadVariableValue failed: OPC UA server is closed!");
            }

            if (!mapNodeIDs.TryGetValue(nodeId, out IntPtr pNodeId)) {
                throw new Exception($"Faild to read OPC UA variable {nodeId}. NodeId not found.");
            }

            quality = UA_Quality.Bad;

            T value = default;
            int q = (int)UA_Quality.Good;

            long time = 0;
            uint res = readVariableMethod(pServer, pNodeId, ref value, ref q, ref time);

            if (res != 0) {
                throw new Exception($"Failed to read value for OPC UA variable {nodeId}. Error: {res:X}");
            }

            quality = (UA_Quality)q;
            timestamp = OpcUaTicksToDateTime(time);

            return value;
        }

        private delegate uint ReadVariableBytesDelegate(IntPtr server, IntPtr nodeId, byte[] value, uint capacity, ref uint strLen, ref int quality, ref long timestamp);

        private byte[] ReadVariableValueBytes(string nodeId, out UA_Quality quality, out DateTime timestamp, ReadVariableBytesDelegate readVariableMethod) {
            if (pServer == IntPtr.Zero) {
                throw new Exception($"ReadVariableValue failed: OPC UA server is closed!");
            }

            if (!mapNodeIDs.TryGetValue(nodeId, out IntPtr pNodeId)) {
                throw new Exception($"Faild to read OPC UA variable {nodeId}. NodeId not found.");
            }

            quality = UA_Quality.Bad;

            byte[] bytes = new byte[48];
            int q = (int)UA_Quality.Good;

            long time = 0;
            uint strLen = 0;
            uint res = readVariableMethod(pServer, pNodeId, bytes, (uint)bytes.Length, ref strLen, ref q, ref time);

            if (res != 0) {
                throw new Exception($"Failed to read value for OPC UA variable {nodeId}. Error: {res:X}");
            }

            if (strLen > bytes.Length) {
                bytes = new byte[strLen];
                res = readVariableMethod(pServer, pNodeId, bytes, (uint)bytes.Length, ref strLen, ref q, ref time);
                if (res != 0) {
                    throw new Exception($"Failed to read value for OPC UA variable {nodeId}. Error: {res:X}");
                }
            }

            quality = (UA_Quality)q;
            timestamp = OpcUaTicksToDateTime(time);

            return SliceByteArray(bytes, strLen);
        }

        private static byte[] SliceByteArray(byte[] array, uint takeFirst) {
            if (array.Length <= takeFirst) {
                return array;
            }
            byte[] result = new byte[takeFirst];
            Array.Copy(array, result, takeFirst);
            return result;
        }

        public bool ReadVariableValue_Boolean(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValue<bool>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_Boolean);
        }

        public sbyte ReadVariableValue_SByte(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValue<sbyte>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_SByte);
        }

        public byte ReadVariableValue_Byte(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValue<byte>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_Byte);
        }

        public short ReadVariableValue_Int16(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValue<short>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_Int16);
        }

        public ushort ReadVariableValue_UInt16(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValue<ushort>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_UInt16);
        }

        public int ReadVariableValue_Int32(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValue<int>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_Int32);
        }

        public uint ReadVariableValue_UInt32(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValue<uint>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_UInt32);
        }

        public long ReadVariableValue_Int64(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValue<long>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_Int64);
        }

        public ulong ReadVariableValue_UInt64(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValue<ulong>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_UInt64);
        }

        public float ReadVariableValue_Float(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValue<float>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_Float);
        }

        public double ReadVariableValue_Double(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValue<double>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_Double);
        }

        public string ReadVariableValue_String(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            byte[] bytes = ReadVariableValueBytes(nodeId, out quality, out timestamp, UA_Server_ReadVariable_String);
            return Encoding.UTF8.GetString(bytes);
        }

        public DateTime ReadVariableValue_DateTime(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            long dt = ReadVariableValue<long>(nodeId, out quality, out timestamp, UA_Server_ReadVariable_DateTime);
            return OpcUaTicksToDateTime(dt);
        }

        public Guid ReadVariableValue_Guid(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            byte[] bytes = ReadVariableValueBytes(nodeId, out quality, out timestamp, UA_Server_ReadVariable_Guid);
            return new Guid(bytes);
        }

        public byte[] ReadVariableValue_ByteString(string nodeId, out UA_Quality quality, out DateTime timestamp) {
            return ReadVariableValueBytes(nodeId, out quality, out timestamp, UA_Server_ReadVariable_ByteString);
        }



        public void Close() {

            if (pServer == IntPtr.Zero) return;

            if (startUpCompletedOK) {
                UA_Server_RunShutdown(pServer);
            }

            foreach (IntPtr nodeId in mapNodeIDs.Values) {
                UA_Server_FreeNodeId(nodeId);
            }
            mapNodeIDs.Clear();

            UA_Server_Delete(pServer);
            pServer = IntPtr.Zero;
        }

        public enum LogLevel: byte {
            Trace = 0,
            Debug,
            Info,
            Warning,
            Error,
            Fatal
        }

        [DllImport("OpcUaServerNative.dll")]
        private static extern IntPtr UA_Server_Create(byte[] name, byte[] host, ushort port, LogLevel logLevel, bool allowAnonymous, byte[] user, byte[] pass, byte[] certificate, uint lenCertificate, byte[] key, uint lenKey);


        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_Boolean(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, bool initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_SByte(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, sbyte initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_Byte(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, byte initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_Int16(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, short initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_UInt16(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, ushort initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_Int32(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, int initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_UInt32(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, uint initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_Int64(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, long initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_UInt64(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, ulong initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_Float(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, float initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_Double(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, double initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_String(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, byte[] initialValue, uint initialValueLength, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_DateTime(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, long initialValue, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_Guid(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, byte[] initialValue, uint initialValueLength, ushort variableTypeId, ref IntPtr pNodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddVariable_ByteString(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, bool writable, byte[] initialValue, uint initialValueLength, ushort variableTypeId, ref IntPtr pNodeId);


        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_AddFolderNode(IntPtr server, IntPtr parentNode, byte[] id, byte[] name, ref IntPtr pNodeId);


        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_Boolean(IntPtr server, IntPtr pNodeId, bool value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_SByte(IntPtr server, IntPtr pNodeId, sbyte value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_Byte(IntPtr server, IntPtr pNodeId, byte value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_Int16(IntPtr server, IntPtr pNodeId, short value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_UInt16(IntPtr server, IntPtr pNodeId, ushort value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_Int32(IntPtr server, IntPtr pNodeId, int value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_UInt32(IntPtr server, IntPtr pNodeId, uint value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_Int64(IntPtr server, IntPtr pNodeId, long value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_UInt64(IntPtr server, IntPtr pNodeId, ulong value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_Float(IntPtr server, IntPtr pNodeId, float value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_Double(IntPtr server, IntPtr pNodeId, double value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_String(IntPtr server, IntPtr pNodeId, byte[] value, uint size, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_DateTime(IntPtr server, IntPtr pNodeId, long value, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_Guid(IntPtr server, IntPtr pNodeId, byte[] value, uint size, int quality, long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_WriteVariable_ByteString(IntPtr server, IntPtr pNodeId, byte[] value, uint size, int quality, long time);


        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_Boolean(IntPtr server, IntPtr pNodeId, ref bool value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_SByte(IntPtr server, IntPtr pNodeId, ref sbyte value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_Byte(IntPtr server, IntPtr pNodeId, ref byte value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_Int16(IntPtr server, IntPtr pNodeId, ref short value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_UInt16(IntPtr server, IntPtr pNodeId, ref ushort value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_Int32(IntPtr server, IntPtr pNodeId, ref int value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_UInt32(IntPtr server, IntPtr pNodeId, ref uint value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_Int64(IntPtr server, IntPtr pNodeId, ref long value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_UInt64(IntPtr server, IntPtr pNodeId, ref ulong value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_Float(IntPtr server, IntPtr pNodeId, ref float value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_Double(IntPtr server, IntPtr pNodeId, ref double value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_String(IntPtr server, IntPtr pNodeId, byte[] value, uint capacity, ref uint strLen, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_DateTime(IntPtr server, IntPtr pNodeId, ref long value, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_Guid(IntPtr server, IntPtr pNodeId, byte[] value, uint capacity, ref uint strLen, ref int quality, ref long time);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_ReadVariable_ByteString(IntPtr server, IntPtr pNodeId, byte[] value, uint capacity, ref uint strLen, ref int quality, ref long time);


        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_RunStartup(IntPtr server);

        [DllImport("OpcUaServerNative.dll")]
        private static extern ushort UA_Server_RunIterate(IntPtr server, bool waitInternal);

        [DllImport("OpcUaServerNative.dll")]
        private static extern uint UA_Server_RunShutdown(IntPtr server);

        [DllImport("OpcUaServerNative.dll")]
        private static extern void UA_Server_FreeNodeId(IntPtr nodeId);

        [DllImport("OpcUaServerNative.dll")]
        private static extern void UA_Server_Delete(IntPtr server);
    }

    public enum UA_Quality {
        Good = 1,
        Bad = 2,
        Uncertain = 3
    }

    enum UaVariableType {
        BaseDataVariableType = 63,
        AnalogItemType = 2368,
    }
}
