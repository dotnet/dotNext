using System.Runtime.CompilerServices;

namespace DotNext.Runtime.InteropServices;

public sealed class UnmanagedMemoryTests : Test
{
    [Fact]
    public static void DefaultValue()
    {
        var memory = default(UnmanagedMemory<Int128>);
        False(memory.IsAllocated);
        Equal(Span<byte>.Empty, memory);
        True(Unsafe.IsNullRef(in memory.Value));

        ValueReference<Int128> reference = memory;
        True(reference.IsEmpty);
    }

    [Fact]
    public static void AllocatedValue()
    {
        var expected = 42L;
        using var memory = new UnmanagedMemory<long> { Value = expected };
        True(memory.IsAllocated);
        Equal(expected, memory.Value);

        Equal(Span.AsReadOnlyBytes(in expected), memory);
    }
}