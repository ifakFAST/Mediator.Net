using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ifak.Fast.Mediator.BinSeri
{
    public static class VariableValue_Serializer
    {
        internal const byte Code = 90;
        private const int MaxModules = 7; // 3 bit - 1
        private const int MaxVariables = 31; // 5 bit - 1

        public static void Serialize(Stream stream, List<VariableValue> varVals, byte binaryVersion) {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                Serialize(writer, varVals, binaryVersion);
            }
        }

        public static void Serialize(BinaryWriter writer, List<VariableValue> varVals, byte binaryVersion) {

            int N = varVals.Count;
            if (N > Common.MaxListLen) throw new System.Exception($"VariableValue_Serializer: May not serialize more than {Common.MaxListLen} items");
            writer.Write(binaryVersion);
            writer.Write(Code);
            writer.Write(N);
            if (N == 0) return;

            long timeBase = varVals[0].Value.T.JavaTicks;
            long diffBase = N == 1 ? 0 : varVals[1].Value.T.JavaTicks - timeBase;
            string valBase = varVals[0].Value.V.JSON;

            timeBase -= diffBase;
            writer.Write(timeBase);
            writer.Write(diffBase);
            writer.Write(valBase);

            string[] moduleIDs = new string[MaxModules];
            string[] variableNames = new string[MaxVariables];
            int countModulesIDs = 0;
            int countVariables = 0;

            for (int k = 0; k < N; ++k) {
                VariableRef vr = varVals[k].Variable;
                string moduleID = vr.Object.ModuleID;
                string variableName = vr.Name;

                for (int i = 0; i < MaxModules; ++i) {
                    string thisID = moduleIDs[i];
                    if (thisID == moduleID) {
                        break;
                    }
                    else if (thisID == null) {
                        moduleIDs[i] = moduleID;
                        countModulesIDs++;
                        break;
                    }
                }

                for (int i = 0; i < MaxVariables; ++i) {
                    string thisID = variableNames[i];
                    if (thisID == variableName) {
                        break;
                    }
                    else if (thisID == null) {
                        variableNames[i] = variableName;
                        countVariables++;
                        break;
                    }
                }
            }

            writer.Write((byte)countModulesIDs);
            for (int i = 0; i < countModulesIDs; ++i) {
                string thisID = moduleIDs[i];
                writer.Write(thisID);
            }

            writer.Write((byte)countVariables);
            for (int i = 0; i < countVariables; ++i) {
                string thisID = variableNames[i];
                writer.Write(thisID);
            }

            byte[] codeTable = Common.mCodeTable;

            for (int k = 0; k < N; ++k) {

                VariableValue varVal = varVals[k];
                VariableRef vr = varVal.Variable;

                string moduleID = vr.Object.ModuleID;
                string objID = vr.Object.LocalObjectID;
                string variableName = vr.Name;

                int control0 = 0;

                bool explicitModuleID = true;
                for (int i = 0; i < countModulesIDs; ++i) {
                    string thisID = moduleIDs[i];
                    if (thisID == moduleID) {
                        control0 = i;
                        explicitModuleID = false;
                        break;
                    }
                }

                bool explicitVarName = true;
                for (int i = 0; i < countVariables; ++i) {
                    string thisID = variableNames[i];
                    if (thisID == variableName) {
                        control0 |= (i << 3);
                        explicitVarName = false;
                        break;
                    }
                }

                if (explicitModuleID) {
                    control0 |= 0x07;
                }

                if (explicitVarName) {
                    control0 |= 0xF8;
                }

                writer.Write((byte)control0);

                if (explicitModuleID) {
                    writer.Write(moduleID);
                }

                if (explicitVarName) {
                    writer.Write(variableName);
                }

                writer.Write(objID);


                VTQ vtq = varVal.Value;

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

        public static List<VariableValue> Deserialize(Stream stream) {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true)) {
                return Deserialize(reader);
            }
        }

        public static List<VariableValue> Deserialize(BinaryReader reader) {

            int binaryVersion = reader.ReadByte();
            if (binaryVersion == 0) throw new IOException("Failed to deserialize VariableValue[]: Version byte is zero");
            if (binaryVersion > Common.CurrentBinaryVersion) throw new IOException("Failed to deserialize VariableValue[]: Wrong version byte");
            if (reader.ReadByte() != Code) throw new IOException("Failed to deserialize VariableValue[]: Wrong start byte");

            int N = reader.ReadInt32();
            if (N > Common.MaxListLen) throw new System.Exception($"VariableValue_Serializer: May not deserialize more than {Common.MaxListLen} items");
            var res = new List<VariableValue>(N);

            if (N == 0) return res;

            long timeBase = reader.ReadInt64();
            long diffBase = reader.ReadInt64();
            string valBase = reader.ReadString();

            string[] moduleIDs = new string[MaxModules];
            string[] variableNames = new string[MaxVariables];

            int countModulesIDs = reader.ReadByte();
            for (int i = 0; i < countModulesIDs; ++i) {
                moduleIDs[i] = reader.ReadString();
            }

            int countVariables = reader.ReadByte();
            for (int i = 0; i < countVariables; ++i) {
                variableNames[i] = reader.ReadString();
            }

            char[] buffer = new char[255];
            char[] mapCode2Char = Common.mapCode2Char;

            for (int k = 0; k < N; ++k) {

                int control0 = reader.ReadByte();
                int idxModuleID = control0 & 0x07;
                int idxVariable = (control0 & 0xF8) >> 3;

                bool implicitModuleID = idxModuleID < 0x07;
                bool implicitVarName = idxVariable < 0x1F;

                string moduleID;
                if (implicitModuleID) {
                    moduleID = moduleIDs[idxModuleID];
                }
                else {
                    moduleID = reader.ReadString();
                }

                string variable;
                if (implicitVarName) {
                    variable = variableNames[idxVariable];
                }
                else {
                    variable = reader.ReadString();
                }

                string objectID = reader.ReadString();

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

                var vtq = VTQ.Make(DataValue.FromJSON(val), Timestamp.FromJavaTicks(time), q);
                res.Add(VariableValue.Make(moduleID, objectID, variable, vtq));

                timeBase = time;
                diffBase = diff;
                valBase = val;
            }

            return res;
        }
    }
}
