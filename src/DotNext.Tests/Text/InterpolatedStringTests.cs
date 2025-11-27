namespace DotNext.Text;

public sealed class InterpolatedStringTests : Test
{
    [Fact]
    public static void AllocateString()
    {
        int x = 10, y = 20;
        using var actual = InterpolatedString.Interpolate(null, $"{x} + {y} = {x + y}");
        Equal($"{x} + {y} = {x + y}", actual.Span.ToString());
    }
}