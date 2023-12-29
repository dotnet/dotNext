using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.IO.Pipelines;

using Buffers;
using Text;

[StructLayout(LayoutKind.Auto)]
internal readonly struct PipeBinaryReader(PipeReader reader) : IAsyncBinaryReader
{
    internal Stream AsStream() => reader.AsStream(leaveOpen: true);

    ValueTask<T> IAsyncBinaryReader.ReadAsync<T>(CancellationToken token)
        => reader.ReadAsync<T>(token);

    ValueTask<T> IAsyncBinaryReader.ReadLittleEndianAsync<T>(CancellationToken token)
        => reader.ReadLittleEndianAsync<T>(token);

    ValueTask<T> IAsyncBinaryReader.ReadBigEndianAsync<T>(CancellationToken token)
        => reader.ReadBigEndianAsync<T>(token);

    ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
        => reader.ReadExactlyAsync(output, token);

    ValueTask<TReader> IAsyncBinaryReader.ReadAsync<TReader>(TReader parser, CancellationToken token)
        => PipeExtensions.ReadAsync<TReader, BufferReader<TReader>>(reader, parser, token);

    ValueTask IAsyncBinaryReader.SkipAsync(long length, CancellationToken token)
        => reader.SkipAsync(length, token);

    ValueTask<MemoryOwner<byte>> IAsyncBinaryReader.ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
        => reader.ReadAsync(lengthFormat, allocator, token);

    ValueTask<MemoryOwner<char>> IAsyncBinaryReader.DecodeAsync(DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator, CancellationToken token)
        => reader.DecodeAsync(context, lengthFormat, allocator, token);

    IAsyncEnumerable<ReadOnlyMemory<char>> IAsyncBinaryReader.DecodeAsync(DecodingContext context, LengthFormat lengthFormat, Memory<char> buffer, CancellationToken token)
        => reader.DecodeAsync(context, lengthFormat, buffer, token);

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        => reader.ParseAsync<T>(lengthFormat, style, provider, token);

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(LengthFormat lengthFormat, IFormatProvider? provider, CancellationToken token)
        => reader.ParseAsync<T>(lengthFormat, provider, token);

    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(DecodingContext context, LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider, MemoryAllocator<char>? allocator, CancellationToken token)
        => reader.ParseAsync((style, provider), IAsyncBinaryReader.Parse<T>, context, lengthFormat, allocator, token);

    ValueTask<TResult> IAsyncBinaryReader.ParseAsync<TArg, TResult>(TArg arg, ReadOnlySpanFunc<char, TArg, TResult> parser, DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator, CancellationToken token)
        => reader.ParseAsync(arg, parser, context, lengthFormat, allocator, token);

    ValueTask IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
        => new(reader.CopyToAsync(output, token));

    ValueTask IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
        => new(reader.CopyToAsync(output, token));

    ValueTask IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, CancellationToken token)
        => reader.CopyToAsync(writer, token);

    ValueTask IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        => reader.CopyToAsync(consumer, token);

    bool IAsyncBinaryReader.TryGetSequence(out ReadOnlySequence<byte> bytes)
    {
        bytes = default;
        return false;
    }
}

[StructLayout(LayoutKind.Auto)]
internal readonly struct PipeBinaryWriter(PipeWriter writer, long bufferSize) : IAsyncBinaryWriter
{
    internal Stream AsStream() => writer.AsStream(leaveOpen: true);

    private ValueTask FlushIfNeededAsync(CancellationToken token)
    {
        return !writer.CanGetUnflushedBytes || writer.UnflushedBytes > bufferSize
            ? FlushAsync(writer, token)
            : ValueTask.CompletedTask;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask FlushAsync(PipeWriter writer, CancellationToken token)
        {
            var result = await writer.FlushAsync(token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);
        }
    }

    private ValueTask<T> FlushIfNeededAsync<T>(T value, CancellationToken token)
    {
        return !writer.CanGetUnflushedBytes || writer.UnflushedBytes > bufferSize
            ? FlushAsync(writer, value, token)
            : ValueTask.FromResult(value);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<T> FlushAsync(PipeWriter writer, T value, CancellationToken token)
        {
            var result = await writer.FlushAsync(token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);
            return value;
        }
    }

    Memory<byte> IAsyncBinaryWriter.Buffer => writer.GetMemory();

    ValueTask IAsyncBinaryWriter.AdvanceAsync(int bytesWritten, CancellationToken token)
    {
        ValueTask result;
        switch (bytesWritten)
        {
            case < 0:
                result = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(bytesWritten)));
                break;
            case 0:
                result = ValueTask.CompletedTask;
                break;
            case > 0:
                writer.Advance(bytesWritten);
                result = FlushIfNeededAsync(token);
                break;
        }

        return result;
    }

    ValueTask IAsyncBinaryWriter.WriteAsync<T>(T value, CancellationToken token)
    {
        writer.Write(value);
        return FlushIfNeededAsync(token);
    }

    ValueTask IAsyncBinaryWriter.WriteLittleEndianAsync<T>(T value, CancellationToken token)
    {
        writer.WriteLittleEndian(value);
        return FlushIfNeededAsync(token);
    }

    ValueTask IAsyncBinaryWriter.WriteBigEndianAsync<T>(T value, CancellationToken token)
    {
        writer.WriteBigEndian(value);
        return FlushIfNeededAsync(token);
    }

    ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token)
    {
        if (lengthFormat.HasValue)
            writer.WriteLength(input.Length, lengthFormat.GetValueOrDefault());

        writer.Write(input.Span);
        return FlushIfNeededAsync(token);
    }

    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> source, CancellationToken token)
    {
        writer.Write(source.Span);
        return FlushIfNeededAsync(token);
    }

    ValueTask<long> IAsyncBinaryWriter.EncodeAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token)
        => FlushIfNeededAsync(writer.Encode(chars.Span, in context, lengthFormat), token);

    ValueTask<long> IAsyncBinaryWriter.FormatAsync<T>(T value, EncodingContext context, LengthFormat? lengthFormat, string? format, IFormatProvider? provider, MemoryAllocator<char>? allocator, CancellationToken token)
        => FlushIfNeededAsync(writer.Format(value, in context, lengthFormat, format, provider, allocator), token);

    ValueTask<int> IAsyncBinaryWriter.FormatAsync<T>(T value, LengthFormat? lengthFormat, string? format, IFormatProvider? provider, CancellationToken token)
        => FlushIfNeededAsync(writer.Format(value, lengthFormat, format, provider), token);

    ValueTask IAsyncBinaryWriter.CopyFromAsync(Stream input, CancellationToken token)
        => new(input.CopyToAsync(writer, token));

    ValueTask IAsyncBinaryWriter.CopyFromAsync(PipeReader input, CancellationToken token)
        => new(input.CopyToAsync(writer, token));

    IBufferWriter<byte>? IAsyncBinaryWriter.TryGetBufferWriter() => writer;
}