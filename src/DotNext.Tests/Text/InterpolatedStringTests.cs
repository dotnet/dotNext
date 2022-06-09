using System.Diagnostics.CodeAnalysis;

namespace DotNext.Text;

using Buffers;

[ExcludeFromCodeCoverage]
public sealed class InterpolatedStringTests : Test
{
    [Fact]
    public static void AllocateString()
    {
        int x = 10, y = 20;
        using var actual = InterpolatedString.Allocate(null, $"{x} + {y} = {x + y}");
        Equal($"{x} + {y} = {x + y}", actual.Span.ToString());
    }

    [Fact]
    public static void BuildString()
    {
        IGrowableBuffer<char> builder = new PoolingInterpolatedStringHandler(1, 0, null);

        try
        {
            Null(builder.Capacity);
            Equal(0, builder.WrittenCount);
            True(builder.TryGetWrittenContent(out _));
            builder.Write("Hello, world!");
            Equal(13, builder.WrittenCount);

            Span<char> str = stackalloc char[32];
            builder.CopyTo(str);
            Equal("Hello, world!", str.Slice(0, (int)builder.WrittenCount).ToString());
            Equal("Hello, world!", builder.ToString());
        }
        finally
        {
            ((IDisposable)builder).Dispose();
        }
    }
}