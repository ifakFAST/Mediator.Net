using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ifak.Fast.Mediator.BinSeri
{
    public static class VTTQ_Serializer
    {
        internal const byte Code = 89;

        public static void Serialize(Stream stream, List<VTTQ> vtqs, byte binaryVersion) {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                Serialize(writer, vtqs, binaryVersion);
            }
        }

        public static void Serialize(BinaryWriter writer, List<VTTQ> vtqs, byte binaryVersion) {

            int N = vtqs.Count;
            writer.Write(binaryVersion);
            writer.Write(Code);
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

                VTTQ vtq = vtqs[k];

                int control = (int)vtq.Q;
                long time = vtq.T.JavaTicks;
                long timeDBDiff = vtq.T_DB.JavaTicks - time;
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


                long absTimeDiffDB = System.Math.Abs(timeDBDiff);
                if (absTimeDiffDB <= 0x3F) {
                    int diffControl = (int)absTimeDiffDB;
                    if (timeDBDiff < 0) {
                        diffControl |= 0x40;
                    }
                    writer.Write((byte)diffControl);
                }
                else {
                    int diffControl = 0x80;
                    if (timeDBDiff < 0) {
                        diffControl |= 0x40;
                    }
                    if (absTimeDiffDB <= byte.MaxValue) {
                        // 1 Byte
                        writer.Write((byte)diffControl);
                        writer.Write((byte)absTimeDiffDB);
                    }
                    else if (absTimeDiffDB <= ushort.MaxValue) {
                        diffControl |= 0x10; // 2 Byte
                        writer.Write((byte)diffControl);
                        writer.Write((ushort)absTimeDiffDB);
                    }
                    else if (absTimeDiffDB <= uint.MaxValue) {
                        diffControl |= 0x20; // 4 Byte
                        writer.Write((byte)diffControl);
                        writer.Write((uint)absTimeDiffDB);
                    }
                    else {
                        diffControl |= 0x30; // 8 Byte
                        writer.Write((byte)diffControl);
                        writer.Write((long)absTimeDiffDB);
                    }
                }

                timeBase = time;
                diffBase = diff;
                valBase = val;
            }
        }

        public static List<VTTQ> Deserialize(Stream stream) {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true)) {
                return Deserialize(reader);
            }
        }

        public static List<VTTQ> Deserialize(BinaryReader reader) {

            int binaryVersion = reader.ReadByte();
            if (binaryVersion == 0) throw new IOException("Failed to deserialize VTTQ[]: Version byte is zero");
            if (binaryVersion > Common.CurrentBinaryVersion) throw new IOException("Failed to deserialize VTTQ[]: Wrong version byte");
            if (reader.ReadByte() != Code) throw new IOException("Failed to deserialize VTTQ[]: Wrong start byte");

            int N = reader.ReadInt32();
            var res = new List<VTTQ>(N);

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

                long diffDB = 0;
                int diffControl = reader.ReadByte();
                if ((diffControl & 0x80) == 0) {
                    int abs = diffControl & 0x3F;
                    diffDB = (diffControl & 0x40) == 0 ? abs : -abs;
                }
                else {
                    long diffFactor = (diffControl & 0x40) == 0 ? 1 : -1;
                    int codeByteCount = (diffControl & 0x30) >> 4;
                    if (codeByteCount == 0) {
                        diffDB = diffFactor * reader.ReadByte();
                    }
                    else if (codeByteCount == 1) {
                        diffDB = diffFactor * reader.ReadUInt16();
                    }
                    else if (codeByteCount == 2) {
                        diffDB = diffFactor * reader.ReadUInt32();
                    }
                    else if (codeByteCount == 3) {
                        diffDB = diffFactor * reader.ReadInt64();
                    }
                }

                res.Add(VTTQ.Make(DataValue.FromJSON(val), Timestamp.FromJavaTicks(time), Timestamp.FromJavaTicks(time + diffDB), q));

                timeBase = time;
                diffBase = diff;
                valBase = val;
            }
            return res;
        }
    }
}
