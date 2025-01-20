using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

[InlineArray(BufferSize)]
internal struct PreallocatedCharBuffer
{
    private const int BufferSize = 64;
    
    private char element0;

    public Span<char> Span => MemoryMarshal.CreateSpan(ref element0, BufferSize);
}