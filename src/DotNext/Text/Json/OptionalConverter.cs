using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNext.Text.Json;

/// <summary>
/// Represents JSON converter for <see cref="Optional{T}"/> data type.
/// </summary>
/// <typeparam name="T">The type of the value in <see cref="Optional{T}"/> container.</typeparam>
public sealed class OptionalConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)]T> : JsonConverter<Optional<T>>
{
    /// <inheritdoc />
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Public properties/fields are preserved")]
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
                // TODO: Attempt to extract IJsonTypeInfo resolver for type T in .NET 7 to avoid Reflection
                JsonSerializer.Serialize<T?>(writer, value.OrDefault(), options);
                break;
        }
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Public properties/fields are preserved")]
    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.TokenType is JsonTokenType.Null ? default : JsonSerializer.Deserialize<T>(ref reader, options));
}