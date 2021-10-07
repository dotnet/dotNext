using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

internal unsafe struct PreallocatedCharBuffer
{
    private const int BufferSize = 64;

    private fixed char buffer[BufferSize];

    public Span<char> Span => MemoryMarshal.CreateSpan(ref buffer[0], BufferSize);
}