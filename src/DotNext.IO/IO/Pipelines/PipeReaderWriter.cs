using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Pipelines
{
    using Text;
    using static Buffers.BufferWriter;

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct PipeBinaryReader : IAsyncBinaryReader
    {
        private readonly PipeReader input;

        internal PipeBinaryReader(PipeReader reader) => input = reader ?? throw new ArgumentNullException(nameof(reader));

        public ValueTask<T> ReadAsync<T>(CancellationToken token)
            where T : unmanaged
            => input.ReadAsync<T>(token);

        public ValueTask ReadAsync(Memory<byte> output, CancellationToken token)
            => input.ReadBlockAsync(output, token);

        public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token)
            => input.ReadStringAsync(length, context, token);

        public ValueTask<string> ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token)
            => input.ReadStringAsync(lengthFormat, context, token);

        ValueTask<byte> IAsyncBinaryReader.ReadByteAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadByteAsync(lengthFormat, context, style, provider, token);

        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadInt16Async(lengthFormat, context, style, provider, token);

        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadInt32Async(lengthFormat, context, style, provider, token);

        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadInt64Async(lengthFormat, context, style, provider, token);

        ValueTask<float> IAsyncBinaryReader.ReadSingleAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadSingleAsync(lengthFormat, context, style, provider, token);

        ValueTask<double> IAsyncBinaryReader.ReadDoubleAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDoubleAsync(lengthFormat, context, style, provider, token);

        ValueTask<decimal> IAsyncBinaryReader.ReadDecimalAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDecimalAsync(lengthFormat, context, style, provider, token);

        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token)
            => input.ReadGuidAsync(lengthFormat, context, token);

        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(StringLengthEncoding lengthFormat, DecodingContext context, string format, CancellationToken token)
            => input.ReadGuidAsync(lengthFormat, context, format, token);

        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(StringLengthEncoding lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDateTimeAsync(lengthFormat, context, style, provider, token);

        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(StringLengthEncoding lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDateTimeAsync(lengthFormat, context, formats, style, provider, token);

        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(StringLengthEncoding lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDateTimeOffsetAsync(lengthFormat, context, style, provider, token);

        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(StringLengthEncoding lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDateTimeOffsetAsync(lengthFormat, context, formats, style, provider, token);

        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(StringLengthEncoding lengthFormat, DecodingContext context, IFormatProvider? provider, CancellationToken token)
            => input.ReadTimeSpanAsync(lengthFormat, context, provider, token);

        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(StringLengthEncoding lengthFormat, DecodingContext context, string[] formats, TimeSpanStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadTimeSpanAsync(lengthFormat, context, formats, style, provider, token);

        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(bool littleEndian, CancellationToken token)
            => input.ReadInt16Async(littleEndian, token);

        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(bool littleEndian, CancellationToken token)
            => input.ReadInt32Async(littleEndian, token);

        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(bool littleEndian, CancellationToken token)
            => input.ReadInt64Async(littleEndian, token);

        Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
            => input.CopyToAsync(output, token);

        Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
            => input.CopyToAsync(output, token);

        Task IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, CancellationToken token)
            => input.CopyToAsync(writer, token);

        Task IAsyncBinaryReader.CopyToAsync<TArg>(ReadOnlySpanAction<byte, TArg> consumer, TArg arg, CancellationToken token)
            => input.ReadAsync(consumer, arg, token);

        Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<ReadOnlyMemory<byte>, TArg, CancellationToken, ValueTask> consumer, TArg arg, CancellationToken token)
            => input.ReadAsync(consumer, arg, token);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct PipeBinaryWriter : IAsyncBinaryWriter
    {
        private readonly PipeWriter output;
        private readonly int stringLengthThreshold;
        private readonly int stringEncodingBufferSize;

        internal PipeBinaryWriter(PipeWriter writer, int stringLengthThreshold = -1, int encodingBufferSize = 0)
        {
            output = writer ?? throw new ArgumentNullException(nameof(writer));
            this.stringLengthThreshold = stringLengthThreshold;
            stringEncodingBufferSize = encodingBufferSize;
        }

        public ValueTask WriteAsync<T>(T value, CancellationToken token)
            where T : unmanaged
        {
            return WriteAsync(output, value, token);

            static async ValueTask WriteAsync(PipeWriter output, T value, CancellationToken token)
            {
                var result = await output.WriteAsync(value, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token)
        {
            return WriteAsync(output, input, token);

            static async ValueTask WriteAsync(PipeWriter output, ReadOnlyMemory<byte> input, CancellationToken token)
            {
                var result = await output.WriteAsync(input, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        public ValueTask WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, StringLengthEncoding? lengthFormat, CancellationToken token)
        {
            if (chars.Length > stringLengthThreshold)
            {
                return output.WriteStringAsync(chars, context, lengthFormat: lengthFormat, token: token);
            }

            return WriteAndFlushOnceAsync(output, chars, context, lengthFormat, token);

            static async ValueTask WriteAndFlushOnceAsync(PipeWriter output, ReadOnlyMemory<char> chars, EncodingContext context, StringLengthEncoding? lengthFormat, CancellationToken token)
            {
                output.WriteString(chars.Span, context, lengthFormat: lengthFormat);
                var result = await output.FlushAsync(token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        // TODO: Can be optimized using function pointers in C# 9
        ValueTask IAsyncBinaryWriter.WriteByteAsync(byte value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            return WriteAsync(output, value, lengthFormat, context, format, provider, token);

            static async ValueTask WriteAsync(PipeWriter output, byte value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            {
                var result = await output.WriteByteAsync(value, lengthFormat, context, format, provider, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested();
            }
        }

        ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            return WriteAsync(output, value, lengthFormat, context, format, provider, token);

            static async ValueTask WriteAsync(PipeWriter output, short value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            {
                var result = await output.WriteInt16Async(value, lengthFormat, context, format, provider, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            return WriteAsync(output, value, lengthFormat, context, format, provider, token);

            static async ValueTask WriteAsync(PipeWriter output, int value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            {
                var result = await output.WriteInt32Async(value, lengthFormat, context, format, provider, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested();
            }
        }

        ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            return WriteAsync(output, value, lengthFormat, context, format, provider, token);

            static async ValueTask WriteAsync(PipeWriter output, long value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            {
                var result = await output.WriteInt64Async(value, lengthFormat, context, format, provider, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        ValueTask IAsyncBinaryWriter.WriteSingleAsync(float value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            return WriteAsync(output, value, lengthFormat, context, format, provider, token);

            static async ValueTask WriteAsync(PipeWriter output, float value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            {
                var result = await output.WriteSingleAsync(value, lengthFormat, context, format, provider, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        ValueTask IAsyncBinaryWriter.WriteDoubleAsync(double value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            return WriteAsync(output, value, lengthFormat, context, format, provider, token);

            static async ValueTask WriteAsync(PipeWriter output, double value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            {
                var result = await output.WriteDoubleAsync(value, lengthFormat, context, format, provider, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        ValueTask IAsyncBinaryWriter.WriteDecimalAsync(decimal value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            return WriteAsync(output, value, lengthFormat, context, format, provider, token);

            static async ValueTask WriteAsync(PipeWriter output, decimal value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            {
                var result = await output.WriteDecimalAsync(value, lengthFormat, context, format, provider, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        ValueTask IAsyncBinaryWriter.WriteGuidAsync(Guid value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, CancellationToken token)
        {
            return WriteAsync(output, value, lengthFormat, context, format, token);

            static async ValueTask WriteAsync(PipeWriter output, Guid value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, CancellationToken token)
            {
                var result = await output.WriteGuidAsync(value, lengthFormat, context, format, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        ValueTask IAsyncBinaryWriter.WriteDateTimeAsync(DateTime value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            return WriteAsync(output, value, lengthFormat, context, format, provider, token);

            static async ValueTask WriteAsync(PipeWriter output, DateTime value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            {
                var result = await output.WriteDateTimeAsync(value, lengthFormat, context, format, provider, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        ValueTask IAsyncBinaryWriter.WriteDateTimeOffsetAsync(DateTimeOffset value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            return WriteAsync(output, value, lengthFormat, context, format, provider, token);

            static async ValueTask WriteAsync(PipeWriter output, DateTimeOffset value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            {
                var result = await output.WriteDateTimeOffsetAsync(value, lengthFormat, context, format, provider, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        ValueTask IAsyncBinaryWriter.WriteTimeSpanAsync(TimeSpan value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            return WriteAsync(output, value, lengthFormat, context, format, provider, token);

            static async ValueTask WriteAsync(PipeWriter output, TimeSpan value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            {
                var result = await output.WriteTimeSpanAsync(value, lengthFormat, context, format, provider, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, bool littleEndian, CancellationToken token)
        {
            return WriteAsync(output, value, littleEndian, token);

            static async ValueTask WriteAsync(PipeWriter output, short value, bool littleEndian, CancellationToken token)
            {
                var result = await output.WriteInt16Async(value, littleEndian, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, bool littleEndian, CancellationToken token)
        {
            return WriteAsync(output, value, littleEndian, token);

            static async ValueTask WriteAsync(PipeWriter output, int value, bool littleEndian, CancellationToken token)
            {
                var result = await output.WriteInt32Async(value, littleEndian, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

        ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, bool littleEndian, CancellationToken token)
        {
            return WriteAsync(output, value, littleEndian, token);

            static async ValueTask WriteAsync(PipeWriter output, long value, bool littleEndian, CancellationToken token)
            {
                var result = await output.WriteInt64Async(value, littleEndian, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }

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