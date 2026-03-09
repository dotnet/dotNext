using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO;

using Buffers;

/// <summary>
/// Represents buffered file reader.
/// </summary>
/// <remarks>
/// This class is not thread-safe. However, it's possible to share the same file
/// handle across multiple readers and use dedicated reader in each thread.
/// </remarks>
public partial class FileReader : Disposable
{
    private const int MinBufferSize = 16;
    private const int DefaultBufferSize = 4096;
    
    /// <summary>
    /// Represents the file handle.
    /// </summary>
    protected readonly SafeFileHandle handle;
    private readonly int maxBufferSize;
    private MemoryOwner<byte> buffer;
    private int bufferStart, bufferEnd;
    private long fileOffset;

    /// <summary>
    /// Initializes a new buffered file reader.
    /// </summary>
    /// <param name="handle">The file handle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
    public FileReader(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);

        maxBufferSize = DefaultBufferSize;
        this.handle = handle;
    }

    /// <summary>
    /// Initializes a new buffered file reader.
    /// </summary>
    /// <param name="source">Readable file stream.</param>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    public FileReader(FileStream source)
        : this(source.SafeFileHandle)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.EnsureReadable(source);

        FilePosition = source.Position;
    }

    /// <summary>
    /// Gets buffer allocator.
    /// </summary>
    [AllowNull]
    public MemoryAllocator<byte> Allocator
    {
        get => field ??= MemoryAllocator<byte>.Default;
        init;
    }

    /// <summary>
    /// Gets or sets the cursor position within the file.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than zero.</exception>
    /// <exception cref="InvalidOperationException">There is buffered data present. Call <see cref="Consume(int)"/> or <see cref="Reset"/> before changing the position.</exception>
    public long FilePosition
    {
        get => fileOffset;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (HasBufferedData)
                throw new InvalidOperationException(ExceptionMessages.ReadBufferNotEmpty);

            fileOffset = value;
        }
    }

    /// <summary>
    /// Gets the read position within the file.
    /// </summary>
    /// <remarks>
    /// The returned value may be larger than <see cref="FilePosition"/> because the reader
    /// performs buffered read.
    /// </remarks>
    public long ReadPosition => fileOffset + BufferLength;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int BufferLength => bufferEnd - bufferStart;

    /// <summary>
    /// Gets unconsumed part of the buffer.
    /// </summary>
    public ReadOnlyMemory<byte> Buffer => buffer.Memory[bufferStart..bufferEnd];

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ReadOnlySpan<byte> BufferSpan => buffer.Span[bufferStart..bufferEnd];

    private ref readonly MemoryOwner<byte> EnsureBufferAllocated()
    {
        ref var result = ref buffer;
        if (result.IsEmpty)
            result = Allocator.AllocateExactly(maxBufferSize);
        
        Debug.Assert(!result.IsEmpty);
        return ref result;
    }

    /// <summary>
    /// Gets a value indicating that the read buffer is not empty.
    /// </summary>
    public bool HasBufferedData => bufferStart < bufferEnd;

    /// <summary>
    /// Gets the maximum size of the internal buffer.
    /// </summary>
    public int MaxBufferSize
    {
        get => maxBufferSize;
        init => maxBufferSize = value >= MinBufferSize ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Advances read position.
    /// </summary>
    /// <param name="count">The number of consumed bytes.</param>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is larger than the length of <see cref="Buffer"/>.</exception>
    public void Consume(int count)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        
        var newPosition = count + bufferStart;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)newPosition, (uint)bufferEnd, nameof(count));

        Consume(count, newPosition);
    }

    private void ConsumeUnsafe(int count) => Consume(count, count + bufferStart);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Consume(int count, int newPosition)
    {
        Debug.Assert(newPosition == count + bufferStart);
        
        if (newPosition == bufferEnd)
        {
            Reset();
        }
        else
        {
            bufferStart = newPosition;
        }

        fileOffset += count;
    }
    
    private bool TryRead(int count, out ReadOnlyMemory<byte> buffer)
    {
        var newPosition = count + bufferStart;
        if ((uint)newPosition > (uint)bufferEnd)
        {
            buffer = default;
            return false;
        }

        buffer = this.buffer.Memory.Slice(bufferStart, count);
        if (newPosition == bufferEnd)
        {
            bufferStart = bufferEnd = 0;
        }
        else
        {
            bufferStart = newPosition;
        }

        fileOffset += count;
        return true;
    }

    private void ResetIfNeeded()
    {
        if (bufferStart == bufferEnd)
            Reset();
    }

    /// <summary>
    /// Clears the read buffer.
    /// </summary>
    public void Reset()
    {
        bufferStart = bufferEnd = 0;
        buffer.Dispose();
    }

    /// <summary>
    /// Fetches the data from the underlying storage to the internal buffer.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the data has been copied from the underlying storage to the internal buffer;
    /// <see langword="false"/> if no more data to read.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="InternalBufferOverflowException">Internal buffer has no free space.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<bool> ReadAsync(CancellationToken token = default)
    {
        return IsDisposed
            ? new(GetDisposedTask<bool>())
            : SubmitAsBoolean(ReadCoreAsync(token), ReadCallback);
    }

    private ValueTask<int> ReadCoreAsync(CancellationToken token)
    {
        return PrepareReadBuffer(out var readBuffer)
            ? RandomAccess.ReadAsync(handle, readBuffer.Slice(bufferEnd), fileOffset + bufferEnd, token)
            : ValueTask.FromException<int>(new InternalBufferOverflowException());
    }

    private bool PrepareReadBuffer(out Memory<byte> readBuffer)
    {
        switch (bufferStart)
        {
            case 0 when bufferEnd == maxBufferSize:
                readBuffer = default;
                return false;
            case > 0:
                readBuffer = buffer.Memory;
                
                // compact buffer
                readBuffer[bufferStart..bufferEnd].CopyTo(readBuffer);
                bufferEnd -= bufferStart;
                bufferStart = 0;
                break;
            default:
                readBuffer = EnsureBufferAllocated().Memory;
                break;
        }

        return true;
    }
    
    /// <summary>
    /// Reads the data from the file to the underlying buffer.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the data has been copied from the file to the internal buffer;
    /// <see langword="false"/> if no more data to read.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="InternalBufferOverflowException">Internal buffer has no free space.</exception>
    public bool Read()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        return ReadCore();
    }

    private bool ReadCore()
    {
        if (!PrepareReadBuffer(out var readBuffer))
            throw new InternalBufferOverflowException();

        var count = RandomAccess.Read(handle, readBuffer.Span.Slice(bufferEnd), fileOffset + bufferEnd);
        bufferEnd += count;
        
        ResetIfNeeded();
        return count > 0;
    }

    /// <summary>
    /// Reads the block of the memory.
    /// </summary>
    /// <param name="destination">The output buffer.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of bytes copied to <paramref name="destination"/>.</returns>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken token = default)
    {
        ValueTask<int> task;
        if (IsDisposed)
        {
            task = new(GetDisposedTask<int>());
        }
        else if (destination.IsEmpty)
        {
            task = new(result: 0);
        }
        else
        {
            extraCount = ReadFromBuffer(destination.Span);
            destination = destination.Slice(extraCount);

            if (destination.Length > maxBufferSize)
            {
                task = ReadDirectAsync(destination, token);
            }
            else if (destination.IsEmpty)
            {
                task = new(extraCount);
            }
            else
            {
                destinationBuffer = destination;
                task = SubmitAsInt32(ReadCoreAsync(token), ReadAndCopyCallback);
            }
        }

        return task;
    }

    private ValueTask<int> ReadDirectAsync(Memory<byte> output, CancellationToken token)
        => SubmitAsInt32(RandomAccess.ReadAsync(handle, output, fileOffset, token), ReadDirectCallback);

    /// <summary>
    /// Reads the block of the memory.
    /// </summary>
    /// <param name="destination">The output buffer.</param>
    /// <returns>The number of bytes copied to <paramref name="destination"/>.</returns>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    public int Read(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        int count;
        if (destination.IsEmpty)
        {
            count = 0;
        }
        else
        {
            count = ReadFromBuffer(destination);
            destination = destination.Slice(count);
            if (destination.Length > maxBufferSize)
            {
                var directBytes = RandomAccess.Read(handle, destination, fileOffset);
                fileOffset += directBytes;
                count += directBytes;
            }
            else if (!destination.IsEmpty && ReadCore())
            {
                count += ReadFromBuffer(destination);
            }
        }

        return count;
    }

    private int ReadFromBuffer(Span<byte> destination)
    {
        var bytesCopied = BufferSpan >>> destination;
        ConsumeUnsafe(bytesCopied);
        return bytesCopied;
    }

    /// <summary>
    /// Skips the specified number of bytes and advances file read cursor.
    /// </summary>
    /// <param name="count">The number of bytes to skip.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
    public void Skip(long count)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count < BufferLength)
        {
            ConsumeUnsafe((int)count);
        }
        else
        {
            Reset();
            fileOffset += count;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            buffer.Dispose();
            readCallback = readDirectCallback = readAndCopyCallback = null;
        }

        fileOffset = 0L;
        bufferStart = bufferEnd = 0;

        base.Dispose(disposing);
    }
}