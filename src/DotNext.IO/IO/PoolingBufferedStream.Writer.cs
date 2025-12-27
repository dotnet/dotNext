using System.Diagnostics;

namespace DotNext.IO;

using Buffers;

partial class PoolingBufferedStream : IAsyncBinaryWriter, IBufferedWriter
{
    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    Memory<byte> IAsyncBinaryWriter.Buffer
    {
        get
        {
            ThrowIfDisposed();
            EnsureReadBufferIsEmpty();

            return WriteBuffer;
        }
    }

    /// <inheritdoc/>
    ValueTask IAsyncBinaryWriter.AdvanceAsync(int count, CancellationToken token)
    {
        AssertState();
        ThrowIfDisposed();

        if (HasBufferedDataToRead)
            return ValueTask.FromException(new InvalidOperationException(ExceptionMessages.ReadBufferNotEmpty));

        var freeCapacity = maxBufferSize - writePosition;

        if ((uint)count > (uint)freeCapacity || buffer.IsEmpty)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(count)));

        writePosition += count;

        return writePosition == maxBufferSize
            ? WriteAndResetAsync(token)
            : ValueTask.CompletedTask;
    }

    private async ValueTask WriteAndResetAsync(CancellationToken token)
    {
        await WriteAndResetAsync(WrittenMemory, token).ConfigureAwait(false);
        Reset();
    }

    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    Memory<byte> IBufferedWriter.Buffer
    {
        get
        {
            ThrowIfDisposed();
            EnsureReadBufferIsEmpty();

            return WriteBuffer;
        }
    }

    /// <inheritdoc/>
    void IBufferedWriter.Produce(int count)
    {
        ThrowIfDisposed();
        EnsureReadBufferIsEmpty();

        var freeCapacity = maxBufferSize - writePosition;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)freeCapacity, nameof(count));

        if (count > 0 && buffer.IsEmpty)
            buffer = Allocator.AllocateExactly(maxBufferSize);

        writePosition += count;
    }
}