using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
#if !NETSTANDARD2_1
using System.Text.Json;
#endif
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

        Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> consumer, TArg arg, CancellationToken token)
            => input.ReadAsync(consumer, arg, token);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct PipeBinaryWriter : IAsyncBinaryWriter
    {
        private interface IWriter
        {
            ValueTask<FlushResult> Invoke(PipeWriter pipe, CancellationToken token);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly unsafe struct Writer<TArg> : IWriter
            where TArg : struct
        {
            private readonly TArg arg;
            private readonly delegate*<PipeWriter, TArg, CancellationToken, ValueTask<FlushResult>> writer;

            internal Writer(TArg arg, delegate*<PipeWriter, TArg, CancellationToken, ValueTask<FlushResult>> writer)
            {
                this.arg = arg;
                this.writer = writer;
            }

            public ValueTask<FlushResult> Invoke(PipeWriter pipe, CancellationToken token)
                => writer(pipe, arg, token);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly unsafe struct Writer<T1, T2> : IWriter
        {
            private readonly T1 arg1;
            private readonly T2 arg2;
            private readonly delegate*<PipeWriter, T1, T2, CancellationToken, ValueTask<FlushResult>> writer;

            internal Writer(T1 arg1, T2 arg2, delegate*<PipeWriter, T1, T2, CancellationToken, ValueTask<FlushResult>> writer)
            {
                this.arg1 = arg1;
                this.arg2 = arg2;
                this.writer = writer;
            }

            public ValueTask<FlushResult> Invoke(PipeWriter pipe, CancellationToken token)
                => writer(pipe, arg1, arg2, token);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly unsafe struct Writer<T1, T2, T3> : IWriter
        {
            private readonly T1 arg1;
            private readonly T2 arg2;
            private readonly T3 arg3;
            private readonly delegate*<PipeWriter, T1, T2, T3, CancellationToken, ValueTask<FlushResult>> writer;

            internal Writer(T1 arg1, T2 arg2, T3 arg3, delegate*<PipeWriter, T1, T2, T3, CancellationToken, ValueTask<FlushResult>> writer)
            {
                this.arg1 = arg1;
                this.arg2 = arg2;
                this.arg3 = arg3;
                this.writer = writer;
            }

            public ValueTask<FlushResult> Invoke(PipeWriter pipe, CancellationToken token)
                => writer(pipe, arg1, arg2, arg3, token);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly unsafe struct Writer<T1, T2, T3, T4> : IWriter
        {
            private readonly T1 arg1;
            private readonly T2 arg2;
            private readonly T3 arg3;
            private readonly T4 arg4;
            private readonly delegate*<PipeWriter, T1, T2, T3, T4, CancellationToken, ValueTask<FlushResult>> writer;

            internal Writer(T1 arg1, T2 arg2, T3 arg3, T4 arg4, delegate*<PipeWriter, T1, T2, T3, T4, CancellationToken, ValueTask<FlushResult>> writer)
            {
                this.arg1 = arg1;
                this.arg2 = arg2;
                this.arg3 = arg3;
                this.arg4 = arg4;
                this.writer = writer;
            }

            public ValueTask<FlushResult> Invoke(PipeWriter pipe, CancellationToken token)
                => writer(pipe, arg1, arg2, arg3, arg4, token);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly unsafe struct Writer<T1, T2, T3, T4, T5> : IWriter
        {
            private readonly T1 arg1;
            private readonly T2 arg2;
            private readonly T3 arg3;
            private readonly T4 arg4;
            private readonly T5 arg5;
            private readonly delegate*<PipeWriter, T1, T2, T3, T4, T5, CancellationToken, ValueTask<FlushResult>> writer;

            internal Writer(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, delegate*<PipeWriter, T1, T2, T3, T4, T5, CancellationToken, ValueTask<FlushResult>> writer)
            {
                this.arg1 = arg1;
                this.arg2 = arg2;
                this.arg3 = arg3;
                this.arg4 = arg4;
                this.arg5 = arg5;
                this.writer = writer;
            }

            public ValueTask<FlushResult> Invoke(PipeWriter pipe, CancellationToken token)
                => writer(pipe, arg1, arg2, arg3, arg4, arg5, token);
        }

        private readonly PipeWriter output;
        private readonly int stringLengthThreshold;
        private readonly int stringEncodingBufferSize;

        internal PipeBinaryWriter(PipeWriter writer, int stringLengthThreshold = -1, int encodingBufferSize = 0)
        {
            output = writer ?? throw new ArgumentNullException(nameof(writer));
            this.stringLengthThreshold = stringLengthThreshold;
            stringEncodingBufferSize = encodingBufferSize;
        }

        private static async ValueTask WriteAsync<TWriter>(PipeWriter output, TWriter writer, CancellationToken token)
            where TWriter : struct, IWriter
        {
            var result = await writer.Invoke(output, token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);
        }

        public unsafe ValueTask WriteAsync<T>(T value, CancellationToken token)
            where T : unmanaged
            => WriteAsync(output, new Writer<T>(value, &PipeExtensions.WriteAsync), token);

        public unsafe ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token)
        {
            return WriteAsync(output, new Writer<ReadOnlyMemory<byte>>(input, &WriteBlockAsync), token);

            static ValueTask<FlushResult> WriteBlockAsync(PipeWriter output, ReadOnlyMemory<byte> input, CancellationToken token)
                => output.WriteAsync(input, token);
        }

        unsafe ValueTask IAsyncBinaryWriter.WriteAsync<TArg>(Action<TArg, IBufferWriter<byte>> writer, TArg arg, CancellationToken token)
        {
            return WriteAsync(output, new Writer<Action<TArg, IBufferWriter<byte>>, TArg>(writer, arg, &WriteBlockAsync), token);

            static ValueTask<FlushResult> WriteBlockAsync(PipeWriter output, Action<TArg, IBufferWriter<byte>> writer, TArg arg, CancellationToken token)
            {
                writer(arg, output);
                return output.FlushAsync(token);
            }
        }

#if !NETSTANDARD2_1
        unsafe ValueTask IAsyncBinaryWriter.WriteJsonAsync<T>(T obj, JsonSerializerOptions? options, CancellationToken token)
        {
            return WriteAsync(output, new Writer<T, JsonSerializerOptions?>(obj, options, &SerializeToJsonAsync), token);

            static ValueTask<FlushResult> SerializeToJsonAsync(PipeWriter output, T obj, JsonSerializerOptions? options, CancellationToken token)
            {
                using var writer = new Utf8JsonWriter(output, IAsyncBinaryWriter.GetWriterOptions(options));
                JsonSerializer.Serialize(writer, obj, options);
                return output.FlushAsync(token);
            }
        }
#endif

        public unsafe ValueTask WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, StringLengthEncoding? lengthFormat, CancellationToken token)
        {
            if (chars.Length > stringLengthThreshold)
                return output.WriteStringAsync(chars, context, lengthFormat: lengthFormat, token: token);

            return WriteAsync(output, new Writer<ReadOnlyMemory<char>, EncodingContext, StringLengthEncoding?>(chars, context, lengthFormat, &WriteAndFlushOnceAsync), token);

            static ValueTask<FlushResult> WriteAndFlushOnceAsync(PipeWriter output, ReadOnlyMemory<char> chars, EncodingContext context, StringLengthEncoding? lengthFormat, CancellationToken token)
            {
                output.WriteString(chars.Span, context, lengthFormat: lengthFormat);
                return output.FlushAsync(token);
            }
        }

        unsafe ValueTask IAsyncBinaryWriter.WriteByteAsync(byte value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<byte, StringLengthEncoding, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteByteAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<short, StringLengthEncoding, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteInt16Async), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<int, StringLengthEncoding, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteInt32Async), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<long, StringLengthEncoding, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteInt64Async), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteSingleAsync(float value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<float, StringLengthEncoding, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteSingleAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteDoubleAsync(double value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<double, StringLengthEncoding, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteDoubleAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteDecimalAsync(decimal value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<decimal, StringLengthEncoding, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteDecimalAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteGuidAsync(Guid value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, CancellationToken token)
            => WriteAsync(output, new Writer<Guid, StringLengthEncoding, EncodingContext, string?>(value, lengthFormat, context, format, &PipeExtensions.WriteGuidAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteDateTimeAsync(DateTime value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<DateTime, StringLengthEncoding, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteDateTimeAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteDateTimeOffsetAsync(DateTimeOffset value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<DateTimeOffset, StringLengthEncoding, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteDateTimeOffsetAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteTimeSpanAsync(TimeSpan value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<TimeSpan, StringLengthEncoding, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteTimeSpanAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, bool littleEndian, CancellationToken token)
            => WriteAsync(output, new Writer<short, bool>(value, littleEndian, &PipeExtensions.WriteInt16Async), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, bool littleEndian, CancellationToken token)
            => WriteAsync(output, new Writer<int, bool>(value, littleEndian, &PipeExtensions.WriteInt32Async), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, bool littleEndian, CancellationToken token)
            => WriteAsync(output, new Writer<long, bool>(value, littleEndian, &PipeExtensions.WriteInt64Async), token);

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