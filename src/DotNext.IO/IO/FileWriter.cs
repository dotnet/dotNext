using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO;

using Buffers;

/// <summary>
/// Represents buffered file writer.
/// </summary>
public sealed partial class FileWriter : Disposable
{
    private readonly SafeFileHandle handle;
    private MemoryOwner<byte> buffer;
    private int bufferOffset;
    private long fileOffset;

    /// <summary>
    /// Creates a new writer backed by the file.
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
    public FileWriter(SafeFileHandle handle, long fileOffset = 0L, int bufferSize = 4096, MemoryAllocator<byte>? allocator = null)
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

    private ReadOnlyMemory<byte> WrittenMemory => buffer.Memory.Slice(0, bufferOffset);

    private int FreeCapacity => buffer.Length - bufferOffset;

    /// <summary>
    /// The remaining part of the internal buffer available for write.
    /// </summary>
    /// <remarks>
    /// The size of returned buffer may be less than or equal to <see cref="MaxBufferSize"/>.
    /// </remarks>
    public Memory<byte> Buffer => buffer.Memory.Slice(bufferOffset);

    /// <summary>
    /// Gets the maximum available buffer size.
    /// </summary>
    public int MaxBufferSize => buffer.Length;

    /// <summary>
    /// Marks the specified number of bytes in the buffer as consumed.
    /// </summary>
    /// <param name="bytes">The number of consumed bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytes"/> is larger than the length of <see cref="Buffer"/>.</exception>
    public void Produce(int bytes)
    {
        if (bytes > FreeCapacity)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        bufferOffset += bytes;
    }

    /// <summary>
    /// Marks the entire buffer as written.
    /// </summary>
    public void Produce() => bufferOffset = buffer.Length;

    /// <summary>
    /// Drops all buffered data.
    /// </summary>
    public void ClearBuffer() => bufferOffset = 0;

    /// <summary>
    /// Gets a value indicating that this writer has buffered data.
    /// </summary>
    public bool HasBufferedData => bufferOffset > 0;

    /// <summary>
    /// Gets or sets the cursor position within the file.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
    /// <exception cref="InvalidOperationException">There is buffered data present. Call <see cref="ClearBuffer"/> or <see cref="WriteAsync(CancellationToken)"/> before changing the position.</exception>
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

    private async ValueTask FlushCoreAsync(CancellationToken token)
    {
        await RandomAccess.WriteAsync(handle, WrittenMemory, fileOffset, token).ConfigureAwait(false);
        fileOffset += bufferOffset;
        bufferOffset = 0;
    }

    /// <summary>
    /// Flushes buffered data to the file.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WriteAsync(CancellationToken token = default)
    {
        if (IsDisposed)
            return new(DisposedTask);

        if (token.IsCancellationRequested)
            return ValueTask.FromCanceled(token);

        if (HasBufferedData)
            return FlushCoreAsync(token);

        return ValueTask.CompletedTask;
    }

    private async ValueTask WriteSlowAsync(ReadOnlyMemory<byte> input, CancellationToken token)
    {
        if (bufferOffset > 0)
            await FlushCoreAsync(token).ConfigureAwait(false);

        if (input.Length > buffer.Length)
        {
            await RandomAccess.WriteAsync(handle, input, fileOffset, token).ConfigureAwait(false);
            fileOffset += input.Length;
        }
        else
        {
            input.CopyTo(buffer.Memory);
            bufferOffset += input.Length;
        }
    }

    /// <summary>
    /// Writes the data to the file through the buffer.
    /// </summary>
    /// <param name="input">The input data to write.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token = default)
    {
        if (IsDisposed)
            return new(DisposedTask);

        if (input.Length <= FreeCapacity)
        {
            input.CopyTo(Buffer);
            bufferOffset += input.Length;
            return ValueTask.CompletedTask;
        }

        return WriteSlowAsync(input, token);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            buffer.Dispose();
        }

        fileOffset = 0L;
        bufferOffset = 0;

        base.Dispose(disposing);
    }
}