using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.IO;

using Buffers;

partial class PoolingBufferedStream : IAsyncBinaryReader
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ReadOnlyMemory<byte> ReadBuffer => buffer.Memory[readPosition..readLength];

    /// <summary>
    /// Tries to get the read buffer
    /// </summary>
    /// <remarks>
    /// Use <see cref="Read(int)"/> to mark the number of bytes read.
    /// </remarks>
    /// <param name="minimumSize">The expected number of available bytes to read in the underlying buffer.</param>
    /// <param name="buffer">The readable buffer.</param>
    /// <returns>
    /// <see langword="true"/> if the underlying buffer is at least of size <paramref name="minimumSize"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetReadBuffer(int minimumSize, out ReadOnlyMemory<byte> buffer)
    {
        var bytesToRead = readLength - readPosition;
        if ((uint)minimumSize > (uint)bytesToRead || HasBufferedDataToWrite)
        {
            buffer = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        buffer = ReadBuffer;
        return true;
    }

    /// <summary>
    /// Marks the specified number of bytes in the internal buffer as read.
    /// </summary>
    /// <param name="count">The number of bytes read.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is larger than the available bytes to read.</exception>
    /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
    /// <exception cref="InvalidOperationException">The underlying write buffer is not empty.</exception>
    /// <seealso cref="TryGetReadBuffer"/>
    public void Read(int count)
    {
        AssertState();
        ThrowIfDisposed();
        EnsureWriteBufferIsEmpty();
        
        var newPosition = count + readPosition;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)newPosition, (uint)readLength, nameof(count));
        ConsumeUnsafe(newPosition);
    }

    private void ConsumeUnsafe(int newPosition)
    {
        Debug.Assert((uint)newPosition <= (uint)readLength);
        
        if (newPosition == readLength)
        {
            Reset();
        }
        else
        {
            readPosition = newPosition;
        }
    }

    /// <inheritdoc/>
    ValueTask<TReader> IAsyncBinaryReader.ReadAsync<TReader>(TReader reader, CancellationToken token)
    {
        AssertState();

        if (stream is null)
            return GetDisposedTask<TReader>();

        if (HasBufferedDataToWrite)
            return ValueTask.FromException<TReader>(new InvalidOperationException(ExceptionMessages.WriteBufferNotEmpty));

        return HasBufferedDataToRead && Read(ref reader)
            ? ValueTask.FromResult(reader)
            : ReadSlowAsync(reader, token);
    }
    
    private bool Read<TParser>(ref TParser parser)
        where TParser : struct, IBufferReader
    {
        Debug.Assert(HasBufferedDataToRead);

        do
        {
            if (ReadBuffer % parser.RemainingBytes is not { IsEmpty: false } buffer)
                return false;

            parser.Invoke(buffer.Span);
            AdvanceReader(buffer.Length);
        }
        while (parser.RemainingBytes > 0);

        return true;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<TParser> ReadSlowAsync<TParser>(TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader
    {
        while (await ReadCoreAsync(token).ConfigureAwait(false) && !Read(ref parser)) ;

        parser.EndOfStream();
        return parser;
    }
    
    private void AdvanceReader(int count) => ConsumeUnsafe(count + readPosition);

    /// <inheritdoc/>
    ValueTask IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, long? count, CancellationToken token)
    {
        AssertState();
        ThrowIfDisposed();

        if (!stream.CanRead)
            return ValueTask.FromException(new NotSupportedException());

        return count.HasValue
            ? CopyToAsync(consumer, count.GetValueOrDefault(), token)
            : CopyToAsync(consumer, token);
    }

    private async ValueTask CopyToAsync<TConsumer>(TConsumer consumer, long count, CancellationToken token)
        where TConsumer : ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        for (ReadOnlyMemory<byte> buffer;
             count > 0L && (HasBufferedDataToRead || await ReadCoreAsync(token).ConfigureAwait(false));
             AdvanceReader(buffer.Length))
        {
            buffer = ReadBuffer % int.CreateSaturating(count);
            await consumer.Invoke(buffer, token).ConfigureAwait(false);
            count -= buffer.Length;
        }

        if (count > 0L)
            throw new EndOfStreamException();
    } 

    private async ValueTask CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        where TConsumer : ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        for (ReadOnlyMemory<byte> buffer;
             HasBufferedDataToRead || await ReadCoreAsync(token).ConfigureAwait(false);
             AdvanceReader(buffer.Length))
        {
            buffer = ReadBuffer;
            await consumer.Invoke(buffer, token).ConfigureAwait(false);
        }
    }

    ValueTask IAsyncBinaryReader.SkipAsync(long length, CancellationToken token)
    {
        ValueTask task;

        switch (length)
        {
            case < 0L:
                task = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(length)));
                break;
            case 0L:
                task = ValueTask.CompletedTask;
                break;
            default:
                if (stream is null)
                {
                    task = new(DisposedTask);
                }
                else if (stream.CanSeek)
                {
                    task = ValueTask.CompletedTask;
                    try
                    {
                        if (SeekCore(length, SeekOrigin.Current) > stream.Length)
                            throw new EndOfStreamException();
                    }
                    catch (Exception e)
                    {
                        task = ValueTask.FromException(e);
                    }
                }
                else
                {
                    task = SkipSlowAsync(length, token);
                }

                break;
        }

        return task;
    }
    
    private ValueTask SkipSlowAsync(long length, CancellationToken token)
    {
        var reader = new SkippingReader(length);
        return HasBufferedDataToRead && Read(ref reader)
            ? ValueTask.CompletedTask
            : SkipSlowAsync(reader, token);
    }

    private async ValueTask SkipSlowAsync(SkippingReader reader, CancellationToken token)
    {
        while (await ReadCoreAsync(token).ConfigureAwait(false) && !Read(ref reader)) ;

        reader.EndOfStream();
    }
}