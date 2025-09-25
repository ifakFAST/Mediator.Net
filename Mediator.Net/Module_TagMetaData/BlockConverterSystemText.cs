using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ifak.Fast.Mediator.TagMetaData.Config;

public class BlockConverterSystemText : JsonConverter<Block>
{
    public override Block Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        
        if (!root.TryGetProperty("type", out var typeProperty))
        {
            throw new JsonException("Missing type property for Block deserialization");
        }
        
        var blockType = typeProperty.GetString();
        
        return blockType switch
        {
            nameof(BlockType.Module) => JsonSerializer.Deserialize<ModuleBlock>(root.GetRawText(), options)!,
            nameof(BlockType.Macro) => JsonSerializer.Deserialize<MacroBlock>(root.GetRawText(), options)!,
            nameof(BlockType.Port) => JsonSerializer.Deserialize<PortBlock>(root.GetRawText(), options)!,
            _ => throw new JsonException($"Unknown BlockType: {blockType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, Block value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case ModuleBlock moduleBlock:
                JsonSerializer.Serialize(writer, moduleBlock, options);
                break;
            case MacroBlock macroBlock:
                JsonSerializer.Serialize(writer, macroBlock, options);
                break;
            case PortBlock portBlock:
                JsonSerializer.Serialize(writer, portBlock, options);
                break;
            default:
                throw new JsonException($"Unknown Block type: {value.GetType()}");
        }
    }
}
