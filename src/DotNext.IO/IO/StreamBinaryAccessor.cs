using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
    private readonly Stream stream;

    internal AsyncStreamBinaryAccessor(Stream stream, Memory<byte> buffer)
    {
        if (buffer.IsEmpty)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.buffer = buffer;
    }

    void IFlushable.Flush() => stream.Flush();

    Task IFlushable.FlushAsync(CancellationToken token) => stream.FlushAsync(token);

    #region Reader
    public ValueTask<T> ReadAsync<T>(CancellationToken token = default)
        where T : unmanaged
        => StreamExtensions.ReadAsync<T>(stream, buffer, token);

    public ValueTask ReadAsync(Memory<byte> output, CancellationToken token = default)
        => StreamExtensions.ReadBlockAsync(stream, output, token);

    private async ValueTask SkipSlowAsync(int length, CancellationToken token)
    {
        for (int bytesRead; length > 0; length -= bytesRead)
        {
            bytesRead = await stream.ReadAsync(length < buffer.Length ? buffer.Slice(0, length) : buffer, token).ConfigureAwait(false);
            if (bytesRead is 0)
                throw new EndOfStreamException();
        }
    }

    ValueTask IAsyncBinaryReader.SkipAsync(int length, CancellationToken token)
    {
        ValueTask result;

        switch (length)
        {
            case < 0:
                result = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(length)));
                break;
            case 0:
                result = ValueTask.CompletedTask;
                break;
            default:
                if (stream.CanSeek)
                {
                    result = ValueTask.CompletedTask;
                    try
                    {
                        if (stream.Seek(length, SeekOrigin.Current) > stream.Length)
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
        => StreamExtensions.ReadBlockAsync(stream, lengthFormat, buffer, allocator, token);

    public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default)
        => StreamExtensions.ReadStringAsync(stream, length, context, buffer, token);

    ValueTask<MemoryOwner<char>> IAsyncBinaryReader.ReadStringAsync(int length, DecodingContext context, MemoryAllocator<char>? allocator, CancellationToken token)
        => StreamExtensions.ReadStringAsync(stream, length, context, buffer, allocator, token);

    public ValueTask<string> ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token = default)
        => StreamExtensions.ReadStringAsync(stream, lengthFormat, context, buffer, token);

    ValueTask<MemoryOwner<char>> IAsyncBinaryReader.ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, MemoryAllocator<char>? allocator, CancellationToken token)
        => StreamExtensions.ReadStringAsync(stream, lengthFormat, context, buffer, allocator, token);

    unsafe ValueTask<short> IAsyncBinaryReader.ReadInt16Async(bool littleEndian, CancellationToken token)
        => littleEndian == BitConverter.IsLittleEndian ? StreamExtensions.ReadAsync<short>(stream, buffer, token) : StreamExtensions.ReadAsync<short, short, Supplier<short, short>>(stream, new(&ReverseEndianness), buffer, token);

    unsafe ValueTask<int> IAsyncBinaryReader.ReadInt32Async(bool littleEndian, CancellationToken token)
        => littleEndian == BitConverter.IsLittleEndian ? StreamExtensions.ReadAsync<int>(stream, buffer, token) : StreamExtensions.ReadAsync<int, int, Supplier<int, int>>(stream, new(&ReverseEndianness), buffer, token);

    unsafe ValueTask<long> IAsyncBinaryReader.ReadInt64Async(bool littleEndian, CancellationToken token)
        => littleEndian == BitConverter.IsLittleEndian ? StreamExtensions.ReadAsync<long>(stream, buffer, token) : StreamExtensions.ReadAsync<long, long, Supplier<long, long>>(stream, new(&ReverseEndianness), buffer, token);

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(Parser<T> parser, LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider, CancellationToken token)
        => StreamExtensions.ParseAsync(stream, parser, lengthFormat, context, buffer, provider, token);

    [RequiresPreviewFeatures]
    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(CancellationToken token)
        => StreamExtensions.ParseAsync<T>(stream, buffer, token);

    ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(int length, bool littleEndian, CancellationToken token)
        => StreamExtensions.ReadBigIntegerAsync(stream, length, littleEndian, token);

    ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(LengthFormat lengthFormat, bool littleEndian, CancellationToken token)
        => StreamExtensions.ReadBigIntegerAsync(stream, lengthFormat, littleEndian, token);

    Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
        => stream.CopyToAsync(output, token);

    Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
        => stream.CopyToAsync(output, token);

    Task IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, CancellationToken token)
        => stream.CopyToAsync(writer, token: token);

    Task IAsyncBinaryReader.CopyToAsync<TArg>(ReadOnlySpanAction<byte, TArg> reader, TArg arg, CancellationToken token)
        => stream.CopyToAsync(reader, arg, buffer, token);

    Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> reader, TArg arg, CancellationToken token)
        => stream.CopyToAsync(reader, arg, buffer, token);

    Task IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        => stream.CopyToAsync(consumer, buffer, token);

    bool IAsyncBinaryReader.TryGetSequence(out ReadOnlySequence<byte> bytes)
    {
        bytes = default;
        return false;
    }

    bool IAsyncBinaryReader.TryGetRemainingBytesCount(out long count)
    {
        bool result;
        count = (result = stream.CanSeek)
            ? Math.Max(0L, stream.Length - stream.Position)
            : default;

        return result;
    }

    #endregion

    #region Writer
    public ValueTask WriteAsync<T>(T value, CancellationToken token)
        where T : unmanaged
        => stream.WriteAsync(value, buffer, token);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token)
        => lengthFormat is null ? stream.WriteAsync(input, token) : stream.WriteBlockAsync(input, lengthFormat.GetValueOrDefault(), buffer, token);

    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
        => stream.WriteAsync(input, token);

    public ValueTask WriteStringAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token)
        => stream.WriteStringAsync(chars, context, buffer, lengthFormat, token);

    async ValueTask IAsyncBinaryWriter.WriteAsync<TArg>(Action<TArg, IBufferWriter<byte>> writer, TArg arg, CancellationToken token)
    {
        using var bufferWriter = new PreallocatedBufferWriter(buffer);
        writer(arg, bufferWriter);
        await stream.WriteAsync(bufferWriter.WrittenMemory, token).ConfigureAwait(false);
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
        => stream.WriteBigIntegerAsync(value, littleEndian, buffer, lengthFormat, token);

    ValueTask IAsyncBinaryWriter.WriteFormattableAsync<T>(T value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        => stream.WriteFormattableAsync(value, lengthFormat, context, buffer, format, provider, token);

    [RequiresPreviewFeatures]
    ValueTask IAsyncBinaryWriter.WriteFormattableAsync<T>(T value, CancellationToken token)
        => stream.WriteFormattableAsync(value, buffer, token);

    Task IAsyncBinaryWriter.CopyFromAsync(Stream input, CancellationToken token)
        => input.CopyToAsync(stream, token);

    Task IAsyncBinaryWriter.CopyFromAsync(PipeReader input, CancellationToken token)
        => input.CopyToAsync(stream, token);

    Task IAsyncBinaryWriter.WriteAsync(ReadOnlySequence<byte> input, CancellationToken token)
        => stream.WriteAsync(input, token).AsTask();

    Task IAsyncBinaryWriter.CopyFromAsync<TArg>(Func<TArg, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> supplier, TArg arg, CancellationToken token)
        => stream.WriteAsync(supplier, arg, token);

    IBufferWriter<byte>? IAsyncBinaryWriter.TryGetBufferWriter() => null;
    #endregion
}