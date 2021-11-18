using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNext.Text.Json;

using RuntimeFeaturesAttribute = Runtime.CompilerServices.RuntimeFeaturesAttribute;

/// <summary>
/// Represents JSON converter for <see cref="Optional{T}"/> data type.
/// </summary>
public sealed class OptionalConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsOptional();

    /// <inheritdoc />
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(OptionalConverter<>))]
    [RuntimeFeaturesAttribute(RuntimeGenericInstantiation = true)]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var underlyingType = Optional.GetUnderlyingType(typeToConvert);
        return underlyingType is null ?
            null :
            Activator.CreateInstance(typeof(OptionalConverter<>).MakeGenericType(underlyingType)) as JsonConverter;
    }
}