using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.IO;

using Buffers;

partial class PoolingBufferedStream : IBufferedReader, IAsyncBinaryReader
{
    /// <inheritdoc/>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    ReadOnlyMemory<byte> IBufferedReader.Buffer
    {
        get
        {
            ThrowIfDisposed();
            EnsureWriteBufferIsEmpty();

            return ReadBuffer;
        }
    }
    
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ReadOnlyMemory<byte> ReadBuffer => buffer.Memory[readPosition..readLength];

    /// <inheritdoc/>
    void IBufferedReader.Consume(int count)
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
            return ValueTask.FromException<TReader>(new ObjectDisposedException(GetType().Name));

        if (HasBufferedDataToWrite)
            return ValueTask.FromException<TReader>(new InvalidOperationException(ExceptionMessages.WriteBufferNotEmpty));

        return HasBufferedDataToRead && Read(ref reader)
            ? ValueTask.FromResult(reader)
            : ReadSlowAsync(reader, token);
    }

    /// <inheritdoc/>
    async ValueTask IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, long? count, CancellationToken token)
    {
        AssertState();
        ThrowIfDisposed();

        if (!stream.CanRead)
            throw new NotSupportedException();

        for (ReadOnlyMemory<byte> buffer;
             HasBufferedDataToRead || await ReadCoreAsync(token).ConfigureAwait(false);
             AdvanceReader(buffer.Length))
        {
            buffer = ReadBuffer;
            await consumer.Invoke(buffer, token).ConfigureAwait(false);
        }
    }
    
    private bool Read<TParser>(ref TParser parser)
        where TParser : struct, IBufferReader
    {
        Debug.Assert(HasBufferedDataToRead);

        do
        {
            if (ReadBuffer.TrimLength(parser.RemainingBytes) is not { IsEmpty: false } buffer)
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
}