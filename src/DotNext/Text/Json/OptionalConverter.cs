using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
        switch (value)
        {
            case { IsUndefined: true }:
                throw new InvalidOperationException(ExceptionMessages.UndefinedValueDetected);
            case { IsNull: true }:
                writer.WriteNullValue();
                break;
            default:
                var typeInfo = options.GetTypeInfo(typeof(T));
                if (typeInfo is JsonTypeInfo<T?> knownTypeInfo)
                {
                    JsonSerializer.Serialize(writer, value.ValueOrDefault, knownTypeInfo);
                }
                else
                {
                    JsonSerializer.Serialize(writer, value.ValueOrDefault, typeInfo);
                }

                break;
        }
    }

    /// <inheritdoc />
    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
            return new(isNull: true);

        var typeInfo = options.GetTypeInfo(typeof(T));
        return new(typeInfo is JsonTypeInfo<T?> knownTypeInfo
            ? JsonSerializer.Deserialize(ref reader, knownTypeInfo)
            : (T?)JsonSerializer.Deserialize(ref reader, typeInfo));
    }
}