using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;

namespace DotNext.IO;

using Buffers;
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

    private static Task GetCompletedOrCanceledTask(CancellationToken token)
        => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

    public ValueTask<T> ReadAsync<T>(CancellationToken token)
        where T : unmanaged
        => EndOfStream<T>();

    public ValueTask ReadAsync(Memory<byte> output, CancellationToken token)
        => output.IsEmpty ? new() : EndOfStream();

    public ValueTask<MemoryOwner<byte>> ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
        => EndOfStream<MemoryOwner<byte>>();

    ValueTask<MemoryOwner<char>> IAsyncBinaryReader.ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, MemoryAllocator<char>? allocator, CancellationToken token)
        => EndOfStream<MemoryOwner<char>>();

    ValueTask<MemoryOwner<char>> IAsyncBinaryReader.ReadStringAsync(int length, DecodingContext context, MemoryAllocator<char>? allocator, CancellationToken token)
        => length == 0 ? new(default(MemoryOwner<char>)) : EndOfStream<MemoryOwner<char>>();

    public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token)
        => length == 0 ? new(string.Empty) : EndOfStream<string>();

    public ValueTask<string> ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
        => EndOfStream<string>();

    public Task CopyToAsync(Stream output, CancellationToken token)
        => GetCompletedOrCanceledTask(token);

    public Task CopyToAsync(PipeWriter output, CancellationToken token)
        => GetCompletedOrCanceledTask(token);

    ValueTask<long> IAsyncBinaryReader.ReadInt64Async(bool littleEndian, CancellationToken token)
        => EndOfStream<long>();

    ValueTask<int> IAsyncBinaryReader.ReadInt32Async(bool littleEndian, CancellationToken token)
        => EndOfStream<int>();

    ValueTask<short> IAsyncBinaryReader.ReadInt16Async(bool littleEndian, CancellationToken token)
        => EndOfStream<short>();

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(Parser<T> parser, LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider, CancellationToken token)
        => EndOfStream<T>();

    ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(LengthFormat lengthFormat, bool littleEndian, CancellationToken token)
        => EndOfStream<BigInteger>();

    ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(int length, bool littleEndian, CancellationToken token)
        => EndOfStream<BigInteger>();

    ValueTask IAsyncBinaryReader.SkipAsync(int length, CancellationToken token)
        => length == 0 ? new() : EndOfStream();

    bool IAsyncBinaryReader.TryGetSequence(out ReadOnlySequence<byte> bytes)
    {
        bytes = ReadOnlySequence<byte>.Empty;
        return true;
    }

    Task IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, CancellationToken token)
        => GetCompletedOrCanceledTask(token);

    Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> consumer, TArg arg, CancellationToken token)
        => GetCompletedOrCanceledTask(token);

    Task IAsyncBinaryReader.CopyToAsync<TArg>(ReadOnlySpanAction<byte, TArg> consumer, TArg arg, CancellationToken token)
        => GetCompletedOrCanceledTask(token);

    Task IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        => GetCompletedOrCanceledTask(token);
}