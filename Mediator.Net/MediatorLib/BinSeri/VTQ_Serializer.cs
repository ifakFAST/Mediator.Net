using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ifak.Fast.Mediator.BinSeri
{
    public static class VTQ_Serializer
    {
        internal const byte Code = 88;
        private const byte Version = 1;

        public static void Serialize(Stream stream, List<VTQ> vtqs) {

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {

                int N = vtqs.Count;
                writer.Write(Code);
                writer.Write(Version);
                writer.Write(N);
                if (N == 0) return;

                long timeBase = vtqs[0].T.JavaTicks;
                long diffBase = N == 1 ? 0 : vtqs[1].T.JavaTicks - timeBase;
                string valBase = vtqs[0].V.JSON;

                timeBase -= diffBase;
                writer.Write(timeBase);
                writer.Write(diffBase);
                writer.Write(valBase);

                byte[] codeTable = Common.mCodeTable;

                for (int k = 0; k < N; ++k) {

                    VTQ vtq = vtqs[k];

                    int control = (int)vtq.Q;
                    long time = vtq.T.JavaTicks;
                    string val = vtq.V.JsonOrNull ?? "";
                    long diff = time - timeBase;

                    int valLen = val.Length;
                    int bytesComapctVal = (valLen + 1) / 2;

                    bool compactStr = true;
                    bool writeStr = true;

                    if (val == valBase) {
                        control |= 0x04;
                        writeStr = false;
                    }
                    else {

                        if (bytesComapctVal > 0xFF) {
                            compactStr = false;
                            control |= 0x08;
                        }
                        else {
                            for (int i = 0; i < valLen; i++) {
                                int c = val[i];
                                if ((c & 0xFF80) != 0 || codeTable[c] == 0xFF) {
                                    compactStr = false;
                                    control |= 0x08;
                                    break;
                                }
                            }
                        }
                    }

                    if (diff == diffBase) {
                        control |= 0x10;
                        writer.Write((byte)control);
                    }
                    else {

                        if (diff % 1000L == 0) {
                            control |= 0x20;
                            diff /= 1000L;
                        }

                        if (diff <= sbyte.MaxValue && diff >= sbyte.MinValue) {
                            // 1 Byte
                            writer.Write((byte)control);
                            writer.Write((sbyte)diff);
                        }
                        else if (diff <= short.MaxValue && diff >= short.MinValue) {
                            control |= 0x40; // 2 Byte
                            writer.Write((byte)control);
                            writer.Write((short)diff);
                        }
                        else if (diff <= int.MaxValue && diff >= int.MinValue) {
                            control |= 0x80; // 4 Byte
                            writer.Write((byte)control);
                            writer.Write((int)diff);
                        }
                        else {
                            control |= 0xC0; // 8 Byte
                            writer.Write((byte)control);
                            writer.Write(diff);
                        }
                    }

                    if (writeStr) {

                        if (compactStr) {

                            writer.Write((byte)bytesComapctVal);

                            for (int i = 0; i < valLen; i += 2) {
                                char c0 = val[i];
                                int x = (codeTable[c0] << 4);
                                if (i + 1 < valLen) {
                                    char c1 = val[i + 1];
                                    x |= codeTable[c1];
                                }
                                else {
                                    x |= 0x0F;
                                }
                                writer.Write((byte)x);
                            }
                        }
                        else {
                            writer.Write(val);
                        }
                    }

                    timeBase = time;
                    diffBase = diff;
                    valBase = val;
                }
            }
        }

        public static List<VTQ> Deserialize(Stream stream) {

            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true)) {

                if (reader.ReadByte() != Code) throw new IOException("Failed to deserialize VTQ[]: Wrong start byte");
                if (reader.ReadByte() != Version) throw new IOException("Failed to deserialize VTQ[]: Wrong version byte");

                int N = reader.ReadInt32();
                var res = new List<VTQ>(N);

                if (N == 0) return res;

                long timeBase = reader.ReadInt64();
                long diffBase = reader.ReadInt64();
                string valBase = reader.ReadString();

                char[] buffer = new char[255];
                char[] mapCode2Char = Common.mapCode2Char;

                for (int k = 0; k < N; ++k) {

                    int control = reader.ReadByte();

                    Quality q = (Quality)(control & 0x03);
                    long time = timeBase;
                    long diff = diffBase;
                    string val = valBase;

                    if ((control & 0x10) == 0) {

                        long diffFactor = ((control & 0x20) == 0) ? 1 : 1000;

                        int codeByteCount = (control & 0xC0) >> 6;
                        if (codeByteCount == 0) {
                            diff = diffFactor * reader.ReadSByte();
                        }
                        else if (codeByteCount == 1) {
                            diff = diffFactor * reader.ReadInt16();
                        }
                        else if (codeByteCount == 2) {
                            diff = diffFactor * reader.ReadInt32();
                        }
                        else if (codeByteCount == 3) {
                            diff = diffFactor * reader.ReadInt64();
                        }
                    }
                    time += diff;

                    if ((control & 0x04) == 0) {

                        if ((control & 0x08) == 0) {
                            int countBytes = reader.ReadByte();
                            int j = 0;
                            for (int i = 0; i < countBytes; i++) {
                                int b = reader.ReadByte();
                                int b0 = (0xF0 & b) >> 4;
                                int b1 = (0x0F & b);
                                buffer[j++] = mapCode2Char[b0];
                                if (b1 == 0x0F) break;
                                buffer[j++] = mapCode2Char[b1];
                            }
                            val = new string(buffer, 0, j);
                        }
                        else {
                            val = reader.ReadString();
                        }
                    }

                    res.Add(VTQ.Make(DataValue.FromJSON(val), Timestamp.FromJavaTicks(time), q));

                    timeBase = time;
                    diffBase = diff;
                    valBase = val;
                }
                return res;
            }
        }
    }
}
