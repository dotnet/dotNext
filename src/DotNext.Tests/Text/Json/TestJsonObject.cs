using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DotNext.Text.Json;

[ExcludeFromCodeCoverage]
public sealed class TestJsonObject : IJsonSerializable<TestJsonObject>
{
    [JsonConverter(typeof(OptionalConverterFactory))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<int> IntegerValue { get; set; }

    [JsonConverter(typeof(OptionalConverter<string>))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<string> StringField { get; set; }

    public bool BoolField { get; set; }

    static JsonTypeInfo<TestJsonObject> IJsonSerializable<TestJsonObject>.TypeInfo => SerializationContext.Default.TestJsonObject;
}