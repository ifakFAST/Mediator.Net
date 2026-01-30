// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.TagMetaData.Config;


// Root -----------------------------------------------------------------------

[XmlRoot(Namespace = "Module_TagMetaData", ElementName = "TagMetaData_Model")]
public sealed class TagMetaData_Model : ModelObject
{
    [XmlIgnore]
    public string ID { get; set; } = "Root";

    [XmlIgnore]
    public string Name { get; set; } = "TagMetaData";

    public FlowDiagram Diagram { get; init; } = new();

    public List<Tag> GetAllTags() => Diagram.GetAllTags();
}

// Diagram --------------------------------------------------------------------

public sealed class FlowDiagram : ModelObject
{
    [XmlArrayItem("ModuleBlock", typeof(ModuleBlock))]
    [XmlArrayItem("MacroBlock", typeof(MacroBlock))]
    [XmlArrayItem("PortBlock", typeof(PortBlock))]
    public List<Block> Blocks { get; init; } = [];

    public List<Line> Lines { get; init; } = [];

    protected override string GetID(IReadOnlyCollection<IModelObject> parents) {
        IModelObject parent = parents.First();
        if (parent is TagMetaData_Model) {
            return "Root/";
        }
        else if (parent is MacroBlock macro) {
            string parentBlockID = Block.GetIDInternal([.. parents.Skip(1)], macro);
            return $"{parentBlockID}/";
        }
        else {
            throw new Exception($"Unexpected parent of FlowDiagram: {parent.GetType().FullName}");
        }
    }

    protected override string GetDisplayName(IReadOnlyCollection<IModelObject> parents) {
        IModelObject parent = parents.First();
        if (parent is TagMetaData_Model) {
            return "Root Diagram";
        }
        else if (parent is MacroBlock macro) {
            string parentBlockID = Block.GetIDInternal([.. parents.Skip(1)], macro);
            return $"{parentBlockID} Diagram";
        }
        else {
            throw new Exception($"Unexpected parent of FlowDiagram: {parent.GetType().FullName}");
        }
    }

    public List<Tag> GetAllTags() {
        List<Tag> tags = [];
        foreach (var block in Blocks) {
            if (block is ModuleBlock mb) {
                tags.AddRange(mb.Tags);
            }
            else if (block is MacroBlock macb) {
                tags.AddRange(macb.Diagram.GetAllTags());
            }
        }
        return tags;
    }
}

// Blocks (base + specializations) --------------------------------------------

[Ifak.Fast.Json.JsonConverter(typeof(BlockConverterIfakFast))]
[System.Text.Json.Serialization.JsonConverter(typeof(BlockConverterSystemText))]
public abstract class Block(BlockType type) : ModelObject
{
    [XmlAttribute("type")]
    public BlockType Type { get; } = type;

    [XmlAttribute("name")]
    public string Name { get; init; } = string.Empty;

    internal const char ID_Separator = '/';

    protected override string GetID(IReadOnlyCollection<IModelObject> parents) {
        return GetIDInternal(parents, this);
    }

    internal static string GetIDInternal(IReadOnlyCollection<IModelObject> parents, Block mainBlock) {

        var sb = new StringBuilder();
        bool first = true;

        foreach (var parent in parents) {
            if (parent is Block block) {
                if (!first) {
                    sb.Append(ID_Separator);
                }
                sb.Append(block.Name);
                first = false;
            }
        }

        // Append current object's Name
        if (!first) {
            sb.Append(ID_Separator);
        }
        sb.Append(mainBlock.Name);

        return sb.ToString();
    }

    [XmlAttribute("x")]
    public double X { get; init; }

    [XmlAttribute("y")]
    public double Y { get; init; }

    [XmlAttribute("w")]
    public double W { get; init; }

    [XmlAttribute("h")]
    public double H { get; init; }

    [XmlAttribute("drawFrame")]
    public bool DrawFrame { get; init; }

