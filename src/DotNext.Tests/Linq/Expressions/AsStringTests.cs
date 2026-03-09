using System.Text;

namespace DotNext.Linq.Expressions;

public sealed class AsStringTests : Test
{
    [Fact]
    public static void IntToString()
    {
        var str = 20.Quoted.AsString();
        Equal(typeof(int), str.Object.Type);
        Equal(typeof(int), str.Method.DeclaringType);
    }

    [Fact]
    public static void DecimalToString()
    {
        var str = 20M.Quoted.AsString();
        Equal(typeof(decimal), str.Object.Type);
        Equal(typeof(decimal), str.Method.DeclaringType);
    }

    [Fact]
    public static void ObjectToString()
    {
        var str = new object().Quoted.AsString();
        Equal(typeof(object), str.Object.Type);
        Equal(typeof(object), str.Method.DeclaringType);
    }

    [Fact]
    public static void StringBuilderToString()
    {
        var str = new StringBuilder("abc").Quoted.AsString();
        Equal(typeof(StringBuilder), str.Object.Type);
        Equal(typeof(StringBuilder), str.Method.DeclaringType);
    }

    [Fact]
    public static void RandomToString()
    {
        var str = new Random().Quoted.AsString();
        Equal(typeof(Random), str.Object.Type);
        Equal(typeof(object), str.Method.DeclaringType);
    }
}