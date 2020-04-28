// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO.Adapter_Modbus
{
    [Identify("ModbusTCP.IQSensorNet")]
    public class IQSensorNet : ModbusTCP
    {
        const string VarInfo = "Info";
        const string VarMain = "Main";
        const string VarSecondary = "Secondary";

        bool IsIQSensorNetAddress(DataItem item) => item.Address.StartsWith('S');

        protected override ModbusAddress GetModbusAddress(DataItem item) {
            if (!IsIQSensorNetAddress(item)) {
                return base.GetModbusAddress(item);
            }
            string address = item.Address;
            int idx = address.IndexOf('.');
            if (idx < 0) throw new Exception($"Invalid address '{address}': Missing value separator");
            int sensorNum = int.Parse(address[1..idx]);
            int RegisterSensor = (sensorNum - 1) * 8 + 1;
            string value = address[(idx + 1)..];
            switch (value) {
                case VarInfo:      return ModbusAddress.Make(RegisterSensor,     count: 8);
                case VarMain:      return ModbusAddress.Make(RegisterSensor + 3, count: 3);
                case VarSecondary: return ModbusAddress.Make(RegisterSensor + 3, count: 5);
                default: throw new Exception($"Invalid address: {address}");
            }
        }

        protected override VTQ ParseModbusResponse(DataItem item, ushort[] words, Timestamp now) {

            if (!IsIQSensorNetAddress(item)) {
                return base.ParseModbusResponse(item, words, now);
            }

            switch (words.Length) {

                case 3: { // Main value

                        Quality quality = MakeQuality(HighNibble(LowByte(words[0])));
                        float value = ReadFloat32FromWords(words, offset: 1);
                        return VTQ.Make(value, now, quality);
                    }

                case 5: { // Secondary value

                        Quality quality = MakeQuality(LowNibble(LowByte(words[0])));
                        float value = ReadFloat32FromWords(words, offset: 3);
                        return VTQ.Make(value, now, quality);
                    }

                case 8: { // Sensor info

                        SensorType sensor = SensorModel(words[1]);

                        var info = new {
                            SensorNum = HighByte(words[0]),
                            SensorStatus = SensorStatus(LowByte(words[0])),
                            SensorModel = sensor.Name,
                            ErrorCode = HexStr(words[2]),
                            Unit = sensor.UnitFromCode(HighByte(words[3])),
                            MainStatus = MeasuredValueStatus(HighNibble(LowByte(words[3]))),
                            MainValue = ReadFloat32FromWords(words, offset: 4),
                            SecondaryStatus = MeasuredValueStatus(LowNibble(LowByte(words[3]))),
                            SecondaryValue = ReadFloat32FromWords(words, offset: 6)
                        };

                        return VTQ.Make(DataValue.FromObject(info), now, Quality.Good);
                    }

                default:
                    throw new Exception($"Unexpected response length: {words.Length}");
            }
        }

        public override Task<string[]> BrowseDataItemAddress(string idOrNull) {
            string[] names = Enumerable.Range(1, 21).SelectMany(SensorVariables).ToArray();
            return Task.FromResult(names);
        }

        string[] SensorVariables(int idx) {
            string num = idx.ToString("00");
            return new string[] {
                $"S{num}.{VarInfo}",
                $"S{num}.{VarMain}",
                $"S{num}.{VarSecondary}",
            };
        }

        Quality MakeQuality(int status) {
            if (status == 1) return Quality.Good; // 1 == VALID
            if (status == 2) return Quality.Bad;  // 2 == OFL
            if (status == 3) return Quality.Bad;  // 3 == INVALID
            if (status == 4) return Quality.Bad;  // 3 == MISSING
            return Quality.Bad;
        }

        string MeasuredValueStatus(byte status) {
            if (status == 1) return "VALID";
            if (status == 2) return "OFL";
            if (status == 3) return "INVALID";
            if (status == 4) return "MISSING";
            return HexStr(status);
        }

        string SensorStatus(byte code) {
            if (code == 0) return "UNUSED_ID";
            if (code == 1) return "INACTIVE";
            if (code == 2) return "MEASURE";
            if (code == 3) return "CALIBRATE";
            if (code == 4) return "ERROR";
            return HexStr(code);
        }

        static SensorType[] SensorTypes = new SensorType[] {
            new SensorType(0x0101, "SensoLyt700IQ",       "pH", "mV"),
            new SensorType(0x0201, "TetraCon700IQ",       "mS/cm", "SAL", "TDS", "S/m"),
            new SensorType(0x0301, "TriOxmatic700IQ",     "mg/l O2", "% O2"),
            new SensorType(0x0302, "TriOxmatic701IQ",     "mg/l O2", "% O2"),
            new SensorType(0x0303, "TriOxmatic702IQ",     "mg/l O2", "% O2"),
            new SensorType(0x0304, "SC_FDO700",           "mg/l O2", "% O2"),
            new SensorType(0x0305, "SC_FDO701",           "mg/l O2", "% O2"),
            new SensorType(0x0401, "VisoTurb700IQ",       "FNU-Turb", "NTU-Turb", "TEF-Turb", "mg/l SiO2", "ppm SiO2", "g/l TSS"),
            new SensorType(0x0402, "ViSolid700IQ",        "g/l TSS (M11)", "% TSS (M11)", "g/l TSS (M21)", "% TSS (M21)", "g/l SiO2 (M1)", "% SiO2 (M1)", "g/l SiO2 (M2)", "% SiO2 (M2)"),
            new SensorType(0x0501, "AmmoLyt700IQ",        "mg/l NH4-N", "mg/l NH4", "mV"),
            new SensorType(0x0503, "AmmoLyt+",            "mg/l NH4-N", "mg/l NH4", "mV"),
            new SensorType(0x0601, "NitraLyt700IQ",       "mg/l NO3-N", "mg/l NO3", "mV"),
            new SensorType(0x0602, "NitraLyt+",           "mg/l NO3-N", "mg/l NO3", "mV"),
            new SensorType(0x0701, "NitraVis700_1",       "mg/l NO3-N", "mg/l NO3", "mg/l NO3-N"),
            new SensorType(0x0702, "NitraVis700_5",       "mg/l NO3-N", "mg/l NO3", "mg/l NO3-N"),
            new SensorType(0x0703, "CarboVis700_5",       "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4"),
            new SensorType(0x0704, "SolidVis700IQ",       "(m)g/l TSS"),
            new SensorType(0x0705, "NitraVis700_5",       "mg/l NO3-N", "mg/l NO3", "mg/l NO3-N"),
            new SensorType(0x0706, "CarboVis700_5",       "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4"),
            new SensorType(0x0707, "CarboVis700_1",       "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4"),
            new SensorType(0x0801, "MIQ/IC2_I1in",        "mA"),
            new SensorType(0x0802, "MIQ/IC2_I2in",        "mA"),
            new SensorType(0x0901, "VARiON_A",            "mg/l NH4-N", "mg/l NH4", "mV"),
            new SensorType(0x0902, "VARiON_N",            "mg/l NO3-N", "mg/l NO3", "mV"),
            new SensorType(0x0905, "VARiON+A",            "mg/l NH4-N", "mg/l NH4", "mV"),
            new SensorType(0x0906, "VARiON+N",            "mg/l NO3-N", "mg/l NO3", "mV"),
            new SensorType(0x0907, "AmmoLyt+K",           "mg/l K", "mV"),
            new SensorType(0x0A01, "NitraVis_701",        "mg/l NO3-N", "mg/l NO3"),
            new SensorType(0x0A02, "NitraVis_705",        "mg/l NO3-N", "mg/l NO3"),
            new SensorType(0x0A03, "CarboVis_701",        "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4", "UVT 254"),
            new SensorType(0x0A04, "CarboVis_705",        "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4", "UVT 254"),
            new SensorType(0x0A05, "UVSAC_701",           "1/m SAKgl", "1/m SAKgs", "% UVT 254", "mg/l CSBgl Korrel", "mg/l CSBgs Korrel", "mg/l TOC Korrel", "mg/l BSB Korrel", "mg/l DOC Korrel"),
            new SensorType(0x0A06, "UVSAC_705",           "1/m SAKgl", "1/m SAKgs", "% UVT 254", "mg/l CSBgl Korrel", "mg/l CSBgs Korrel", "mg/l TOC Korrel", "mg/l BSB Korrel", "mg/l DOC Korrel"),
            new SensorType(0x0A07, "SolidVis_701",        "(m)g/l TSS"),
            new SensorType(0x0A08, "SolidVis_705",        "(m)g/l TSS"),
            new SensorType(0x0A09, "UVNOx_701",           "mg/l NO3-N", "mg/l NO3"),
            new SensorType(0x0A0A, "UVNOx_705",           "mg/l NO3-N", "mg/l NO3"),
            new SensorType(0x0A1A, "VirtualNsensor_701",  "mg/l NO3-N", "mg/l NO3"),
            new SensorType(0x0A1B, "VirtualNsensor_705",  "mg/l NO3-N", "mg/l NO3"),
            new SensorType(0x0A1C, "VirtualCsensor1_701", "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4", "UVT 254"),
            new SensorType(0x0A1D, "VirtualCsensor2_701", "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4", "UVT 254"),
            new SensorType(0x0A1E, "VirtualCsensor3_701", "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4", "UVT 254"),
            new SensorType(0x0A1F, "VirtualCsensor4_701", "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4", "UVT 254"),
            new SensorType(0x0A20, "VirtualCsensor1_705", "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4", "UVT 254"),
            new SensorType(0x0A21, "VirtualCsensor2_705", "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4", "UVT 254"),
            new SensorType(0x0A22, "VirtualCsensor3_705", "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4", "UVT 254"),
            new SensorType(0x0A23, "VirtualCsensor4_705", "mg/l CODto", "mg/l CODds", "mg/l TOC", "mg/l BOD", "mg/l DOC", "Abs/m SACto", "Abs/m SACds", "mg/l CSB4", "UVT 254"),
            new SensorType(0x0B01, "P700IQ",              "mg/l PO4-P", "mg/l PO4"),
            new SensorType(0x0C01, "IFL700IQ",            "m"),
            new SensorType(0x0C02, "IFL701IQ",            "m"),
        };

        SensorType SensorModel(ushort code) {
            SensorType sensor = SensorTypes.FirstOrDefault(sensor => sensor.Code == code);
            return sensor ?? new SensorType(code, HexStr(code));
        }

        byte HighByte(ushort word) => (byte)((word & 0xFF00) >> 8);

        byte LowByte(ushort word) => (byte)((word & 0x00FF));

        byte HighNibble(byte b) => (byte)((b & 0xF0) >> 4);

        byte LowNibble(byte b) => (byte)(b & 0x0F);

        string HexStr(ushort v) => v.ToString("X4") + "h";

        string HexStr(byte v) => v.ToString("X2") + "h";
    }

    class SensorType
    {
        public string Name { get; private set; }
        public ushort Code { get; private set; }
        public string[] Units { get; private set; }

        public SensorType(ushort code, string name, params string[] units) {
            Name = name;
            Code = code;
            Units = units;
        }

        public string UnitFromCode(byte code) {
            if (code >= Units.Length) return code.ToString("X2") + "h";
            return Units[code];
        }
    }
}
