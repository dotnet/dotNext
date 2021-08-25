#if !NETCOREAPP3_1
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DotNext.Text.Json
{
    [ExcludeFromCodeCoverage]
    public sealed class OptionalConverterFactoryTests : Test
    {
        public sealed class JsonObject
        {
            [JsonConverter(typeof(OptionalConverterFactory))]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public Optional<int> IntegerValue{ get; set; }

            [JsonConverter(typeof(OptionalConverterFactory))]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public Optional<string> StringField{ get; set; }

            public bool BoolField{ get; set; }
        }

        [Fact]
        public static void UndefinedValues()
        {
            var expected = new JsonObject
            {
                IntegerValue = Optional.None<int>(),
                StringField = Optional.None<string>(),
                BoolField = true,
            };

            var json = JsonSerializer.Serialize<JsonObject>(expected);
            var actual = JsonSerializer.Deserialize<JsonObject>(json);
            True(actual.IntegerValue.IsUndefined);
            True(actual.StringField.IsUndefined);
            True(actual.BoolField);
        }

        [Fact]
        public static void NullValues()
        {
            var expected = new JsonObject
            {
                IntegerValue = Optional.None<int>(),
                StringField = Optional.Null<string>(),
                BoolField = true,
            };

            var json = JsonSerializer.Serialize<JsonObject>(expected);
            var actual = JsonSerializer.Deserialize<JsonObject>(json);
            True(actual.IntegerValue.IsUndefined);
            True(actual.StringField.IsNull);
            True(actual.BoolField);
        }

        [Fact]
        public static void NormalValues()
        {
            var expected = new JsonObject
            {
                IntegerValue = 10,
                StringField = "Hello, world!",
                BoolField = true,
            };

            var json = JsonSerializer.Serialize<JsonObject>(expected);
            var actual = JsonSerializer.Deserialize<JsonObject>(json);
            Equal(10, actual.IntegerValue.Value);
            Equal("Hello, world!", actual.StringField.Value);
            True(actual.BoolField);
        }
    }
}
#endif