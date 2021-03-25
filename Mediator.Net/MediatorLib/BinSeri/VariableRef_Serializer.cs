using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ifak.Fast.Mediator.BinSeri
{
    public static class VariableRef_Serializer
    {
        internal const byte Code = 91;
        private const int MaxModules = 7; // 3 bit - 1
        private const int MaxVariables = 31; // 5 bit - 1

        public static void Serialize(Stream stream, List<VariableRef> variables, byte binaryVersion) {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                Serialize(writer, variables, binaryVersion);
            }
        }

        public static void Serialize(BinaryWriter writer, List<VariableRef> variables, byte binaryVersion) {

            int N = variables.Count;
            writer.Write(binaryVersion);
            writer.Write(Code);
            writer.Write(N);
            if (N == 0) return;

            string[] moduleIDs = new string[MaxModules];
            string[] variableNames = new string[MaxVariables];
            int countModulesIDs = 0;
            int countVariables = 0;

            for (int k = 0; k < N; ++k) {
                VariableRef vr = variables[k];
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

            for (int k = 0; k < N; ++k) {

                VariableRef vr = variables[k];

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
            }
        }

        public static List<VariableRef> Deserialize(Stream stream) {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true)) {
                return Deserialize(reader);
            }
        }

        public static List<VariableRef> Deserialize(BinaryReader reader) {

            int binaryVersion = reader.ReadByte();
            if (binaryVersion == 0) throw new IOException("Failed to deserialize VariableRef[]: Version byte is zero");
            if (binaryVersion > Common.CurrentBinaryVersion) throw new IOException("Failed to deserialize VariableRef[]: Wrong version byte");
            if (reader.ReadByte() != Code) throw new IOException("Failed to deserialize VariableRef[]: Wrong start byte");

            int N = reader.ReadInt32();
            var res = new List<VariableRef>(N);

            if (N == 0) return res;

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

                res.Add(VariableRef.Make(moduleID, objectID, variable));
            }

            return res;
        }
    }
}