    [XmlAttribute("drawName")]
    public bool DrawName { get; init; }

    [XmlAttribute("drawPortLabel")]
    public bool DrawPortLabel { get; init; }

    [XmlAttribute("flipName")]
    public bool FlipName { get; init; }

    [XmlAttribute("rotation")]
    public string Rotation { get; init; } = "0"; // 0, 90, 180, 270

    // Defaults: black on white (leave null to imply defaults)
    public string? ColorForeground { get; init; }
    public string? ColorBackground { get; init; }

    public Frame? Frame { get; init; }
    public Icon? Icon { get; init; }
    public Font? Font { get; init; }

    public bool ShouldSerializeRotation() => Rotation != "0";
    public bool ShouldSerializeColorForeground() => !string.IsNullOrEmpty(ColorForeground);
    public bool ShouldSerializeColorBackground() => !string.IsNullOrEmpty(ColorBackground);
    public bool ShouldSerializeFrame() => Frame != null;
    public bool ShouldSerializeIcon() => Icon != null;
    public bool ShouldSerializeFont() => Font != null;
}

public sealed class ModuleBlock() : Block(BlockType.Module)
{
    public string ModuleType { get; init; } = string.Empty;

    [XmlIgnore]
    public Dictionary<string, string> Parameters { get; init; } = [];

    [Ifak.Fast.Json.JsonIgnore]
    [JsonIgnore]
    [XmlArray("Parameters")]
    [XmlArrayItem("Parameter")]
    public Parameter[] ParametersForSerialization
    {
        get => Parameters.Select(kvp => new Parameter { Name = kvp.Key, Value = kvp.Value }).ToArray();
        init => Parameters = value?.ToDictionary(p => p.Name, p => p.Value) ?? [];
    }

    public sealed class Parameter
    {
        [XmlAttribute("name")]
        public string Name { get; set; } = "";
        
        [XmlText]
        public string Value { get; set; } = "";
    }

    public List<Tag> Tags { get; init; } = [];

    public bool ShouldSerializeTags() => Tags.Count > 0;
}

public sealed class MacroBlock() : Block(BlockType.Macro)
{
    public FlowDiagram Diagram { get; init; } = new();
}

public sealed class PortBlock() : Block(BlockType.Port)
{
    [XmlAttribute("io")]
    public IO IO { get; init; }

    [XmlAttribute("index")]
    public int Index { get; init; }

    [XmlAttribute("lineType")]
    public LineType LineType { get; init; }

    [XmlAttribute("dimension")]
    public int Dimension { get; init; }
}

// Lines ----------------------------------------------------------------------

public sealed class Line
{
    [XmlAttribute("type")]
    public LineType Type { get; init; }

    [XmlAttribute("source")]
    public string Source { get; init; } = string.Empty;

    [XmlAttribute("sourceIdx")]
    public int SourceIdx { get; init; }

    [XmlAttribute("dest")]
    public string Dest { get; init; } = string.Empty;

    [XmlAttribute("destIdx")]
    public int DestIdx { get; init; }

    public List<Point> Points { get; init; } = [];
}

public struct Point
{
    [XmlAttribute("x")]
    public double X { get; set; }

    [XmlAttribute("y")]
    public double Y { get; set; }
}

// Drawing details ------------------------------------------------------------

public sealed class Frame
{
    [XmlAttribute("shape")]
    public Shape Shape { get; init; }

    [XmlAttribute("strokeWidth")]
    public double StrokeWidth { get; init; }

    [XmlAttribute("strokeColor")]
    public string StrokeColor { get; init; } = "#000000";

    [XmlAttribute("fillColor")]
    public string FillColor { get; init; } = "#FFFFFF";

    [XmlAttribute("shadow")]
    public bool Shadow { get; init; }

    [XmlAttribute("var1")]
    public double Var1 { get; init; }

    [XmlAttribute("var2")]
    public double Var2 { get; init; }

