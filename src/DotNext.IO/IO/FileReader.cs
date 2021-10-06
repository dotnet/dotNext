using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO;

using Buffers;

/// <summary>
/// Represents buffered file reader.
/// </summary>
public partial class FileReader : Disposable
{
    private readonly SafeFileHandle handle;
    private MemoryOwner<byte> buffer;
    private int bufferStart, bufferEnd;
    private long fileOffset;

    /// <summary>
    /// Initializes a new buffered file reader.
    /// </summary>
    /// <param name="handle">The file handle.</param>
    /// <param name="fileOffset">The initial offset within the file.</param>
    /// <param name="bufferSize">The buffer size.</param>
    /// <param name="allocator">The buffer allocator.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fileOffset"/> is less than zero;
    /// or <paramref name="bufferSize"/> is less than 16 bytes.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is not opened in asynchronous mode.</exception>
    public FileReader(SafeFileHandle handle, long fileOffset = 0L, int bufferSize = 4096, MemoryAllocator<byte>? allocator = null)
    {
        ArgumentNullException.ThrowIfNull(handle, nameof(handle));

        if (!handle.IsAsync)
            throw new ArgumentException(ExceptionMessages.AsyncFileExpected, nameof(handle));

        if (fileOffset < 0L)
            throw new ArgumentOutOfRangeException(nameof(fileOffset));

        if (bufferSize <= 16)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        buffer = allocator.Invoke(bufferSize, false);
        this.handle = handle;
        this.fileOffset = fileOffset;
    }

    /// <summary>
    /// Gets or sets the cursor position within the file.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
    /// <exception cref="InvalidOperationException">There is buffered data present. Call <see cref="Consume(int)"/> or <see cref="ClearBuffer"/> before changing the position.</exception>
    public long FilePosition
    {
        get => fileOffset;
        set
        {
            if (value < 0L)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (HasBufferedData)
                throw new InvalidOperationException();

            fileOffset = value;
        }
    }

    /// <summary>
    /// Gets read position within the file.
    /// </summary>
    /// <remarks>
    /// The returned value may be larger than <see cref="FilePosition"/> because the reader
    /// performs buffered read.
    /// </remarks>
    public long ReadPosition => fileOffset + BufferLength;

    private int BufferLength => bufferEnd - bufferStart;

    /// <summary>
    /// Gets unconsumed part of the buffer.
    /// </summary>
    public ReadOnlyMemory<byte> Buffer => buffer.Memory.Slice(bufferStart, BufferLength);

    /// <summary>
    /// Gets a value indicating that the read buffer is not empty.
    /// </summary>
    public bool HasBufferedData => bufferStart < bufferEnd;

    /// <summary>
    /// Gets the maximum possible amount of data that can be placed to the buffer.
    /// </summary>
    public int MaxBufferSize => buffer.Length;

    /// <summary>
    /// Advances read position.
    /// </summary>
    /// <param name="bytes">The number of consumed bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytes"/> is larger than the length of <see cref="Buffer"/>.</exception>
    public void Consume(int bytes)
    {
        var newPosition = bytes + bufferStart;

        if ((uint)newPosition > (uint)bufferEnd)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        if (newPosition == bufferEnd)
        {
            ClearBuffer();
        }
        else
        {
            bufferStart = newPosition;
        }

        fileOffset += bytes;
    }

    /// <summary>
    /// Clears the read buffer.
    /// </summary>
    public void ClearBuffer() => bufferStart = bufferEnd = 0;

    /// <summary>
    /// Reads the data from the file to the underlying buffer.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the data has been copied from the file to the internal buffer;
    /// <see langword="false"/> if no more data to read.
    /// </returns>
    /// <exception cref="InvalidOperationException">Internal buffer has no free space.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask<bool> ReadAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();

        var buffer = this.buffer.Memory;

        switch (bufferStart)
        {
            case 0 when bufferEnd == buffer.Length:
                throw new InvalidOperationException();
            case > 0:
                // compact buffer
                buffer.Slice(bufferStart, BufferLength).CopyTo(buffer);
                bufferEnd -= bufferStart;
                bufferStart = 0;
                break;
        }

        var count = await RandomAccess.ReadAsync(handle, buffer.Slice(bufferEnd), fileOffset + bufferEnd, token).ConfigureAwait(false);
        bufferEnd += count;
        return count > 0;
    }

    /// <summary>
    /// Reads the block of the memory.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of bytes copied to <paramref name="output"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<int> ReadAsync(Memory<byte> output, CancellationToken token = default)
    {
        if (IsDisposed)
            return new(GetDisposedTask<int>());

        if (output.IsEmpty)
            return new(0);

        return HasBufferedData || output.Length < buffer.Length
            ? ReadBufferedAsync(output, token)
            : ReadDirectAsync(output, token);

        async ValueTask<int> ReadDirectAsync(Memory<byte> output, CancellationToken token)
        {
            var count = await RandomAccess.ReadAsync(handle, output, fileOffset, token).ConfigureAwait(false);
            fileOffset += count;
            return count;
        }

        async ValueTask<int> ReadBufferedAsync(Memory<byte> output, CancellationToken token)
        {
            var result = 0;

            for (int writtenCount; !output.IsEmpty; output = output.Slice(writtenCount))
            {
                if (!HasBufferedData && !await ReadAsync(token).ConfigureAwait(false))
                    break;

                Buffer.Span.CopyTo(output.Span, out writtenCount);
                result += writtenCount;
                Consume(writtenCount);
            }

            return result;
        }
    }

    /// <summary>
    /// Skips the specified number of bytes and advances file read cursor.
    /// </summary>
    /// <param name="bytes">The number of bytes to skip.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytes"/> is less than zero.</exception>
    public void Skip(long bytes)
    {
        ThrowIfDisposed();

        if (bytes < 0L)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        if (bytes < BufferLength)
        {
            Consume((int)bytes);
        }
        else
        {
            ClearBuffer();
            fileOffset += bytes;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            buffer.Dispose();
        }

        fileOffset = 0L;
        bufferStart = bufferEnd = 0;

        base.Dispose(disposing);
    }
}