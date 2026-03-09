using System.Diagnostics;

namespace DotNext.IO;

partial class PoolingBufferedStream : IAsyncBinaryWriter
{
    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    Memory<byte> IAsyncBinaryWriter.Buffer
    {
        get
        {
            ThrowIfDisposed();
            EnsureReadBufferIsEmpty();

            return EnsureBufferAllocated().Memory.Slice(writePosition);
        }
    }

    /// <inheritdoc/>
    ValueTask IAsyncBinaryWriter.AdvanceAsync(int count, CancellationToken token)
    {
        AssertState();

        if (stream is null)
            return new(DisposedTask);

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

    /// <summary>
    /// Tries to get the underlying buffer for write.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Write(int)"/> to commit the written bytes.
    /// </remarks>
    /// <param name="minimumSize">The minimum size of the requested buffer.</param>
    /// <param name="buffer">The writable buffer.</param>
    /// <returns>
    /// <see langword="true"/> if the underlying buffer is at least of size <paramref name="minimumSize"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetWriteBuffer(int minimumSize, out Memory<byte> buffer)
    {
        var freeCapacity = maxBufferSize - writePosition;
        if ((uint)minimumSize > (uint)freeCapacity || HasBufferedDataToRead)
        {
            buffer = Memory<byte>.Empty;
            return false;
        }

        buffer = EnsureBufferAllocated().Memory.Slice(writePosition);
        return true;
    }

    /// <summary>
    /// Marks the specified number of bytes in the internal buffer as written.
    /// </summary>
    /// <param name="count">The number of bytes to commit.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is larger than the available internal buffer.</exception>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    /// <exception cref="InvalidOperationException">The underlying read buffer is not empty.</exception>
    /// <seealso cref="TryGetWriteBuffer"/>
    public void Write(int count)
    {
        ThrowIfDisposed();
        EnsureReadBufferIsEmpty();

        var freeCapacity = maxBufferSize - writePosition;
        if ((uint)count > (uint)freeCapacity || buffer.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(count));

        writePosition += count;
    }
}