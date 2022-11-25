using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNext.Text.Json;

/// <summary>
/// Represents JSON converter for <see cref="Optional{T}"/> data type.
/// </summary>
/// <remarks>
/// For AOT and self-contained app deployment models, use <see cref="OptionalConverter{T}"/>
/// converter explicitly as an argument for <see cref="JsonConverterAttribute"/>.
/// </remarks>
[RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
public sealed class OptionalConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsOptional();

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var underlyingType = Optional.GetUnderlyingType(typeToConvert);
        return underlyingType is null
            ? null
            : Activator.CreateInstance(typeof(OptionalConverter<>).MakeGenericType(underlyingType)) as JsonConverter;
    }
}