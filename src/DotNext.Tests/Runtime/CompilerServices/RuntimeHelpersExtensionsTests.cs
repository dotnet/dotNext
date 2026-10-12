using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices;

public class RuntimeHelpersExtensionsTests : Test
{
    [Fact]
    public static void IsOnStack()
    {
        True(RuntimeHelpers.IsStackDetectionSupported);
        var i = 42;
        True(RuntimeHelpers.IsOnExecutionStack(in i));
        False(RuntimeHelpers.IsOnExecutionStack(in new StrongBox<int>().Value));
    }

    [Fact]
    public static void ReadOnlySpanLocation()
    {
        Span<byte> span = stackalloc byte[8];
        True(span.IsStackAllocated);

        span = new byte[8];
        False(span.IsStackAllocated);
    }
}