    public bool ShouldSerializeVar1() => Var1 != 0;
    public bool ShouldSerializeVar2() => Var2 != 0;
}

public sealed class Icon
{
    [XmlAttribute("name")]
    public string Name { get; init; } = string.Empty;

    [XmlAttribute("x")]
    public int X { get; init; }

    [XmlAttribute("y")]
    public int Y { get; init; }

    [XmlAttribute("w")]
    public int W { get; init; }

    [XmlAttribute("h")]
    public int H { get; init; }

    [XmlAttribute("rotate")]
    public bool Rotate { get; init; }
}

public sealed class Font
{
    [XmlAttribute("family")]
    public string Family { get; init; } = "sans-serif";

    [XmlAttribute("size")]
    public int Size { get; init; } // px

    [XmlAttribute("style")]
    public FontStyle Style { get; init; } = FontStyle.Normal;

    [XmlAttribute("weight")]
    public FontWeight Weight { get; init; } = FontWeight.Normal;

    public bool ShouldSerializeStyle() => Style != FontStyle.Normal;
    public bool ShouldSerializeWeight() => Weight != FontWeight.Normal;
}

// Enums ----------------------------------------------------------------------

public enum BlockType
{
    Module,
    Macro,
    Port
}

public enum LineType
{
    Water,
    Air,
    Signal
}

public enum IO
{
    In,
    Out
}

public enum Shape
{
    Rectangle,
    RoundedRectangle,
    Circle
}

public enum FontStyle
{
    Normal,
    Italic,
    Oblique
}

public enum FontWeight
{
    Normal,
    Bold,
    Thin
}


//[XmlRoot(Namespace = "Module_TagMetaData", ElementName = "TagMetaData_Model")]
//public class TagMetaData_Model : ModelObject
//{
//    [XmlIgnore]
//    public string ID { get; set; } = "Root";

//    [XmlIgnore]
//    public string Name { get; set; } = "TagMetaData";

//    public List<Tag> Tags { get; set; } = [];

//    public string FlowModel { get; set; } = ""; // JSON blob defining a flow model used for linking tags

//    //public static TagMetaData_Model CreateExample()
//    //{
//    //    return new TagMetaData_Model { 
//    //        ID = "Root",
//    //        Name = "TagMetaData",
//    //        Tags = [
//    //            new Tag {
//    //                ID = "Tag1",
//    //                What = "Q_Water",
//    //                UnitSource = "m³/h",
//    //                Unit = "m³/d",
//    //                Variable = VariableRef.Make("IO", "Data_001", "Value"),
//    //            },
//    //            new Tag {
//    //                ID = "Tag2",
//    //                What = "C_NHx_N",
//    //                UnitSource = "mg/L",
//    //                Unit = "mg/L",
//    //                Variable = VariableRef.Make("IO", "Data_002", "Value"),
//    //            },
//    //            new Tag {
//    //                ID = "Tag3",
//    //                What = "L_NHx_N",
//    //                UnitSource = "t/d",
//    //                Unit = "t/d",
//    //                Variable = VariableRef.Make("IO", "Data_003", "Value"),
//    //            }
//    //        ]
//    //    };
//    //}
//}

public class Tag : ModelObject
{
    [XmlAttribute("id")]
    public string ID { get; set; } = "";

    [XmlAttribute("what")]
    public string What { get; set; } = ""; // Identifier of a row in the What metadata table

    [XmlAttribute("unitSource")]
    public string UnitSource { get; set; } = ""; // Identifier of a row in the Unit_Source metadata table

    [XmlAttribute("unit")]
    public string Unit { get; set; } = ""; // Determined by What metadata table

    private static double? ParseNullableDouble(string? s) {
        return string.IsNullOrEmpty(s) ? null : double.Parse(s, CultureInfo.InvariantCulture);
    }

