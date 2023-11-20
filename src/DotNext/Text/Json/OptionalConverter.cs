using System.Diagnostics.CodeAnalysis;
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
    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013", Justification = "False positive")]
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
                if (typeInfo is JsonTypeInfo<T?> info)
                {
                    JsonSerializer.Serialize(writer, value.ValueOrDefault, info);
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
        JsonTypeInfo typeInfo;
        return new(
            reader.TokenType is JsonTokenType.Null ?
            default
            : (typeInfo = options.GetTypeInfo(typeof(T))) is JsonTypeInfo<T?>
            ? JsonSerializer.Deserialize(ref reader, (JsonTypeInfo<T>)typeInfo)
            : (T?)JsonSerializer.Deserialize(ref reader, typeInfo));
    }
}