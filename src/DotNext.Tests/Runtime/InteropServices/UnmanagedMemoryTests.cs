using System.Runtime.CompilerServices;

namespace DotNext.Runtime.InteropServices;

public sealed class UnmanagedMemoryTests : Test
{
    [Fact]
    public static void DefaultValue()
    {
        using var memory = default(UnmanagedMemory<Int128>);
        True(memory.Pointer.IsNull);
        True(Unsafe.IsNullRef(in memory.Pointer.Value));

        ValueReference<Int128> reference = memory.Pointer;
        True(reference.IsEmpty);
    }

    [Fact]
    public static void AllocatedValue()
    {
        const long expected = 42L;
        using var memory = new UnmanagedMemory<long>(expected);
        False(memory.Pointer.IsNull);
        Equal(expected, memory.Pointer.Value);

        ValueReference<long> reference = memory.Pointer;
        False(reference.IsEmpty);
        Equal(expected, reference.Value);
    }

    [Fact]
    public static void UnmanagedMemoryInterface()
    {
        var expected = 42L;
        IUnmanagedMemory memory = new UnmanagedMemory<long>(expected);
        Equal(Span.AsReadOnlyBytes(in expected), memory.Bytes);
        Equal((uint)sizeof(ulong), memory.Size);
        Equal(memory.Pointer.ToString(), memory.ToString());
    }

    [Fact]
    public static async Task ByRefParameter()
    {
        using var memory = new UnmanagedMemory<long>();
        await DoAsync(memory.Pointer);
        Equal(42L, memory.Pointer.Value);
        
        static async Task DoAsync(ValueReference<long> byRef)
        {
            await Task.Yield();
            byRef.Value = 42L;
        }
    }
}