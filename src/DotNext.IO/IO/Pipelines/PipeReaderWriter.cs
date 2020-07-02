using System;
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

        internal PipeBinaryReader(PipeReader reader) => input = reader;

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
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct PipeBinaryWriter : IAsyncBinaryWriter
    {
        private readonly PipeWriter output;
        private readonly int stringLengthThreshold;
        private readonly int stringEncodingBufferSize;

        internal PipeBinaryWriter(PipeWriter writer, int stringLengthThreshold = -1, int encodingBufferSize = 0)
        {
            output = writer;
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
                result.ThrowIfCancellationRequested();
            }
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token)
        {
            return WriteAsync(output, input, token);

            static async ValueTask WriteAsync(PipeWriter output, ReadOnlyMemory<byte> input, CancellationToken token)
            {
                var result = await output.WriteAsync(input, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested();
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

        Task IAsyncBinaryWriter.CopyFromAsync(Stream input, CancellationToken token)
            => input.CopyToAsync(output, token);

        Task IAsyncBinaryWriter.CopyFromAsync(PipeReader input, CancellationToken token)
            => input.CopyToAsync(output, token);
    }
}