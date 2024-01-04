using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;

namespace DotNext.IO;

using Buffers;
using Patterns;
using AsyncEnumerable = Collections.Generic.AsyncEnumerable;
using DecodingContext = Text.DecodingContext;

internal sealed class EmptyBinaryReader : IAsyncBinaryReader, ISingleton<EmptyBinaryReader>
{
    public static EmptyBinaryReader Instance { get; } = new();

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
        => output.IsEmpty ? GetCompletedOrCanceledTask(token) : EndOfStream();

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

    ValueTask IAsyncBinaryReader.SkipAsync(long length, CancellationToken token) => length switch
    {
        < 0L => ValueTask.FromException(new ArgumentOutOfRangeException(nameof(length))),
        0L => ValueTask.CompletedTask,
        _ => EndOfStream(),
    };

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

    private static ValueTask CopyHelper(long? count, CancellationToken token) => count switch
    {
        null or 0L => GetCompletedOrCanceledTask(token),
        < 0L => ValueTask.FromException(new ArgumentOutOfRangeException(nameof(count))),
        _ => EndOfStream(),
    };

    ValueTask IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, long? count, CancellationToken token)
        => CopyHelper(count, token);

    ValueTask IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, long? count, CancellationToken token)
        => CopyHelper(count, token);

    ValueTask IAsyncBinaryReader.CopyToAsync(Stream output, long? count, CancellationToken token)
        => CopyHelper(count, token);

    ValueTask IAsyncBinaryReader.CopyToAsync(PipeWriter output, long? count, CancellationToken token)
        => CopyHelper(count, token);
}