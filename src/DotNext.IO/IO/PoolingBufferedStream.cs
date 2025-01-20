using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.IO;

using Buffers;

/// <summary>
/// Represents alternative implementation of <see cref="BufferedStream"/> that supports
/// memory pooling.
/// </summary>
/// <remarks>
/// The stream implements lazy buffer pattern. It means that the stream releases the buffer when there is no buffered data.
/// </remarks>
/// <param name="stream">The underlying stream to be buffered.</param>
/// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open after the object is disposed; otherwise, <see langword="false"/>.</param>
public sealed class PoolingBufferedStream(Stream stream, bool leaveOpen = false) : Stream, IBufferedWriter, IFlushable, IBufferedReader
{
    private const int MinBufferSize = 16;
    private const int DefaultBufferSize = 4096;

    private readonly int maxBufferSize = DefaultBufferSize;
    private int readPosition, writePosition, readLength;
    private MemoryOwner<byte> buffer;
    private Stream? stream = stream ?? throw new ArgumentNullException(nameof(stream));

    /// <summary>
    /// Gets or sets buffer allocator.
    /// </summary>
    public MemoryAllocator<byte>? Allocator
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the base stream.
    /// </summary>
    public Stream BaseStream
    {
        get
        {
            ThrowIfDisposed();
            return stream;
        }
    }

    /// <summary>
    /// Gets the maximum size of the internal buffer, in bytes.
    /// </summary>
    public int MaxBufferSize
    {
        get => maxBufferSize;
        init => maxBufferSize = value >= MinBufferSize ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <inheritdoc/>
    public override bool CanRead => stream?.CanRead ?? false;

    /// <inheritdoc/>
    public override bool CanWrite => stream?.CanWrite ?? false;
    
    /// <inheritdoc/>
    public override bool CanSeek => stream?.CanSeek ?? false;

    /// <inheritdoc/>
    public override bool CanTimeout => stream?.CanTimeout ?? false;

    /// <inheritdoc/>
    public override int ReadTimeout
    {
        get => stream?.ReadTimeout ?? throw new InvalidOperationException();
        set
        {
            if (stream is null)
                throw new InvalidOperationException();
            
            stream.ReadTimeout = value;
        }
    }

    /// <inheritdoc/>
    public override int WriteTimeout
    {
        get => stream?.WriteTimeout ?? throw new InvalidOperationException();
        set
        {
            if (stream is null)
                throw new InvalidOperationException();
            
            stream.WriteTimeout = value;
        }
    }

    /// <inheritdoc/>
    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            
            WriteCore();
            return stream.Length;
        }
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ThrowIfDisposed();
        
