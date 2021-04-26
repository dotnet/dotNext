using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;
    using Text;

    /// <summary>
    /// Represents binary reader for the stream.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly partial struct AsyncStreamBinaryAccessor : IAsyncBinaryReader, IAsyncBinaryWriter, IFlushable
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
                if (bytesRead == 0)
                    throw new EndOfStreamException();
            }
        }

        ValueTask IAsyncBinaryReader.SkipAsync(int length, CancellationToken token)
        {
            if (length < 0)
#if NETSTANDARD2_1
                return new ValueTask(Task.FromException(new ArgumentOutOfRangeException(nameof(length))));
#else
                return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(length)));
#endif

            if (!stream.CanSeek)
                return SkipSlowAsync(length, token);

            var current = stream.Position;
            if (current + length > stream.Length)
#if NETSTANDARD2_1
                return new ValueTask(Task.FromException(new EndOfStreamException()));
#else
                return ValueTask.FromException(new EndOfStreamException());
#endif

            stream.Position = length + current;
            return new ValueTask();
        }

        ValueTask<MemoryOwner<byte>> IAsyncBinaryReader.ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
            => StreamExtensions.ReadBlockAsync(stream, lengthFormat, buffer, allocator, token);

        public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default)
            => StreamExtensions.ReadStringAsync(stream, length, context, buffer, token);

        public ValueTask<string> ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token = default)
            => StreamExtensions.ReadStringAsync(stream, lengthFormat, context, buffer, token);

        ValueTask<byte> IAsyncBinaryReader.ReadByteAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadByteAsync(stream, lengthFormat, context, buffer, style, provider, token);

        async ValueTask<short> IAsyncBinaryReader.ReadInt16Async(bool littleEndian, CancellationToken token)
        {
            var result = await StreamExtensions.ReadAsync<short>(stream, buffer, token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadInt16Async(stream, lengthFormat, context, buffer, style, provider, token);

        async ValueTask<int> IAsyncBinaryReader.ReadInt32Async(bool littleEndian, CancellationToken token)
        {
            var result = await StreamExtensions.ReadAsync<int>(stream, buffer, token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadInt32Async(stream, lengthFormat, context, buffer, style, provider, token);

        async ValueTask<long> IAsyncBinaryReader.ReadInt64Async(bool littleEndian, CancellationToken token)
        {
            var result = await StreamExtensions.ReadAsync<long>(stream, buffer, token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadInt64Async(stream, lengthFormat, context, buffer, style, provider, token);

        ValueTask<decimal> IAsyncBinaryReader.ReadDecimalAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDecimalAsync(stream, lengthFormat, context, buffer, style, provider, token);

        ValueTask<float> IAsyncBinaryReader.ReadSingleAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadSingleAsync(stream, lengthFormat, context, buffer, style, provider, token);

        ValueTask<double> IAsyncBinaryReader.ReadDoubleAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDoubleAsync(stream, lengthFormat, context, buffer, style, provider, token);

        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
            => StreamExtensions.ReadGuidAsync(stream, lengthFormat, context, buffer, token);

        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(LengthFormat lengthFormat, DecodingContext context, string format, CancellationToken token)
            => StreamExtensions.ReadGuidAsync(stream, lengthFormat, context, buffer, format, token);

        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDateTimeAsync(stream, lengthFormat, context, buffer, style, provider, token);

        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDateTimeAsync(stream, lengthFormat, context, buffer, style, provider, token);

        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDateTimeOffsetAsync(stream, lengthFormat, context, buffer, style, provider, token);

        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDateTimeOffsetAsync(stream, lengthFormat, context, buffer, style, provider, token);

        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadTimeSpanAsync(stream, lengthFormat, context, buffer, provider, token);

        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, TimeSpanStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadTimeSpanAsync(stream, lengthFormat, context, buffer, formats, style, provider, token);

        ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadBigIntegerAsync(stream, lengthFormat, context, buffer, style, provider, token);

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
#endregion

#region Writer
        public ValueTask WriteAsync<T>(T value, CancellationToken token)
            where T : unmanaged
            => stream.WriteAsync(value, buffer, token);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token)
            => lengthFormat is null ? stream.WriteAsync(input, token) : stream.WriteBlockAsync(input, lengthFormat.GetValueOrDefault(), buffer, token);

        ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
            => stream.WriteAsync(input, token);

        public ValueTask WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token)
            => stream.WriteStringAsync(chars, context, buffer, lengthFormat, token);

        async ValueTask IAsyncBinaryWriter.WriteAsync<TArg>(Action<TArg, IBufferWriter<byte>> writer, TArg arg, CancellationToken token)
        {
            using var bufferWriter = new BufferedStreamWriter(buffer);
            writer(arg, bufferWriter);
            await stream.WriteAsync(bufferWriter.WrittenMemory, token).ConfigureAwait(false);
        }

        ValueTask IAsyncBinaryWriter.WriteByteAsync(byte value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => stream.WriteByteAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, bool littleEndian, CancellationToken token)
        {
            value.ReverseIfNeeded(littleEndian);
            return WriteAsync(value, token);
        }

        ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => stream.WriteInt16Async(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, bool littleEndian, CancellationToken token)
        {
            value.ReverseIfNeeded(littleEndian);
            return WriteAsync(value, token);
        }

        ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => stream.WriteInt32Async(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, bool littleEndian, CancellationToken token)
        {
            value.ReverseIfNeeded(littleEndian);
            return WriteAsync(value, token);
        }

        ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => stream.WriteInt64Async(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteDecimalAsync(decimal value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => stream.WriteDecimalAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteSingleAsync(float value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => stream.WriteSingleAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteDoubleAsync(double value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => stream.WriteDoubleAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteDateTimeAsync(DateTime value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => stream.WriteDateTimeAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteDateTimeOffsetAsync(DateTimeOffset value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => stream.WriteDateTimeOffsetAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteTimeSpanAsync(TimeSpan value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => stream.WriteTimeSpanAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteBigIntegerAsync(BigInteger value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => stream.WriteBigIntegerAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteBigIntegerAsync(BigInteger value, bool littleEndian, LengthFormat? lengthFormat, CancellationToken token)
            => stream.WriteBigIntegerAsync(value, littleEndian, buffer, lengthFormat, token);

        Task IAsyncBinaryWriter.CopyFromAsync(Stream input, CancellationToken token)
            => input.CopyToAsync(stream, token);

        Task IAsyncBinaryWriter.CopyFromAsync(PipeReader input, CancellationToken token)
            => input.CopyToAsync(stream, token);

        Task IAsyncBinaryWriter.WriteAsync(ReadOnlySequence<byte> input, CancellationToken token)
            => stream.WriteAsync(input, token).AsTask();

        Task IAsyncBinaryWriter.CopyFromAsync<TArg>(Func<TArg, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> supplier, TArg arg, CancellationToken token)
            => stream.WriteAsync(supplier, arg, token);
#endregion
    }
}