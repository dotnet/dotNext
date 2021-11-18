using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DotNext.Text.Json;

[ExcludeFromCodeCoverage]
public sealed class TestJsonObject
{
    [JsonConverter(typeof(OptionalConverterFactory))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<int> IntegerValue { get; set; }

    [JsonConverter(typeof(OptionalConverter<string>))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<string> StringField { get; set; }

    public bool BoolField { get; set; }
}