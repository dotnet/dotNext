using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.IO;

using Buffers;

public partial class FileWriter
{
    /// <inheritdoc/>
    void IBufferWriter<byte>.Advance(int count)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)FreeCapacity, nameof(count));

        if (count > 0 && buffer.IsEmpty)
            buffer = Allocator.AllocateExactly(maxBufferSize);

        bufferOffset += count;

        if (bufferOffset == maxBufferSize)
            Flush();
    }

    private void EnsureBufferSize(int sizeHint, [CallerArgumentExpression(nameof(sizeHint))] string? paramName = null)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint, paramName);

        if (sizeHint > maxBufferSize)
            throw new InsufficientMemoryException();
        
        if (sizeHint is 0 && bufferOffset == maxBufferSize || sizeHint > FreeCapacity)
        {
            RandomAccess.Write(handle, buffer.Span.Slice(0, bufferOffset), fileOffset);
            fileOffset += bufferOffset;
            bufferOffset = 0;
        }
        else if (buffer.IsEmpty)
        {
            buffer = Allocator.AllocateExactly(maxBufferSize);
        }
    }

    /// <inheritdoc/>
    Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint)
    {
        EnsureBufferSize(sizeHint);
        
        Debug.Assert(!buffer.IsEmpty);
        return buffer.Memory.Slice(bufferOffset);
    }

    /// <inheritdoc/>
    Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint)
    {
        EnsureBufferSize(sizeHint);
        
        Debug.Assert(!buffer.IsEmpty);
        return buffer.Span.Slice(bufferOffset);
    }
}