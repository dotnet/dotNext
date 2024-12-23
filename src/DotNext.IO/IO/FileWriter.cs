using System.Diagnostics;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO;

using Buffers;

/// <summary>
/// Represents buffered file writer.
/// </summary>
/// <remarks>
/// This class is not thread-safe. However, it's possible to share the same file
/// handle across multiple writers and use dedicated writer in each thread.
/// </remarks>
public partial class FileWriter : Disposable, IFlushable, IResettable
{
    /// <summary>
    /// Represents the file handle.
    /// </summary>
    protected readonly SafeFileHandle handle;
    private readonly MemoryAllocator<byte>? allocator;
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fileOffset"/> is less than zero;
    /// or <paramref name="bufferSize"/> to small.
    /// </exception>
    public FileWriter(SafeFileHandle handle, long fileOffset = 0L, int bufferSize = 4096, MemoryAllocator<byte>? allocator = null)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bufferSize, 16);

        MaxBufferSize = bufferSize;
        this.handle = handle;
        this.fileOffset = fileOffset;
        this.allocator = allocator;
    }

    /// <summary>
    /// Creates a new writer backed by the file.
    /// </summary>
    /// <param name="destination">Writable file stream.</param>
    /// <param name="bufferSize">The buffer size.</param>
    /// <param name="allocator">The buffer allocator.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is not writable.</exception>
    public FileWriter(FileStream destination, int bufferSize = 4096, MemoryAllocator<byte>? allocator = null)
        : this(destination.SafeFileHandle, destination.Position, bufferSize, allocator)
    {
        if (!destination.CanWrite)
            throw new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination));
    }

    /// <summary>
    /// Gets written part of the buffer.
    /// </summary>
    public ReadOnlyMemory<byte> WrittenBuffer => buffer.Memory.Slice(0, bufferOffset);

    private int FreeCapacity => MaxBufferSize - bufferOffset;

    private ref readonly MemoryOwner<byte> EnsureBufferAllocated()
    {
        ref var result = ref buffer;
        if (result.IsEmpty)
            result = allocator.AllocateAtLeast(MaxBufferSize);
        
        Debug.Assert(!result.IsEmpty);
        return ref result;
    }

    [Conditional("DEBUG")]
    private void AssertState()
    {
        Debug.Assert(bufferOffset <= buffer.Length, $"Offset = {bufferOffset}, Buffer Size = {buffer.Length}");
    }

    /// <summary>
    /// The remaining part of the internal buffer available for write.
    /// </summary>
    /// <remarks>
    /// The size of returned buffer may be less than or equal to <see cref="MaxBufferSize"/>.
    /// </remarks>
    public Memory<byte> Buffer => EnsureBufferAllocated().Memory.Slice(bufferOffset);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Span<byte> BufferSpan => EnsureBufferAllocated().Span.Slice(bufferOffset);

    /// <summary>
    /// Gets the maximum available buffer size.
    /// </summary>
    public int MaxBufferSize { get; }

    /// <summary>
    /// Marks the specified number of bytes in the buffer as produced.
    /// </summary>
    /// <param name="count">The number of produced bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is larger than the length of <see cref="Buffer"/>.</exception>
    public void Produce(int count)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)FreeCapacity, nameof(count));

        if (count > 0 && buffer.IsEmpty)
            buffer = allocator.AllocateAtLeast(MaxBufferSize);
        
        bufferOffset += count;
    }

    /// <summary>
    /// Tries to write the data to the internal buffer.
    /// </summary>
    /// <param name="input">The input data to be copied.</param>
    /// <returns><see langword="true"/> if the internal buffer has enough space to place the data from <paramref name="input"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryWrite(ReadOnlySpan<byte> input)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        bool result;
        if (result = input.Length <= FreeCapacity)
        {
            input.CopyTo(BufferSpan);
            bufferOffset += input.Length;
        }

        return result;
    }

    /// <summary>
    /// Drops all buffered data.
    /// </summary>
    public void Reset()
    {
        bufferOffset = 0;
        buffer.Dispose();
    }

    /// <summary>
    /// Gets a value indicating that this writer has buffered data.
    /// </summary>
    public bool HasBufferedData
    {
        get
        {
            AssertState();
            
            return bufferOffset > 0;
        }
    }

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
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (HasBufferedData)
                throw new InvalidOperationException();

            fileOffset = value;
        }
    }

    /// <summary>
    /// Gets write position.
    /// </summary>
    /// <remarks>
    /// The returned value may be larger than <see cref="FilePosition"/> because the writer
    /// performs buffered write.
    /// </remarks>
    public long WritePosition => fileOffset + bufferOffset;

    private ValueTask FlushAsync(CancellationToken token)
        => Submit(RandomAccess.WriteAsync(handle, WrittenBuffer, fileOffset, token), WriteCallback);

    private void Flush()
    {
        RandomAccess.Write(handle, WrittenBuffer.Span, fileOffset);
        fileOffset += bufferOffset;
        Reset();
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

        AssertState();
        return HasBufferedData ? FlushAsync(token) : ValueTask.CompletedTask;
    }

    /// <summary>
    /// Flushes the operating system buffers for the given file to disk.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    public void FlushToDisk()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        AssertState();
        RandomAccess.FlushToDisk(handle);
    }

    /// <inheritdoc />
    Task IFlushable.FlushAsync(CancellationToken token) => WriteAsync(token).AsTask();

    /// <summary>
    /// Flushes buffered data to the file.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    public void Write()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (HasBufferedData)
            Flush();
    }

    /// <inheritdoc />
    void IFlushable.Flush() => Write();

    private void WriteSlow(ReadOnlySpan<byte> input)
    {
        if (input.Length >= MaxBufferSize)
        {
            RandomAccess.Write(handle, WrittenBuffer.Span, fileOffset);
            fileOffset += bufferOffset;

            RandomAccess.Write(handle, input, fileOffset);
            fileOffset += input.Length;
            Reset();
        }
        else
        {
            RandomAccess.Write(handle, WrittenBuffer.Span, fileOffset);
            fileOffset += bufferOffset;
            input.CopyTo(EnsureBufferAllocated().Span);
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
        ValueTask task;
        
        if (IsDisposed)
        {
            task = new(DisposedTask);
        }
        else if (input.IsEmpty)
        {
            task = new();
        }
        else
        {
            AssertState();
            var freeCapacity = FreeCapacity;
            switch (input.Length.CompareTo(freeCapacity))
            {
                case < 0:
                    input.CopyTo(Buffer);
                    bufferOffset += input.Length;
                    task = new();
                    break;
                case 0:
                    task = WriteDirectAsync(input, token);
                    break;
                case > 0 when input.Length < MaxBufferSize:
                    task = WriteAndCopyAsync(input, token);
                    break;
                default:
                    goto case 0;
            }
        }

        return task;
    }

    private ValueTask WriteDirectAsync(ReadOnlyMemory<byte> input, CancellationToken token)
    {
        ValueTask task;
        if (HasBufferedData)
        {
            secondBuffer = input;
            task = RandomAccess.WriteAsync(handle, (IReadOnlyList<ReadOnlyMemory<byte>>)this, fileOffset, token);
        }
        else
        {
            bufferOffset = input.Length;
            task = RandomAccess.WriteAsync(handle, input, fileOffset, token);
        }

        return Submit(task, WriteCallback);
    }

    private ValueTask WriteAndCopyAsync(ReadOnlyMemory<byte> input, CancellationToken token)
    {
        Debug.Assert(HasBufferedData);

        secondBuffer = input;
        return Submit(RandomAccess.WriteAsync(handle, WrittenBuffer, fileOffset, token), WriteAndCopyCallback);
    }

    /// <summary>
    /// Writes the data to the file through the buffer.
    /// </summary>
    /// <param name="input">The input data to write.</param>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public void Write(ReadOnlySpan<byte> input)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        AssertState();
        if (input.Length <= FreeCapacity)
        {
            input.CopyTo(BufferSpan);
            bufferOffset += input.Length;
        }
        else
        {
            WriteSlow(input);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            writeCallback = writeAndCopyCallback = null;
            buffer.Dispose();
        }

        fileOffset = 0L;
        bufferOffset = 0;

        base.Dispose(disposing);
    }
}