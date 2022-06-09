using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DotNext.Linq.Expressions;

[ExcludeFromCodeCoverage]
public sealed class AsStringTests : Test
{
    [Fact]
    public static void IntToString()
    {
        var str = 20.Const().AsString();
        Equal(typeof(int), str.Object.Type);
        Equal(typeof(int), str.Method.DeclaringType);
    }

    [Fact]
    public static void DecimalToString()
    {
        var str = 20M.Const().AsString();
        Equal(typeof(decimal), str.Object.Type);
        Equal(typeof(decimal), str.Method.DeclaringType);
    }

    [Fact]
    public static void ObjectToString()
    {
        var str = new object().Const().AsString();
        Equal(typeof(object), str.Object.Type);
        Equal(typeof(object), str.Method.DeclaringType);
    }

    [Fact]
    public static void StringBuilderToString()
    {
        var str = new StringBuilder("abc").Const().AsString();
        Equal(typeof(StringBuilder), str.Object.Type);
        Equal(typeof(StringBuilder), str.Method.DeclaringType);
    }

    [Fact]
    public static void RandomToString()
    {
        var str = new Random().Const().AsString();
        Equal(typeof(Random), str.Object.Type);
        Equal(typeof(object), str.Method.DeclaringType);
    }
}