using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace DotNext.Text.Json;

[ExcludeFromCodeCoverage]
public sealed class OptionalConverterFactoryTests : Test
{
    [Fact]
    public static void UndefinedValues()
    {
        var expected = new TestJsonObject
        {
            IntegerValue = Optional.None<int>(),
            StringField = Optional.None<string>(),
            BoolField = true,
        };

        var json = JsonSerializer.Serialize<TestJsonObject>(expected);
        var actual = JsonSerializer.Deserialize<TestJsonObject>(json);
        True(actual.IntegerValue.IsUndefined);
        True(actual.StringField.IsUndefined);
        True(actual.BoolField);
    }

    [Fact]
    public static void NullValues()
    {
        var expected = new TestJsonObject
        {
            IntegerValue = Optional.None<int>(),
            StringField = Optional.Null<string>(),
            BoolField = true,
        };

        var json = JsonSerializer.Serialize<TestJsonObject>(expected);
        var actual = JsonSerializer.Deserialize<TestJsonObject>(json);
        True(actual.IntegerValue.IsUndefined);
        True(actual.StringField.IsNull);
        True(actual.BoolField);
    }

    [Fact]
    public static void NormalValues()
    {
        var expected = new TestJsonObject
        {
            IntegerValue = 10,
            StringField = "Hello, world!",
            BoolField = true,
        };

        var json = JsonSerializer.Serialize<TestJsonObject>(expected);
        var actual = JsonSerializer.Deserialize<TestJsonObject>(json);
        Equal(10, actual.IntegerValue.Value);
        Equal("Hello, world!", actual.StringField.Value);
        True(actual.BoolField);
    }

    [Fact]
    public static void UndefinedValuesWithContext()
    {
        var expected = new TestJsonObject
        {
            IntegerValue = Optional.None<int>(),
            StringField = Optional.None<string>(),
            BoolField = true,
        };

        var json = JsonSerializer.Serialize<TestJsonObject>(expected, SerializationContext.Default.TestJsonObject);
        var actual = JsonSerializer.Deserialize<TestJsonObject>(json);
        True(actual.IntegerValue.IsUndefined);
        True(actual.StringField.IsUndefined);
        True(actual.BoolField);
    }

    [Fact]
    public static void NormalValuesWithContext()
    {
        var expected = new TestJsonObject
        {
            IntegerValue = 10,
            StringField = "Hello, world!",
            BoolField = true,
        };

        var json = JsonSerializer.Serialize<TestJsonObject>(expected, SerializationContext.Default.TestJsonObject);
        var actual = JsonSerializer.Deserialize<TestJsonObject>(json);
        Equal(10, actual.IntegerValue.Value);
        Equal("Hello, world!", actual.StringField.Value);
        True(actual.BoolField);
    }
}