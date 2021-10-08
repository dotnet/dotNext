using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using static System.Globalization.CultureInfo;

namespace DotNext.Runtime.CompilerServices
{
    using Buffers;

    [ExcludeFromCodeCoverage]
    public sealed class InterpolatedStringTemplateBuilderTests : Test
    {
        [Fact]
        public static void BuildRenderer()
        {
            var template = BuildTemplate($"{typeof(int):X} + {typeof(int):X} = {typeof(int):X}").Compile() as Func<IFormatProvider, int, int, int, MemoryAllocator<char>, string>;
            NotNull(template);

            int x = 10, y = 20;
            Equal($"{x:X} + {y:X} = {x + y:X}", template(InvariantCulture, x, y, x + y, null));

            static LambdaExpression BuildTemplate(ref InterpolatedStringTemplateBuilder builder)
                => builder.Build();
        }

        [Fact]
        public static void GenerateTemplate()
        {
            var template = BuildTemplate($"{typeof(int),1:X} + {typeof(int):X} = {typeof(int):X}");

            Equal(@"{0,1:X} + {1:X} = {2:X}", template);

            static string BuildTemplate(ref InterpolatedStringTemplateBuilder builder)
                => builder.ToString();
        }
    }
}