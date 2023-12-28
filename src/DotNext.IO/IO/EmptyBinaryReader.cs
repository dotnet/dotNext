using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;

namespace DotNext.IO;

using Buffers;
using AsyncEnumerable = Collections.Generic.AsyncEnumerable;
using DecodingContext = Text.DecodingContext;

internal sealed class EmptyBinaryReader : IAsyncBinaryReader
{
    internal static readonly EmptyBinaryReader Instance = new();

    private EmptyBinaryReader()
    {
    }

    private static ValueTask<T> EndOfStream<T>()
        => ValueTask.FromException<T>(new EndOfStreamException());

    private static ValueTask EndOfStream()
        => ValueTask.FromException(new EndOfStreamException());

    private static ValueTask GetCompletedOrCanceledTask(CancellationToken token)
        => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;

    ValueTask<T> IAsyncBinaryReader.ReadAsync<T>(CancellationToken token)
        => EndOfStream<T>();

    ValueTask<T> IAsyncBinaryReader.ReadLittleEndianAsync<T>(CancellationToken token)
        => EndOfStream<T>();

    ValueTask<T> IAsyncBinaryReader.ReadBigEndianAsync<T>(CancellationToken token)
        => EndOfStream<T>();

    ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
        => output.IsEmpty ? ValueTask.CompletedTask : EndOfStream();

    ValueTask<MemoryOwner<byte>> IAsyncBinaryReader.ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
        => EndOfStream<MemoryOwner<byte>>();

    ValueTask<MemoryOwner<char>> IAsyncBinaryReader.DecodeAsync(DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator, CancellationToken token)
        => EndOfStream<MemoryOwner<char>>();

    IAsyncEnumerable<ReadOnlyMemory<char>> IAsyncBinaryReader.DecodeAsync(DecodingContext context, LengthFormat lengthFormat, Memory<char> buffer, CancellationToken token)
        => AsyncEnumerable.Throw<ReadOnlyMemory<char>>(new EndOfStreamException());

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        => EndOfStream<T>();

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(LengthFormat lengthFormat, IFormatProvider? provider, CancellationToken token)
        => EndOfStream<T>();

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(DecodingContext context, LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider, MemoryAllocator<char>? allocator, CancellationToken token)
        => EndOfStream<T>();

    ValueTask<TResult> IAsyncBinaryReader.ParseAsync<TArg, TResult>(TArg arg, ReadOnlySpanFunc<char, TArg, TResult> parser, DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator, CancellationToken token)
        => EndOfStream<TResult>();

    ValueTask<TReader> IAsyncBinaryReader.ReadAsync<TReader>(TReader reader, CancellationToken token)
        => reader.RemainingBytes > 0 ? EndOfStream<TReader>() : ValueTask.FromResult(reader);

    ValueTask IAsyncBinaryReader.SkipAsync(long length, CancellationToken token)
        => length is 0L ? ValueTask.CompletedTask : EndOfStream();

    bool IAsyncBinaryReader.TryGetSequence(out ReadOnlySequence<byte> bytes)
    {
        bytes = ReadOnlySequence<byte>.Empty;
        return true;
    }

    bool IAsyncBinaryReader.TryGetRemainingBytesCount(out long count)
    {
        count = 0L;
        return true;
    }

    ValueTask IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, CancellationToken token)
        => GetCompletedOrCanceledTask(token);

    ValueTask IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        => GetCompletedOrCanceledTask(token);

    ValueTask IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
        => GetCompletedOrCanceledTask(token);

    ValueTask IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
        => GetCompletedOrCanceledTask(token);
}