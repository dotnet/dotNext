using System.Text.Json.Serialization;

namespace DotNext.Text.Json;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(TestJsonObject))]
public partial class SerializationContext : JsonSerializerContext
{
}