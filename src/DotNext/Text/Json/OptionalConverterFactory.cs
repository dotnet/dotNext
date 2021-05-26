#if !NETSTANDARD2_1
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNext.Text.Json
{
    using RuntimeFeaturesAttribute = Runtime.CompilerServices.RuntimeFeaturesAttribute;

    /// <summary>
    /// Represents JSON converter for <see cref="Optional{T}"/> data type.
    /// </summary>
    public sealed class OptionalConverterFactory : JsonConverterFactory
    {
        [SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated implicitly inside of CreateConverter method")]
        private sealed class DelegatingConverter<T> : JsonConverter<Optional<T>>
        {
            public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
            {
                if (value.IsUndefined)
                    throw new InvalidOperationException(ExceptionMessages.UndefinedValueDetected);

                if (value.IsNull)
                    writer.WriteNullValue();
                else
                    JsonSerializer.Serialize<T>(writer, value.OrDefault()!, options);
            }

            public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                Optional<T> result;
                if (reader.TokenType == JsonTokenType.Null)
                {
                    result = default(T);
                }
                else
                {
                    result = JsonSerializer.Deserialize<T>(ref reader, options);
                }

                return result;
            }
        }

        /// <summary>
        /// Initializes a new converter for <see cref="Optional{T}"/> type.
        /// </summary>
        public OptionalConverterFactory()
        {
            if (LibrarySettings.IsUndefinedEqualsNull)
                throw new NotSupportedException(ExceptionMessages.UndefinedEqualsNullSwitchEnabled);
        }

        /// <inheritdoc />
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsOptional();

        /// <inheritdoc />
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DelegatingConverter<>))]
        [RuntimeFeaturesAttribute(RuntimeGenericInstantiation = true)]
        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var underlyingType = Optional.GetUnderlyingType(typeToConvert);
            return underlyingType is null ?
                null :
                Activator.CreateInstance(typeof(DelegatingConverter<>).MakeGenericType(underlyingType)) as JsonConverter;
        }
    }
}
#endif