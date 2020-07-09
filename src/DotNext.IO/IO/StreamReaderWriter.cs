using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Text;

    /// <summary>
    /// Represents binary reader for the stream.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct AsyncStreamBinaryReader : IAsyncBinaryReader
    {
        private readonly Memory<byte> buffer;
        private readonly Stream input;

        internal AsyncStreamBinaryReader(Stream input, Memory<byte> buffer)
        {
            if (buffer.IsEmpty)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.buffer = buffer;
        }

        public ValueTask<T> ReadAsync<T>(CancellationToken token = default)
            where T : unmanaged
            => StreamExtensions.ReadAsync<T>(input, buffer, token);

        public ValueTask ReadAsync(Memory<byte> output, CancellationToken token = default)
            => StreamExtensions.ReadBlockAsync(input, output, token);

        public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default)
            => StreamExtensions.ReadStringAsync(input, length, context, buffer, token);

        public ValueTask<string> ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token = default)
            => StreamExtensions.ReadStringAsync(input, lengthFormat, context, buffer, token);

        ValueTask<byte> IAsyncBinaryReader.ReadByteAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadByteAsync(input, lengthFormat, context, buffer, style, provider, token);

        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadInt16Async(input, lengthFormat, context, buffer, style, provider, token);

        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadInt32Async(input, lengthFormat, context, buffer, style, provider, token);

        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadInt64Async(input, lengthFormat, context, buffer, style, provider, token);

        ValueTask<decimal> IAsyncBinaryReader.ReadDecimalAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDecimalAsync(input, lengthFormat, context, buffer, style, provider, token);

        ValueTask<float> IAsyncBinaryReader.ReadSingleAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadSingleAsync(input, lengthFormat, context, buffer, style, provider, token);

        ValueTask<double> IAsyncBinaryReader.ReadDoubleAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDoubleAsync(input, lengthFormat, context, buffer, style, provider, token);

        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token)
            => StreamExtensions.ReadGuidAsync(input, lengthFormat, context, buffer, token);

        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(StringLengthEncoding lengthFormat, DecodingContext context, string format, CancellationToken token)
            => StreamExtensions.ReadGuidAsync(input, lengthFormat, context, buffer, format, token);

        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(StringLengthEncoding lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDateTimeAsync(input, lengthFormat, context, buffer, style, provider, token);

        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(StringLengthEncoding lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDateTimeAsync(input, lengthFormat, context, buffer, style, provider, token);

        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(StringLengthEncoding lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDateTimeOffsetAsync(input, lengthFormat, context, buffer, style, provider, token);

        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(StringLengthEncoding lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadDateTimeOffsetAsync(input, lengthFormat, context, buffer, style, provider, token);

        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(StringLengthEncoding lengthFormat, DecodingContext context, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadTimeSpanAsync(input, lengthFormat, context, buffer, provider, token);

        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(StringLengthEncoding lengthFormat, DecodingContext context, string[] formats, TimeSpanStyles style, IFormatProvider? provider, CancellationToken token)
            => StreamExtensions.ReadTimeSpanAsync(input, lengthFormat, context, buffer, formats, style, provider, token);

        Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
            => input.CopyToAsync(output, token);

        Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
            => input.CopyToAsync(output, token);

        Task IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, CancellationToken token)
            => input.CopyToAsync(writer, token: token);

        Task IAsyncBinaryReader.CopyToAsync<TArg>(ReadOnlySpanAction<byte, TArg> reader, TArg arg, CancellationToken token)
            => input.ReadAsync(reader, arg, buffer, token);

        Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<ReadOnlyMemory<byte>, TArg, CancellationToken, ValueTask> reader, TArg arg, CancellationToken token)
            => input.ReadAsync(reader, arg, buffer, token);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct AsyncStreamBinaryWriter : IAsyncBinaryWriter
    {
        private readonly Memory<byte> buffer;
        private readonly Stream output;

        internal AsyncStreamBinaryWriter(Stream output, Memory<byte> buffer)
        {
            if (buffer.IsEmpty)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
            this.output = output ?? throw new ArgumentNullException(nameof(output));
            this.buffer = buffer;
        }

        public ValueTask WriteAsync<T>(T value, CancellationToken token)
            where T : unmanaged
            => output.WriteAsync(value, buffer, token);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token)
            => output.WriteAsync(input, token);

        public ValueTask WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, StringLengthEncoding? lengthFormat, CancellationToken token)
            => output.WriteStringAsync(chars, context, buffer, lengthFormat, token);

        ValueTask IAsyncBinaryWriter.WriteByteAsync(byte value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => output.WriteByteAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => output.WriteInt16Async(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => output.WriteInt32Async(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => output.WriteInt64Async(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteDecimalAsync(decimal value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => output.WriteDecimalAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteSingleAsync(float value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => output.WriteSingleAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteDoubleAsync(double value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => output.WriteDoubleAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteDateTimeAsync(DateTime value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => output.WriteDateTimeAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteDateTimeOffsetAsync(DateTimeOffset value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => output.WriteDateTimeOffsetAsync(value, lengthFormat, context, buffer, format, provider, token);

        ValueTask IAsyncBinaryWriter.WriteTimeSpanAsync(TimeSpan value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => output.WriteTimeSpanAsync(value, lengthFormat, context, buffer, format, provider, token);

        Task IAsyncBinaryWriter.CopyFromAsync(Stream input, CancellationToken token)
            => input.CopyToAsync(output, token);

        Task IAsyncBinaryWriter.CopyFromAsync(PipeReader input, CancellationToken token)
            => input.CopyToAsync(output, token);

        Task IAsyncBinaryWriter.WriteAsync(ReadOnlySequence<byte> input, CancellationToken token)
            => output.WriteAsync(input, token).AsTask();

        Task IAsyncBinaryWriter.CopyFromAsync<TArg>(Func<TArg, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> supplier, TArg arg, CancellationToken token)
            => output.WriteAsync(supplier, arg, token);
    }
}