    private static string FormatNullableDouble(double? d) {
        return d.HasValue ? d.Value.ToString(CultureInfo.InvariantCulture) : "";
    }

    [XmlIgnore]
    public double? Depth { get; set; } = null; // in m

    [Ifak.Fast.Json.JsonIgnore]
    [JsonIgnore]
    [XmlAttribute("depth")]
    public string DepthXml
    {
        get => FormatNullableDouble(Depth);
        set => Depth = ParseNullableDouble(value);
    }

    public string Location { get; set; } = ""; // module type specific location description, e.g. enum (front, middle, back) or (inlet, outlet)

    [XmlAttribute("sampling")]
    public Sampling Sampling { get; set; } = Sampling.Sensor;
  
    public SensorDetails? SensorDetails { get; set; } = null; // only if Sampling == Sensor
    public AutoSamplerDetails? AutoSamplerDetails { get; set; } = null; // only if Sampling == AutoSampler
    public string Notes { get; set; } = "";

    [XmlAttribute("source")]
    public string SourceTag { get; set; } = "";

    public VariableRef? GetSourceTagVarRef() {
        if (string.IsNullOrEmpty(SourceTag)) return null;
        ObjectRef? obj = ObjectRef.TryFromEncodedString(SourceTag);
        return obj is null ? null : VariableRef.Make(obj.Value, "Value");
    }

    public bool ShouldSerializeDepth() => Depth.HasValue;
    public bool ShouldSerializeDepthXml() => Depth.HasValue;
    public bool ShouldSerializeLocation() => !string.IsNullOrEmpty(Location);
    public bool ShouldSerializeSensorDetails() => SensorDetails != null;
    public bool ShouldSerializeAutoSamplerDetails() => AutoSamplerDetails != null;
    public bool ShouldSerializeNotes() => !string.IsNullOrEmpty(Notes);

    protected override string GetDisplayName(IReadOnlyCollection<IModelObject> parents) => What;

    protected override Variable[] GetVariablesOrNull(IReadOnlyCollection<IModelObject> parents) {

        History history = History.None;

        var variable = new Variable(
                name: "Value",
                type: DataType.Float64,
                defaultValue: DataValue.Empty,
                unit: Unit,
                typeConstraints: "",
                dimension: 1,
                dimensionNames: [],
                remember: true,
                history: history,
                writable: false,
                syncReadable: false);

        return [variable];
    }
}

public enum Sampling
{
    Sensor,
    GrabSampling,
    AutoSampler,
    Calculated,
}

public class SensorDetails {

    public SensorType Type { get; set; } = SensorType.InSitu;
    public MeasurementPrinciple Principle { get; set; } = MeasurementPrinciple.ISE;
    public double T90 { get; set; } = 0.0; // in minutes

    public SensorDetails() { }

    public SensorDetails(SensorType type, MeasurementPrinciple principle, double t90) {
        Type = type;
        Principle = principle;
        T90 = t90;
    }
}

public class AutoSamplerDetails {

    public ProportionalType Proportional { get; set; } = ProportionalType.Volume;
    public double Interval { get; set; } = 1.0; // in hours
    public double Offset { get; set; } = 0.0; // in hours
    public TimestampPos TimestampPosition { get; set; } = TimestampPos.Start;

    public AutoSamplerDetails() { }

    public AutoSamplerDetails(ProportionalType proportional, double interval, double offset, TimestampPos timestampPosition) {
        Proportional = proportional;
        Interval = interval;
        Offset = offset;
        TimestampPosition = timestampPosition;
    }
}

public enum MeasurementPrinciple
{
    ISE, // Ion Selective Electrode
    GSE, // Galvanic Sensor Electrode
    Colorimetric,
    Spectral
}

public enum SensorType
{
    InSitu,
    ExSitu
}

public enum ProportionalType
{
    Volume,
    Time,
    Flow
}

public enum TimestampPos
{
    Start,
    Middle,
    End
}
