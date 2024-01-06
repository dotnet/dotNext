using System.Buffers;

namespace DotNext.IO;

public partial class FileWriter : IBufferWriter<byte>
{
    /// <inheritdoc />
    void IBufferWriter<byte>.Advance(int count) => Produce(count);

    /// <inheritdoc />
    Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        var result = Buffer;
        return sizeHint <= result.Length ? result : throw new InsufficientMemoryException();
    }

    /// <inheritdoc />
    Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        var result = BufferSpan;
        return sizeHint <= result.Length ? result : throw new InsufficientMemoryException();
    }
}