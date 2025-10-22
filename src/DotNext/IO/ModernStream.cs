using System.Runtime.CompilerServices;

namespace DotNext.IO;

/// <summary>
/// Represents a modern base class for custom streams that requires only necessary
/// abstract methods to work correctly.
/// </summary>
public abstract class ModernStream : Stream, IFlushable
{
    /// <inheritdoc/>
    public abstract override int Read(Span<byte> buffer);

    /// <inheritdoc/>
    public abstract override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default);

    /// <inheritdoc/>
    public sealed override int Read(byte[] buffer, int offset, int count)
        => Read(new(buffer, offset, count));
    
    /// <inheritdoc/>
    public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();

    /// <inheritdoc/>
    public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        => CanRead
            ? TaskToAsyncResult.Begin(ReadAsync(buffer, offset, count, CancellationToken.None), callback, state)
            : throw new NotSupportedException();

    /// <inheritdoc/>
    public sealed override int EndRead(IAsyncResult asyncResult)
        => TaskToAsyncResult.End<int>(asyncResult);

    /// <inheritdoc/>
    public sealed override int ReadByte()
    {
        Unsafe.SkipInit(out byte b);
        return Read(new(ref b)) is 1 ? b : -1;
    }

    /// <inheritdoc/>
    public abstract override void Write(ReadOnlySpan<byte> buffer);
    
    /// <inheritdoc/>
    public abstract override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default);

    /// <inheritdoc/>
    public sealed override void Write(byte[] buffer, int offset, int count)
        => Write(new(buffer, offset, count));

    /// <inheritdoc/>
    public sealed override void WriteByte(byte value)
        => Write(new ReadOnlySpan<byte>(in value));

    /// <inheritdoc/>
    public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), token).AsTask();

    /// <inheritdoc/>
    public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        => CanWrite
            ? TaskToAsyncResult.Begin(WriteAsync(buffer, offset, count, CancellationToken.None), callback, state)
            : throw new NotSupportedException();

    /// <inheritdoc/>
    public sealed override void EndWrite(IAsyncResult asyncResult)
        => TaskToAsyncResult.End(asyncResult);
}