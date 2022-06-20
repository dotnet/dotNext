using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DotNext.Text.Json;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(TestJsonObject))]
[ExcludeFromCodeCoverage]
public partial class SerializationContext : JsonSerializerContext
{
}