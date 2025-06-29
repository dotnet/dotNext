using System.Buffers;

namespace DotNext.Runtime.InteropServices;

partial struct Pointer<T> : IPinnable
{
    /// <inheritdoc />
    public unsafe MemoryHandle Pin(int elementIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
        
        if (IsNull)
            ThrowNullPointerException();

        return new(value + elementIndex);
    }

    /// <inheritdoc />
    void IPinnable.Unpin()
    {
    }
}