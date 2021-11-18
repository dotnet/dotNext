using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNext.Text.Json;

/// <summary>
/// Represents JSON converter for <see cref="Optional{T}"/> data type.
/// </summary>
/// <typeparam name="T">The type of the value in <see cref="Optional{T}"/> container.</typeparam>
public sealed class OptionalConverter<T> : JsonConverter<Optional<T>>
{
    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
    {
        if (value.IsUndefined)
            throw new InvalidOperationException(ExceptionMessages.UndefinedValueDetected);

        if (value.IsNull)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize<T?>(writer, value.OrDefault(), options);
    }

    /// <inheritdoc />
    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.TokenType == JsonTokenType.Null ? default : JsonSerializer.Deserialize<T>(ref reader, options));
}