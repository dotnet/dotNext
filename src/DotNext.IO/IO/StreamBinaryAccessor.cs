using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.InteropServices;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.IO;

using Buffers;
using Text;

/// <summary>
/// Represents binary reader for the stream.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct AsyncStreamBinaryAccessor : IAsyncBinaryReader, IAsyncBinaryWriter, IFlushable
{
    private readonly Memory<byte> buffer;
    internal readonly Stream Stream;

    internal AsyncStreamBinaryAccessor(Stream stream, Memory<byte> buffer)
    {
        if (buffer.IsEmpty)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.buffer = buffer;
    }

    void IFlushable.Flush() => Stream.Flush();

    Task IFlushable.FlushAsync(CancellationToken token) => Stream.FlushAsync(token);

    #region Reader
    public ValueTask<T> ReadAsync<T>(CancellationToken token = default)
        where T : unmanaged
        => StreamExtensions.ReadAsync<T>(Stream, buffer, token);

    public ValueTask ReadAsync(Memory<byte> output, CancellationToken token = default)
        => Stream.ReadExactlyAsync(output, token);

    private async ValueTask SkipSlowAsync(long length, CancellationToken token)
    {
        for (int bytesRead; length > 0L; length -= bytesRead)
        {
            bytesRead = await Stream.ReadAsync(length < buffer.Length ? buffer.Slice(0, (int)length) : buffer, token).ConfigureAwait(false);
            if (bytesRead is 0)
                throw new EndOfStreamException();
        }
    }

    ValueTask IAsyncBinaryReader.SkipAsync(long length, CancellationToken token)
    {
        ValueTask result;

        switch (length)
        {
            case < 0L:
                result = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(length)));
                break;
            case 0L:
                result = ValueTask.CompletedTask;
                break;
            default:
                if (Stream.CanSeek)
                {
                    result = ValueTask.CompletedTask;
                    try
                    {
                        if (Stream.Seek(length, SeekOrigin.Current) > Stream.Length)
                            throw new EndOfStreamException();
                    }
                    catch (Exception e)
                    {
                        result = ValueTask.FromException(e);
                    }
                }
                else
                {
                    result = SkipSlowAsync(length, token);
                }

                break;
        }

        return result;
    }

    ValueTask<MemoryOwner<byte>> IAsyncBinaryReader.ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
        => StreamExtensions.ReadBlockAsync(Stream, lengthFormat, buffer, allocator, token);

    public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default)
        => StreamExtensions.ReadStringAsync(Stream, length, context, buffer, token);

    ValueTask<MemoryOwner<char>> IAsyncBinaryReader.ReadStringAsync(int length, DecodingContext context, MemoryAllocator<char>? allocator, CancellationToken token)
        => StreamExtensions.ReadStringAsync(Stream, length, context, buffer, allocator, token);

    public ValueTask<string> ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token = default)
        => StreamExtensions.ReadStringAsync(Stream, lengthFormat, context, buffer, token);

    ValueTask<MemoryOwner<char>> IAsyncBinaryReader.ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, MemoryAllocator<char>? allocator, CancellationToken token)
        => StreamExtensions.ReadStringAsync(Stream, lengthFormat, context, buffer, allocator, token);

    unsafe ValueTask<short> IAsyncBinaryReader.ReadInt16Async(bool littleEndian, CancellationToken token)
        => littleEndian == BitConverter.IsLittleEndian ? StreamExtensions.ReadAsync<short>(Stream, buffer, token) : StreamExtensions.ReadAsync<short, short, Supplier<short, short>>(Stream, new(&ReverseEndianness), buffer, token);

    unsafe ValueTask<int> IAsyncBinaryReader.ReadInt32Async(bool littleEndian, CancellationToken token)
        => littleEndian == BitConverter.IsLittleEndian ? StreamExtensions.ReadAsync<int>(Stream, buffer, token) : StreamExtensions.ReadAsync<int, int, Supplier<int, int>>(Stream, new(&ReverseEndianness), buffer, token);

    unsafe ValueTask<long> IAsyncBinaryReader.ReadInt64Async(bool littleEndian, CancellationToken token)
        => littleEndian == BitConverter.IsLittleEndian ? StreamExtensions.ReadAsync<long>(Stream, buffer, token) : StreamExtensions.ReadAsync<long, long, Supplier<long, long>>(Stream, new(&ReverseEndianness), buffer, token);

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(Parser<T> parser, LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider, CancellationToken token)
        => StreamExtensions.ParseAsync(Stream, parser, lengthFormat, context, buffer, provider, token);

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(CancellationToken token)
        => StreamExtensions.ParseAsync<T>(Stream, buffer, token);

    ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(int length, bool littleEndian, CancellationToken token)
        => StreamExtensions.ReadBigIntegerAsync(Stream, length, littleEndian, token);

    ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(LengthFormat lengthFormat, bool littleEndian, CancellationToken token)
        => StreamExtensions.ReadBigIntegerAsync(Stream, lengthFormat, littleEndian, token);

    Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
        => Stream.CopyToAsync(output, token);

    Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
        => Stream.CopyToAsync(output, token);

    Task IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, CancellationToken token)
        => Stream.CopyToAsync(writer, token: token);

    Task IAsyncBinaryReader.CopyToAsync<TArg>(ReadOnlySpanAction<byte, TArg> reader, TArg arg, CancellationToken token)
        => Stream.CopyToAsync(reader, arg, buffer, token);

    Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> reader, TArg arg, CancellationToken token)
        => Stream.CopyToAsync(reader, arg, buffer, token);

    Task IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        => Stream.CopyToAsync(consumer, buffer, token);

    bool IAsyncBinaryReader.TryGetSequence(out ReadOnlySequence<byte> bytes)
    {
        bytes = default;
        return false;
    }

    bool IAsyncBinaryReader.TryGetRemainingBytesCount(out long count)
    {
        bool result;
        count = (result = Stream.CanSeek)
            ? Math.Max(0L, Stream.Length - Stream.Position)
            : default;

        return result;
    }

    #endregion

    #region Writer
    public ValueTask WriteAsync<T>(T value, CancellationToken token)
        where T : unmanaged
        => Stream.WriteAsync(value, buffer, token);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token)
        => lengthFormat is null ? Stream.WriteAsync(input, token) : Stream.WriteBlockAsync(input, lengthFormat.GetValueOrDefault(), buffer, token);

    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
        => Stream.WriteAsync(input, token);

    public ValueTask WriteStringAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token)
        => Stream.WriteStringAsync(chars, context, buffer, lengthFormat, token);

    async ValueTask IAsyncBinaryWriter.WriteAsync<TArg>(Action<TArg, IBufferWriter<byte>> writer, TArg arg, CancellationToken token)
    {
        using var bufferWriter = new PreallocatedBufferWriter(buffer);
        writer(arg, bufferWriter);
        await Stream.WriteAsync(bufferWriter.WrittenMemory, token).ConfigureAwait(false);
    }

    ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, bool littleEndian, CancellationToken token)
    {
        value.ReverseIfNeeded(littleEndian);
        return WriteAsync(value, token);
    }

    ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, bool littleEndian, CancellationToken token)
    {
        value.ReverseIfNeeded(littleEndian);
        return WriteAsync(value, token);
    }

    ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, bool littleEndian, CancellationToken token)
    {
        value.ReverseIfNeeded(littleEndian);
        return WriteAsync(value, token);
    }

    ValueTask IAsyncBinaryWriter.WriteBigIntegerAsync(BigInteger value, bool littleEndian, LengthFormat? lengthFormat, CancellationToken token)
        => Stream.WriteBigIntegerAsync(value, littleEndian, buffer, lengthFormat, token);

    ValueTask IAsyncBinaryWriter.WriteFormattableAsync<T>(T value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        => Stream.WriteFormattableAsync(value, lengthFormat, context, buffer, format, provider, token);

    ValueTask IAsyncBinaryWriter.WriteFormattableAsync<T>(T value, CancellationToken token)
        => Stream.WriteFormattableAsync(value, buffer, token);

    Task IAsyncBinaryWriter.CopyFromAsync(Stream input, CancellationToken token)
        => input.CopyToAsync(Stream, token);

    Task IAsyncBinaryWriter.CopyFromAsync(PipeReader input, CancellationToken token)
        => input.CopyToAsync(Stream, token);

    Task IAsyncBinaryWriter.WriteAsync(ReadOnlySequence<byte> input, CancellationToken token)
        => Stream.WriteAsync(input, token).AsTask();

    Task IAsyncBinaryWriter.CopyFromAsync<TArg>(Func<TArg, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> supplier, TArg arg, CancellationToken token)
        => Stream.WriteAsync(supplier, arg, token);

    IBufferWriter<byte>? IAsyncBinaryWriter.TryGetBufferWriter() => null;
    #endregion
}