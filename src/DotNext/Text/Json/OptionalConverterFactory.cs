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
public sealed class OptionalConverterFactory : JsonConverterFactory // TODO: Add [RequiresDynamicCode] in .NET 7
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsOptional();

    /// <inheritdoc />
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "No way to annotate the entire class")]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var underlyingType = Optional.GetUnderlyingType(typeToConvert);
        return underlyingType is null
            ? null
            : Activator.CreateInstance(typeof(OptionalConverter<>).MakeGenericType(underlyingType)) as JsonConverter;
    }
}