        WriteCore();
        stream.SetLength(value);
    }

    /// <inheritdoc/>
    public override long Position
    {
        get
        {
            ThrowIfDisposed();

            return stream.Position + (readPosition - readLength + writePosition);
        }

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            Seek(value, SeekOrigin.Begin);
        }
    }

    [MemberNotNull(nameof(stream))]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(stream is null, this);

    /// <summary>
    /// Resets the internal buffer.
    /// </summary>
    public void Reset()
    {
        readPosition = writePosition = readLength = 0;
        buffer.Dispose();
    }

    private void EnsureReadBufferIsEmpty()
    {
        if (readPosition != readLength)
            throw new InvalidOperationException(ExceptionMessages.ReadBufferNotEmpty);
    }

    /// <inheritdoc/>
    Memory<byte> IBufferedWriter.Buffer
    {
        get
        {
            ThrowIfDisposed();
            EnsureReadBufferIsEmpty();

            return EnsureBufferAllocated().Memory.Slice(writePosition);
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

    /// <summary>
    /// Gets a value indicating that the stream has buffered data in write buffer.
    /// </summary>
    public bool HasBufferedDataToWrite => writePosition > 0;

    private ReadOnlyMemory<byte> WrittenMemory => buffer.Memory.Slice(0, writePosition);

    /// <summary>
    /// Writes the buffered data to the underlying stream.
    /// </summary>
    public void Write()
    {
        AssertState();
        ThrowIfDisposed();

        if (!stream.CanWrite)
            throw new NotSupportedException();

        WriteCore();
    }

    /// <summary>
    /// Writes the buffered data to the underlying stream.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    public ValueTask WriteAsync(CancellationToken token = default)
    {
        ValueTask task;
        if (stream is null)
        {
            task = new(DisposedTask);
        }
        else if (stream.CanWrite)
        {
            task = WriteCoreAsync(token);
        }
        else
        {
            task = ValueTask.FromException(new NotSupportedException());
        }

        return task;
    }

    private void WriteCore()
    {
        Debug.Assert(stream is not null);
        
        if (WrittenMemory.Span is { IsEmpty: false } writeBuf)
        {
            stream.Write(writeBuf);
            writePosition = 0;
        }
    }

    private ValueTask WriteCoreAsync(CancellationToken token)
        => WrittenMemory is { IsEmpty: false } writeBuf
            ? WriteAndResetAsync(writeBuf, token)
            : ValueTask.CompletedTask;

    private ValueTask WriteAndResetAsync(ReadOnlyMemory<byte> data, CancellationToken token)
    {
        Debug.Assert(stream is not null);
        
        writePosition = 0;
        return stream.WriteAsync(data, token);
    }

    private void ClearReadBufferBeforeWrite()
    {
        var relativePos = readPosition - readLength;
        if (relativePos is not 0)
        {
            Debug.Assert(stream is not null);
            stream.Seek(relativePos, SeekOrigin.Current);
        }

        readLength = readPosition = 0;
    }

    private ref readonly MemoryOwner<byte> EnsureBufferAllocated()
    {
        ref var result = ref buffer;
        if (result.IsEmpty)
            result = Allocator.AllocateExactly(maxBufferSize);
        
        Debug.Assert(!result.IsEmpty);
        return ref result;
    }

    private void ResetIfNeeded()
    {
        if (writePosition is 0 && readLength == readPosition)
            Reset();
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> data)
    {
        AssertState();
        ThrowIfDisposed();

        if (!stream.CanWrite)
            throw new NotSupportedException();
        
        if (!data.IsEmpty)
            WriteCore(data);
    }

    private void WriteCore(ReadOnlySpan<byte> data)
    {
        Debug.Assert(stream is not null);
        
        if (writePosition is 0)
            ClearReadBufferBeforeWrite();
        
        var freeBuf = EnsureBufferAllocated().Span.Slice(writePosition);
        
        // drain buffered data if needed
        if (freeBuf.Length < data.Length)
            WriteCore();
        
        // if internal buffer has not enough space then just write through
        if (data.Length > freeBuf.Length)
        {
            stream.Write(data);
            ResetIfNeeded();
        }
        else
        {
            data.CopyTo(freeBuf);
            writePosition += data.Length;
        }
    }

    /// <inheritdoc/>
    public override void Write(byte[] data, int offset, int count)
    {
        ValidateBufferArguments(data, offset, count);

        Write(new ReadOnlySpan<byte>(data, offset, count));
    }

    /// <inheritdoc/>
    public override void WriteByte(byte value)
        => Write(new ReadOnlySpan<byte>(in value));

    /// <inheritdoc cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken token = default)
    {
        AssertState();
        
        ValueTask task;
        if (stream is null)
        {
            task = new(DisposedTask);
        }
        else if (!stream.CanWrite)
        {
            task = ValueTask.FromException(new NotSupportedException());
        }
        else if (data.IsEmpty)
        {
            task = new();
        }
        else
        {
            task = WriteCoreAsync(data, token);
        }

        return task;
    }

    private async ValueTask WriteCoreAsync(ReadOnlyMemory<byte> data, CancellationToken token)
    {
        Debug.Assert(stream is not null);
        
        if (writePosition is 0)
            ClearReadBufferBeforeWrite();

        var freeBuf = EnsureBufferAllocated().Memory.Slice(writePosition);
        
        // drain buffered data if needed
        if (freeBuf.Length < data.Length)
        {
            await WriteCoreAsync(token).ConfigureAwait(false);
            freeBuf = buffer.Memory.Slice(writePosition);
        }
            
        // if internal buffer has not enough space then just write through
        if (data.Length > freeBuf.Length)
        {
            await stream.WriteAsync(data, token).ConfigureAwait(false);
            ResetIfNeeded();
        }
        else
        {
            data.CopyTo(freeBuf);
            writePosition += data.Length;
        }
    }
    
    /// <inheritdoc/>
    public override Task WriteAsync(byte[] data, int offset, int count, CancellationToken token)
        => WriteAsync(new ReadOnlyMemory<byte>(data, offset, count), token).AsTask();

    /// <inheritdoc/>
    public override IAsyncResult BeginWrite(byte[] data, int offset, int count, AsyncCallback? callback, object? state)
        => TaskToAsyncResult.Begin(WriteAsync(data, offset, count), callback, state);

    /// <inheritdoc/>
    public override void EndWrite(IAsyncResult asyncResult) => TaskToAsyncResult.End(asyncResult);

    private ReadOnlyMemory<byte> MemoryToRead => buffer.Memory[readPosition..readLength];
    
    private int ReadFromBuffer(Span<byte> destination)
    {
        int count;
        if (MemoryToRead.Span is { IsEmpty: false } readBuf)
        {
            readBuf.CopyTo(destination, out count);
            readPosition += count;
            ResetIfNeeded();
        }
        else
        {
            count = 0;
        }

        return count;
    }

    /// <summary>
    /// Gets a value indicating that the stream has data in read buffer.
    /// </summary>
    public bool HasBufferedDataToRead => readPosition != readLength;

    /// <inheritdoc/>
    public override int Read(Span<byte> data)
    {
        AssertState();
        ThrowIfDisposed();

        if (!stream.CanRead)
            throw new InvalidOperationException();
        
        return data.IsEmpty ? 0 : ReadCore(data);
    }

    private int ReadCore(Span<byte> data)
    {
        Debug.Assert(stream is not null);
        
        var bytesRead = ReadFromBuffer(data);
        data = data.Slice(bytesRead);
        WriteCore();

        if (data.IsEmpty)
        {
            // nothing to do
        }
        else if (data.Length > MaxBufferSize)
        {
            bytesRead += stream.Read(data);
        }
        else
        {
            readLength = stream.Read(EnsureBufferAllocated().Span);
            bytesRead += ReadFromBuffer(data);
        }

        return bytesRead;
    }

    /// <inheritdoc/>
    public override int Read(byte[] data, int offset, int count)
    {
        ValidateBufferArguments(data, offset, count);

        return Read(data.AsSpan(offset, count));
    }

    /// <inheritdoc cref="Stream.ReadAsync(Memory{byte}, CancellationToken)"/>
    public override ValueTask<int> ReadAsync(Memory<byte> data, CancellationToken token = default)
    {
        AssertState();
        ValueTask<int> task;

        if (stream is null)
        {
            task = GetDisposedTask<int>();
        }
        else if (!stream.CanRead)
        {
            task = ValueTask.FromException<int>(new NotSupportedException());
        }
        else if (data.IsEmpty)
        {
            task = new(result: 0);
        }
        else
        {
            task = ReadCoreAsync(data, token);
        }

        return task;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken token)
        => ReadAsync(data.AsMemory(offset, count), token).AsTask();

    private async ValueTask<int> ReadCoreAsync(Memory<byte> data, CancellationToken token)
    {
        Debug.Assert(stream is not null);
        
        var bytesRead = ReadFromBuffer(data.Span);
        data = data.Slice(bytesRead);

        await WriteCoreAsync(token).ConfigureAwait(false);
        if (data.IsEmpty)
        {
            // nothing to do
        }
        else if (data.Length > MaxBufferSize)
        {
            bytesRead += await stream.ReadAsync(data, token).ConfigureAwait(false);
        }
        else
        {
            readLength = await stream.ReadAsync(EnsureBufferAllocated().Memory, token).ConfigureAwait(false);
            bytesRead += ReadFromBuffer(data.Span);
        }

        return bytesRead;
    }

    /// <summary>
    /// Fetches the internal buffer from the underlying stream.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the data has been copied from the file to the internal buffer;
    /// <see langword="false"/> if no more data to read.
    /// </returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    /// <exception cref="InternalBufferOverflowException">The internal buffer is full.</exception>
    public async ValueTask<bool> ReadAsync(CancellationToken token = default)
    {
        AssertState();
        ThrowIfDisposed();

        if (!stream.CanRead)
            throw new NotSupportedException();
        
        await WriteCoreAsync(token).ConfigureAwait(false);

        var count = PrepareReadBuffer(out var readBuf)
            ? await stream.ReadAsync(readBuf, token).ConfigureAwait(false)
            : throw new InternalBufferOverflowException();
        readLength += count;
        ResetIfNeeded();

        return count > 0;
    }

    /// <summary>
    /// Populates the internal buffer from the underlying stream.
    /// </summary>
    /// <returns><see langword="true"/> if </returns>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    /// <exception cref="InternalBufferOverflowException">The internal buffer is full.</exception>
    public bool Read()
    {
        AssertState();
        ThrowIfDisposed();

        if (!stream.CanRead)
            throw new NotSupportedException();
        
        WriteCore();

        var count = PrepareReadBuffer(out var readBuf)
            ? stream.Read(readBuf.Span)
            : throw new InternalBufferOverflowException();
        readLength += count;
        ResetIfNeeded();
        
        return count > 0;
    }
    
    private bool PrepareReadBuffer(out Memory<byte> readBuffer)
    {
        Debug.Assert(writePosition is 0);
        
        switch (readPosition)
        {
            case 0 when readLength == maxBufferSize:
                readBuffer = default;
                return false;
            case > 0:
                readBuffer = buffer.Memory;
                
                // compact buffer
                readBuffer[readPosition..readLength].CopyTo(readBuffer);
                readLength -= readPosition;
                readPosition = 0;
                break;
            default:
                readBuffer = EnsureBufferAllocated().Memory;
                break;
        }

        return true;
    }

    /// <inheritdoc/>
    public override IAsyncResult BeginRead(byte[] data, int offset, int count, AsyncCallback? callback, object? state)
        => TaskToAsyncResult.Begin(ReadAsync(data, offset, count), callback, state);

    /// <inheritdoc/>
    public override int EndRead(IAsyncResult asyncResult) => TaskToAsyncResult.End<int>(asyncResult);

    /// <inheritdoc cref="Stream.FlushAsync(CancellationToken)"/>
    public override Task FlushAsync(CancellationToken token)
    {
        Task task;
        if (writePosition > 0)
        {
            task = WriteAndFlushAsync(token);
        }
        else if (stream is null)
        {
            task = DisposedTask;
        }
        else
        {
            Reset();
            task = stream.FlushAsync(token);
        }

        return task;
    }

    private Task DisposedTask => Task.FromException(new ObjectDisposedException(GetType().Name));

    private ValueTask<T> GetDisposedTask<T>() => ValueTask.FromException<T>(new ObjectDisposedException(GetType().Name));

    private async Task WriteAndFlushAsync(CancellationToken token)
    {
        Debug.Assert(writePosition > 0);
        Debug.Assert(buffer.Length > 0);

        ThrowIfDisposed();

        await stream.WriteAsync(WrittenMemory, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);

        Reset();
    }

    /// <inheritdoc cref="Stream.Flush()"/>
    public override void Flush()
    {
        AssertState();
        ThrowIfDisposed();

        WriteCore();
        stream.Flush();
        Reset();
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        AssertState();
        ThrowIfDisposed();

        long result;
        if (WrittenMemory.Span is { IsEmpty: false } writeBuf)
        {
            stream.Write(writeBuf);
            writePosition = 0;
            ResetIfNeeded();
            result = stream.Seek(offset, origin);
        }
        else
        {
            result = SeekNoWriteBuffer(offset, origin);
        }

        return result;
    }

    private long SeekNoWriteBuffer(long offset, SeekOrigin origin)
    {
        Debug.Assert(stream is not null);
        
        var readBytes = readLength - readPosition;
        if (origin is SeekOrigin.Current && readBytes > 0)
            offset -= readBytes;
        
        var oldPos = stream.Position - readBytes;
        var newPos = stream.Seek(offset, origin);

        var readPos = newPos - oldPos + readPosition;
        if (readPos >= 0L && readPos < readLength)
        {
            readPosition = (int)readPos;
            stream.Seek(readLength - readPosition, SeekOrigin.Current);
        }
        else
        {
            Reset();
        }

        return newPos;
    }

    /// <inheritdoc/>
    public override int ReadByte()
    {
        Unsafe.SkipInit(out byte value);
        return Read(new Span<byte>(ref value)) > 0 ? value : -1;
    }

    private void EnsureWriteBufferIsEmpty()
    {
        if (writePosition is not 0)
            throw new InvalidOperationException(ExceptionMessages.WriteBufferNotEmpty);
    }

    /// <inheritdoc/>
    ReadOnlyMemory<byte> IBufferedReader.Buffer
    {
        get
        {
            AssertState();
            ThrowIfDisposed();
            EnsureWriteBufferIsEmpty();

            return buffer.Memory[readPosition..readLength];
        }
    }

    /// <inheritdoc/>
    void IBufferedReader.Consume(int count)
    {
        AssertState();
        ThrowIfDisposed();
        EnsureWriteBufferIsEmpty();
        
        var newPosition = count + readPosition;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)newPosition, (uint)readLength, nameof(count));
        
        if (newPosition == readLength)
        {
            Reset();
        }
        else
        {
            readPosition = newPosition;
        }
    }

    [Conditional("DEBUG")]
    private void AssertState()
    {
        // if reader or writer state differs from the default one, the buffer must be allocated
        Debug.Assert((readPosition == readLength && writePosition is 0) || buffer.Length > 0);
    }

    /// <inheritdoc/>
    public override void CopyTo(Stream destination, int bufferSize)
    {
        AssertState();
        ValidateCopyToArguments(destination, bufferSize);
        ThrowIfDisposed();

        if (MemoryToRead.Span is { IsEmpty: false } readBuf)
        {
            destination.Write(readBuf);
            readLength = readPosition = 0;
        }
        else if (WrittenMemory.Span is { IsEmpty: false } writeBuf)
        {
            stream.Write(writeBuf);
            writePosition = 0;
        }

        stream.CopyTo(destination, bufferSize);
        ResetIfNeeded();
    }

    /// <inheritdoc/>
    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
    {
        AssertState();
        ValidateCopyToArguments(destination, bufferSize);
        ThrowIfDisposed();

        if (MemoryToRead is { IsEmpty: false } readBuf)
        {
            await destination.WriteAsync(readBuf, token).ConfigureAwait(false);
            readLength = readPosition = 0;
        }
        else if (WrittenMemory is { IsEmpty: false } writeBuf)
        {
            await stream.WriteAsync(writeBuf, token).ConfigureAwait(false);
            writePosition = 0;
        }

        await stream.CopyToAsync(destination, bufferSize, token).ConfigureAwait(false);
        ResetIfNeeded();
    }

    /// <inheritdoc/>
    public override ValueTask DisposeAsync()
    {
        ValueTask task;
        if (stream is null)
        {
            task = new();
        }
        else
        {
            Reset();
            task = leaveOpen ? new() : stream.DisposeAsync();
            stream = null;
        }

        return task;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!leaveOpen && stream is not null)
                stream.Dispose();
            
            stream = null;
        }
        
        Reset();
        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    ~PoolingBufferedStream() => Dispose(false);
}