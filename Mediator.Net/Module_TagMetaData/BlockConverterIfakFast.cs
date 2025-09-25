using System;
using Ifak.Fast.Json;
using Ifak.Fast.Json.Linq;

namespace Ifak.Fast.Mediator.TagMetaData.Config;

public class BlockConverterIfakFast : JsonConverter<Block>
{
    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, Block? value, JsonSerializer serializer) {
        throw new NotImplementedException("Custom WriteJson is not needed as CanWrite is false.");
    }

    public override Block? ReadJson(JsonReader reader, Type objectType, Block? existingValue, bool hasExistingValue, JsonSerializer serializer) {

        if (reader.TokenType == JsonToken.Null)
            return null;

        JObject obj = JObject.Load(reader);

        JToken? token = obj["Type"];
        string? type = token?.Value<string>();

        if (type == null)
            throw new JsonSerializationException("Missing 'Type' property in Block JSON");

        Block targetBlock = type switch {
            nameof(BlockType.Module) => new ModuleBlock(),
            nameof(BlockType.Macro) => new MacroBlock(),
            nameof(BlockType.Port) => new PortBlock(),
            _ => throw new JsonSerializationException($"Unsupported BlockType: {type}"),
        };

        using (var subReader = obj.CreateReader()) {
            serializer.Populate(subReader, targetBlock);
        }

        return targetBlock;
    }
}
