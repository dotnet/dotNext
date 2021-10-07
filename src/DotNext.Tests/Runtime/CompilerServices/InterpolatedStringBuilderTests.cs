using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using static System.Globalization.CultureInfo;

namespace DotNext.Runtime.CompilerServices
{
    using Buffers;

    [ExcludeFromCodeCoverage]
    public sealed class InterpolatedStringBuilderTests : Test
    {
        private static LambdaExpression BuildTemplate(ref InterpolatedStringBuilder builder)
            => builder.Build();

        [Fact]
        public static void BuildRenderer()
        {
            int x = 10, y = 20;
            var expr = BuildTemplate($"{x:X} + {y:X} = {x + y:X}") as Expression<Func<IFormatProvider, int, int, int, MemoryAllocator<char>, string>>;
            NotNull(expr);

            var renderer = expr.Compile();

            Equal($"{x:X} + {y:X} = {x + y:X}", renderer(InvariantCulture, x, y, x + y, null));
        }
    }
}