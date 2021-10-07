using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using static System.Globalization.CultureInfo;

namespace DotNext.Runtime.CompilerServices
{
    using Buffers;

    [ExcludeFromCodeCoverage]
    public sealed class InterpolatedStringBuilderTests : Test
    {
        [Fact]
        public static void BuildRenderer()
        {
            var expr = BuildTemplate($"{typeof(int):X} + {typeof(int):X} = {typeof(int):X}") as Expression<Func<IFormatProvider, int, int, int, MemoryAllocator<char>, string>>;
            NotNull(expr);

            var renderer = expr.Compile();

            int x = 10, y = 20;
            Equal($"{x:X} + {y:X} = {x + y:X}", renderer(InvariantCulture, x, y, x + y, null));

            static LambdaExpression BuildTemplate(ref InterpolatedStringBuilder builder)
                => builder.Build();
        }

        [Fact]
        public static void GenerateTemplate()
        {
            var template = BuildTemplate($"{typeof(int),1:X} + {typeof(int):X} = {typeof(int):X}");

            Equal(@"{0,1:X} + {1:X} = {2:X}", template);

            static string BuildTemplate(ref InterpolatedStringBuilder builder)
                => builder.ToString();
        }
    }
}