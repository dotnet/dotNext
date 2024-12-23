using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.IO;

using Buffers;

public sealed class PoolingBufferedStream : Stream, IResettable, IFlushable
{
    private int readPosition, writePosition, readLength;
    private MemoryOwner<byte> buffer;
    private Stream? stream;

    public PoolingBufferedStream(Stream stream, int bufferSize)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        MaxBufferSize = bufferSize;
        this.stream = stream;
    }

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
    public int MaxBufferSize { get; }

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
        get => stream?.ReadTimeout ?? throw new NotSupportedException();
        set
        {
            if (stream is null)
                throw new NotSupportedException();
            
            stream.ReadTimeout = value;
        }
    }

    /// <inheritdoc/>
    public override int WriteTimeout
    {
        get => stream?.ReadTimeout ?? throw new NotSupportedException();
        set
        {
            if (stream is null)
                throw new NotSupportedException();
            
            stream.ReadTimeout = value;
        }
    }

    /// <inheritdoc/>
    public override long Length
    {
        get
        {
            ThrowIfDisposed();

            return stream.Length + writePosition;
        }
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ThrowIfDisposed();

        Flush();
        stream.SetLength(value);
    }

    /// <summary>
    /// Sets length of this stream asynchronously.
    /// </summary>
    /// <param name="value">A new length of the stream.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than zero.</exception>
    public async ValueTask SetLengthAsync(long value, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ThrowIfDisposed();

        await FlushCoreAsync(token).ConfigureAwait(false);
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

    /// <summary>
    /// Writes the buffered data to the underlying stream.
    /// </summary>
    public void Write()
    {
        ThrowIfDisposed();

        WriteCore();
    }

    /// <summary>
    /// Writes the buffered data to the underlying stream.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    public ValueTask WriteAsync(CancellationToken token)
        => stream is null ? DisposedTask : WriteCoreAsync(token);

    private void WriteCore()
    {
        Debug.Assert(stream is not null);
        
        if (buffer.Span.Slice(0, writePosition) is { IsEmpty: false } writeBuf)
        {
            stream.Write(writeBuf);
            writePosition = 0;
        }
    }

    private ValueTask WriteCoreAsync(CancellationToken token)
        => buffer.Memory.Slice(0, writePosition) is { IsEmpty: false } writeBuf
            ? WriteAndResetAsync(writeBuf, token)
            : ValueTask.CompletedTask;

    private async ValueTask WriteAndResetAsync(ReadOnlyMemory<byte> data, CancellationToken token)
    {
        Debug.Assert(stream is not null);
        
        await stream.WriteAsync(data, token).ConfigureAwait(false);
        writePosition = 0;
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

    private void EnsureBufferAllocated()
    {
        if (buffer.IsEmpty)
            buffer = Allocator.AllocateAtLeast(MaxBufferSize);
    }

    private void ResetIfNeeded()
    {
        if (writePosition is 0 && readLength == readPosition)
            Reset();
    }

    private Memory<byte> FreeBuffer => buffer.Memory.Slice(writePosition);

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();

        if (!CanWrite)
            throw new NotSupportedException();
        
        if (!buffer.IsEmpty)
            WriteCore(data);
    }

    private void WriteCore(ReadOnlySpan<byte> data)
    {
        Debug.Assert(stream is not null);
        
        if (writePosition is 0)
            ClearReadBufferBeforeWrite();
        
        EnsureBufferAllocated();
        var freeBuf = FreeBuffer.Span;
        
        // drain buffered data if needed
        if (freeBuf.Length < buffer.Length)
            WriteCore();
        
        // if internal buffer has not enough space then just write through
        if (buffer.Length > freeBuf.Length)
        {
            stream.Write(data);
            ResetIfNeeded();
        }
        else
        {
            data.CopyTo(freeBuf);
            writePosition += buffer.Length;
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
        ValueTask task;
        if (stream is null)
        {
            task = DisposedTask;
        }
        else if (stream.CanWrite)
        {
            task = WriteCoreAsync(data, token);
        }
        else
        {
            task = ValueTask.FromException(new NotSupportedException());
        }

        return task;
    }

    private async ValueTask WriteCoreAsync(ReadOnlyMemory<byte> data, CancellationToken token)
    {
        Debug.Assert(stream is not null);
        
        if (writePosition is 0)
            ClearReadBufferBeforeWrite();
        
        EnsureBufferAllocated();
        var freeBuf = FreeBuffer;
        
        // drain buffered data if needed
        if (freeBuf.Length < buffer.Length)
        {
            await WriteCoreAsync(token).ConfigureAwait(false);
            freeBuf = FreeBuffer;
        }
            
        // if internal buffer has not enough space then just write through
        if (buffer.Length > freeBuf.Length)
        {
            await stream.WriteAsync(data, token).ConfigureAwait(false);
            ResetIfNeeded();
        }
        else
        {
            data.CopyTo(freeBuf);
            writePosition += buffer.Length;
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

    private ReadOnlyMemory<byte> ReadBuffer => buffer.Memory[readPosition..readLength];
    
    private int ReadFromBuffer(Span<byte> destination)
    {
        int count;
        if (ReadBuffer.Span is { IsEmpty: false } readBuf)
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

    /// <inheritdoc/>
    public override int Read(Span<byte> data)
    {
        ThrowIfDisposed();
        
        return buffer.IsEmpty ? 0 : ReadCore(data);
    }

    private int ReadCore(Span<byte> data)
    {
        Debug.Assert(stream is not null);
        
        var bytesRead = ReadFromBuffer(data);
        if (bytesRead < data.Length)
        {
            data = data.Slice(bytesRead);
            readPosition = readLength = 0;
            WriteCore();
        }

        if (data.Length > MaxBufferSize)
        {
            bytesRead += stream.Read(data);
        }
        else
        {
            EnsureBufferAllocated();
            readLength = stream.Read(buffer.Span);
            bytesRead += ReadFromBuffer(data);
        }

        ResetIfNeeded();
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
        ValueTask<int> task;

        if (stream is null)
        {
            task = GetDisposedTask<int>();
        }
        else if (buffer.IsEmpty)
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
        if (bytesRead < data.Length)
        {
            data = data.Slice(bytesRead);
            readPosition = readLength = 0;
        }

        await WriteCoreAsync(token).ConfigureAwait(false);
        if (data.Length > MaxBufferSize)
        {
            bytesRead += await stream.ReadAsync(data, token).ConfigureAwait(false);
        }
        else
        {
            EnsureBufferAllocated();
            readLength = await stream.ReadAsync(buffer.Memory, token).ConfigureAwait(false);
            bytesRead += ReadFromBuffer(data.Span);
        }

        ResetIfNeeded();
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
    public async ValueTask<bool> ReadAsync(CancellationToken token)
    {
        ThrowIfDisposed();

        await WriteCoreAsync(token).ConfigureAwait(false);

        EnsureBufferAllocated();
        var readBuf = buffer.Memory.Slice(readLength);
        var count = await stream.ReadAsync(readBuf, token).ConfigureAwait(false);
        readLength += count;
        ResetIfNeeded();
        
        return count > 0;
    }

    /// <inheritdoc cref="Stream.FlushAsync(CancellationToken)"/>
    public override Task FlushAsync(CancellationToken token)
        => FlushCoreAsync(token).AsTask();

    private ValueTask FlushCoreAsync(CancellationToken token)
    {
        ValueTask task;
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
            task = new(stream.FlushAsync(token));
        }

        return task;
    }

    private ValueTask DisposedTask => ValueTask.FromException(new ObjectDisposedException(GetType().Name));

    private ValueTask<T> GetDisposedTask<T>() => ValueTask.FromException<T>(new ObjectDisposedException(GetType().Name));

    private async ValueTask WriteAndFlushAsync(CancellationToken token)
    {
        Debug.Assert(writePosition > 0);
        Debug.Assert(buffer.Length > 0);

        ThrowIfDisposed();

        await stream.WriteAsync(buffer.Memory.Slice(0, writePosition), token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);

        Reset();
    }

    /// <inheritdoc cref="Stream.Flush()"/>
    public override void Flush()
    {
        ThrowIfDisposed();

        if (writePosition > 0)
        {
            Debug.Assert(buffer.Length > 0);

            stream.Write(buffer.Span.Slice(0, writePosition));
        }

        stream.Flush();
        Reset();
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();

        long result;
        if (buffer.Span.Slice(0, writePosition) is { IsEmpty: false } writeBuf)
        {
            stream.Write(writeBuf);
            result = stream.Seek(offset, origin);
            ResetIfNeeded();
        }
        else
        {
            result = SeekNoWriteBuffer(offset, origin);
        }

        return result;
    }

    /// <summary>
    /// Sets the position within the current stream.
    /// </summary>
    /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
    /// <param name="origin">The reference point used to obtain the new position.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The new position within the current stream.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    public ValueTask<long> SeekAsync(long offset, SeekOrigin origin, CancellationToken token)
    {
        ValueTask<long> task;

        if (stream is null)
        {
            task = GetDisposedTask<long>();
        }
        else if (buffer.Memory.Slice(0, writePosition) is { IsEmpty: false } writeBuf)
        {
            task = WriteAndSeekAsync(writeBuf, offset, origin, token);
        }
        else
        {
            try
            {
                task = new(SeekNoWriteBuffer(offset, origin));
            }
            catch (Exception e)
            {
                task = ValueTask.FromException<long>(e);
            }
        }

        return task;
    }

    private async ValueTask<long> WriteAndSeekAsync(ReadOnlyMemory<byte> writeBuf, long offset, SeekOrigin origin, CancellationToken token)
    {
        Debug.Assert(stream is not null);

        await stream.WriteAsync(writeBuf, token).ConfigureAwait(false);
        var result = stream.Seek(offset, origin);
        ResetIfNeeded();
        return result;
    }

    private long SeekNoWriteBuffer(long offset, SeekOrigin origin)
    {
        Debug.Assert(stream is not null);
        
        var readBytes = readLength - readPosition;
        if (origin is SeekOrigin.Current && readBytes > 0)
            offset -= readBytes;

        var oldPos = stream.Position + (writePosition - readLength);
        var newPos = stream.Seek(offset, origin);

        var readPos = newPos - oldPos;
        if (readPos >= 0L && readPos < readLength)
        {
            readPosition = (int)readPos;
            stream.Seek(readBytes, SeekOrigin.Current);
        }
        else
        {
            Reset();
        }

        return newPos;
    }

    /// <inheritdoc/>
    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);
        ThrowIfDisposed();

        var readBytes = readLength - readPosition;
        if (readBytes > 0)
        {
            destination.Write(ReadBuffer.Span);
            readLength = readPosition = 0;
        }
        else if (writePosition > 0)
        {
            stream.Write(buffer.Span.Slice(0, writePosition));
        }

        stream.CopyTo(destination, bufferSize);
    }

    /// <inheritdoc/>
    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
    {
        ValidateCopyToArguments(destination, bufferSize);
        ThrowIfDisposed();
        
        var readBytes = readLength - readPosition;
        if (readBytes > 0)
        {
            await destination.WriteAsync(ReadBuffer, token).ConfigureAwait(false);
            readLength = readPosition = 0;
        }
        else if (writePosition > 0)
        {
            await stream.WriteAsync(buffer.Memory.Slice(0, writePosition), token).ConfigureAwait(false);
        }

        await stream.CopyToAsync(destination, bufferSize, token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override ValueTask DisposeAsync()
    {
        ValueTask task;
        if (stream is null)
        {
            task = ValueTask.CompletedTask;
        }
        else
        {
            buffer.Dispose();
            task = stream.DisposeAsync();
            stream = null;
        }

        return task;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            stream?.Dispose();
            buffer.Dispose();
            stream = null;
        }
        
        base.Dispose(disposing);
    }

    ~PoolingBufferedStream() => Dispose(false);
}