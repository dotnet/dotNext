using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Xunit;

namespace DotNext.Linq.Expressions
{
    [ExcludeFromCodeCoverage]
    public sealed class InterpolationTests : Assert
    {
        [Fact]
        public static void PlainString()
        {
            var str = InterpolationExpression.PlainString($"Hello, {"Sally".Const()}");
            NotEmpty(str.Arguments);
            Equal(typeof(string), str.Type);
            Equal("Hello, {0}", str.Format);
            IsType<ConstantExpression>(str.Arguments[0]);
        }

        [Fact]
        public static void FormattableString()
        {
            var str = InterpolationExpression.FormattableString($"Hello, {"Sally".Const()}");
            NotEmpty(str.Arguments);
            Equal(typeof(FormattableString), str.Type);
            Equal("Hello, {0}", str.Format);
            IsType<ConstantExpression>(str.Arguments[0]);
        }
    }
}
