using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions;

[ExcludeFromCodeCoverage]
public sealed class InterpolationTests : Test
{
    [Fact]
    public static void PlainString()
    {
        var str = InterpolationExpression.PlainString($"Hello, {"Sally".Const()}");
        NotEmpty(str.Arguments);
        Equal(typeof(string), str.Type);
        Equal("Hello, {0}", str.Format);
        IsType<ConstantExpression>(str.Arguments[0]);
        IsAssignableFrom<MethodCallExpression>(str.Reduce());
    }

    [Fact]
    public static void FormattableString()
    {
        var str = InterpolationExpression.FormattableString($"Hello, {"Sally".Const()}");
        NotEmpty(str.Arguments);
        Equal(typeof(FormattableString), str.Type);
        Equal("Hello, {0}", str.Format);
        IsType<ConstantExpression>(str.Arguments[0]);
        IsAssignableFrom<MethodCallExpression>(str.Reduce());
    }

    [Fact]
    public static void InterpolatedString()
    {
        var str = InterpolationExpression.Create($"Hello, {"Sally".Const()}");
        NotEmpty(str.Arguments);
        Equal(typeof(string), str.Type);
        Equal("Hello, {0}", str.Format);
        IsAssignableFrom<InvocationExpression>(str.Reduce());
    }
}