using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Runtime.InteropServices;

namespace DotNext.IO;

using Buffers;
using IO.Pipelines;
using Text;

/// <summary>
/// Represents binary reader for the stream.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct AsyncStreamBinaryAccessor(Stream stream, Memory<byte> buffer) : IAsyncBinaryReader, IAsyncBinaryWriter, IFlushable
{
    private readonly Memory<byte> buffer = buffer.IsEmpty ? throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer)) : buffer;

    internal Stream Stream => stream;

    void IFlushable.Flush() => Stream.Flush();

    Task IFlushable.FlushAsync(CancellationToken token) => Stream.FlushAsync(token);

    #region Reader
    ValueTask<T> IAsyncBinaryReader.ReadAsync<T>(CancellationToken token)
        => stream.ReadAsync<T>(buffer, token);

    ValueTask<TReader> IAsyncBinaryReader.ReadAsync<TReader>(TReader reader, CancellationToken token)
        => StreamExtensions.ReadAsync<TReader, BufferReader<TReader>>(stream, reader, buffer, token);

    ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
        => stream.ReadExactlyAsync(output, token);

    private async ValueTask SkipSlowAsync(long length, CancellationToken token)
    {
        for (Memory<byte> fragment; length > 0L; length -= fragment.Length)
        {
            fragment = length < buffer.Length ? buffer.Slice(0, (int)length) : buffer;
            await stream.ReadExactlyAsync(fragment, token).ConfigureAwait(false);
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
                if (stream.CanSeek)
                {
                    result = ValueTask.CompletedTask;
                    try
                    {
                        if (stream.Seek(length, SeekOrigin.Current) > Stream.Length)
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
        => stream.ReadBlockAsync(lengthFormat, allocator, token);

    ValueTask<MemoryOwner<char>> IAsyncBinaryReader.DecodeAsync(DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator, CancellationToken token)
        => stream.DecodeAsync(context, lengthFormat, buffer, allocator, token);

    IAsyncEnumerable<ReadOnlyMemory<char>> IAsyncBinaryReader.DecodeAsync(DecodingContext context, LengthFormat lengthFormat, Memory<char> buffer, CancellationToken token)
        => stream.DecodeAsync(context, lengthFormat, buffer, this.buffer, token);

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        => stream.ParseAsync<T>(lengthFormat, buffer, style, provider, token);

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(LengthFormat lengthFormat, IFormatProvider? provider, CancellationToken token)
        => stream.ParseAsync<T>(lengthFormat, buffer, provider, token);

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(DecodingContext context, LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider, MemoryAllocator<char>? allocator, CancellationToken token)
        => stream.ParseAsync((style, provider), IAsyncBinaryReader.Parse<T>, context, lengthFormat, buffer, allocator, token);

    ValueTask<TResult> IAsyncBinaryReader.ParseAsync<TArg, TResult>(TArg arg, ReadOnlySpanFunc<char, TArg, TResult> parser, DecodingContext context, DotNext.IO.LengthFormat lengthFormat, MemoryAllocator<char>? allocator, CancellationToken token)
        => stream.ParseAsync(arg, parser, context, lengthFormat, buffer, allocator, token);

    ValueTask IAsyncBinaryReader.CopyToAsync(Stream output, long? count, CancellationToken token)
        => count.HasValue ? stream.CopyToAsync(output, count.GetValueOrDefault(), buffer, token) : stream.CopyToAsync(output, buffer, token);

    ValueTask IAsyncBinaryReader.CopyToAsync(PipeWriter output, long? count, CancellationToken token)
        => count.HasValue ? output.CopyFromAsync(stream, count.GetValueOrDefault(), token) : new(stream.CopyToAsync(output, token));

    ValueTask IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, long? count, CancellationToken token)
        => count.HasValue ? stream.CopyToAsync(writer, count.GetValueOrDefault(), token: token) : stream.CopyToAsync(writer, token: token);

    ValueTask IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, long? count, CancellationToken token)
        => count.HasValue ? stream.CopyToAsync(consumer, count.GetValueOrDefault(), buffer, token) : stream.CopyToAsync(consumer, buffer, token);

    bool IAsyncBinaryReader.TryGetSequence(out ReadOnlySequence<byte> bytes)
    {
        bytes = default;
        return false;
    }

    bool IAsyncBinaryReader.TryGetRemainingBytesCount(out long count)
    {
        bool result;
        count = (result = stream.CanSeek)
            ? long.Max(0L, stream.Length - stream.Position)
            : default;

        return result;
    }

    #endregion

    #region Writer
    ValueTask IAsyncBinaryWriter.WriteAsync<T>(T value, CancellationToken token)
        => stream.WriteAsync(value, buffer, token);

    ValueTask IAsyncBinaryWriter.WriteLittleEndianAsync<T>(T value, CancellationToken token)
        => stream.WriteLittleEndianAsync(value, buffer, token);

    ValueTask IAsyncBinaryWriter.WriteBigEndianAsync<T>(T value, CancellationToken token)
        => stream.WriteBigEndianAsync(value, buffer, token);

    Memory<byte> IAsyncBinaryWriter.Buffer => buffer;

    ValueTask IAsyncBinaryWriter.AdvanceAsync(int bytesWritten, CancellationToken token) => bytesWritten switch
    {
        < 0 => ValueTask.FromException(new ArgumentOutOfRangeException(nameof(bytesWritten))),
        0 => ValueTask.CompletedTask,
        > 0 => stream.WriteAsync(buffer.Slice(0, bytesWritten), token),
    };

    ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token)
        => lengthFormat.HasValue ? stream.WriteAsync(input, lengthFormat.GetValueOrDefault(), buffer, token) : stream.WriteAsync(input, token);

    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
        => stream.WriteAsync(input, token);

    ValueTask<long> IAsyncBinaryWriter.EncodeAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token)
        => stream.EncodeAsync(chars, context, lengthFormat, buffer, token);

    ValueTask<int> IAsyncBinaryWriter.FormatAsync<T>(T value, LengthFormat? lengthFormat, string? format, IFormatProvider? provider, CancellationToken token)
        => stream.FormatAsync(value, lengthFormat, buffer, format, provider, token);

    ValueTask<long> IAsyncBinaryWriter.FormatAsync<T>(T value, EncodingContext context, LengthFormat? lengthFormat, string? format, IFormatProvider? provider, MemoryAllocator<char>? allocator, CancellationToken token)
        => stream.FormatAsync(value, context, lengthFormat, buffer, format, provider, allocator: null, token);

    ValueTask IAsyncBinaryWriter.CopyFromAsync(Stream source, long? count, CancellationToken token)
        => count.HasValue ? source.CopyToAsync(stream, count.GetValueOrDefault(), buffer, token) : source.CopyToAsync(stream, buffer, token);

    ValueTask IAsyncBinaryWriter.CopyFromAsync(PipeReader source, long? count, CancellationToken token)
        => count.HasValue ? source.CopyToAsync<StreamConsumer>(stream, count.GetValueOrDefault(), token) : new(source.CopyToAsync(stream, token));

    IBufferWriter<byte>? IAsyncBinaryWriter.TryGetBufferWriter() => null;

    #endregion
}