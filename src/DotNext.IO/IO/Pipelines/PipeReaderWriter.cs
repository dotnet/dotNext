using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Pipelines
{
    using Buffers;
    using Text;

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

        ValueTask<MemoryOwner<byte>> IAsyncBinaryReader.ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
            => input.ReadBlockAsync(lengthFormat, allocator, token);

        public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token)
            => input.ReadStringAsync(length, context, token);

        public ValueTask<string> ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
            => input.ReadStringAsync(lengthFormat, context, token);

        ValueTask<byte> IAsyncBinaryReader.ReadByteAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadByteAsync(lengthFormat, context, style, provider, token);

        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadInt16Async(lengthFormat, context, style, provider, token);

        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadInt32Async(lengthFormat, context, style, provider, token);

        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadInt64Async(lengthFormat, context, style, provider, token);

        ValueTask<float> IAsyncBinaryReader.ReadSingleAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadSingleAsync(lengthFormat, context, style, provider, token);

        ValueTask<double> IAsyncBinaryReader.ReadDoubleAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDoubleAsync(lengthFormat, context, style, provider, token);

        ValueTask<decimal> IAsyncBinaryReader.ReadDecimalAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDecimalAsync(lengthFormat, context, style, provider, token);

        ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadBigIntegerAsync(lengthFormat, context, style, provider, token);

        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
            => input.ReadGuidAsync(lengthFormat, context, token);

        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(LengthFormat lengthFormat, DecodingContext context, string format, CancellationToken token)
            => input.ReadGuidAsync(lengthFormat, context, format, token);

        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDateTimeAsync(lengthFormat, context, style, provider, token);

        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDateTimeAsync(lengthFormat, context, formats, style, provider, token);

        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDateTimeOffsetAsync(lengthFormat, context, style, provider, token);

        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => input.ReadDateTimeOffsetAsync(lengthFormat, context, formats, style, provider, token);

        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider, CancellationToken token)
            => input.ReadTimeSpanAsync(lengthFormat, context, provider, token);

        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, TimeSpanStyles style, IFormatProvider? provider, CancellationToken token)
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
            => input.CopyToAsync(consumer, arg, token);

        Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> consumer, TArg arg, CancellationToken token)
            => input.CopyToAsync(consumer, arg, token);

        Task IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
            => input.CopyToAsync(consumer, token);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct PipeBinaryWriter : IAsyncBinaryWriter
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly unsafe struct Writer<TArg> : ISupplier<PipeWriter, CancellationToken, ValueTask<FlushResult>>
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
        private readonly unsafe struct Writer<T1, T2> : ISupplier<PipeWriter, CancellationToken, ValueTask<FlushResult>>
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
        private readonly unsafe struct Writer<T1, T2, T3> : ISupplier<PipeWriter, CancellationToken, ValueTask<FlushResult>>
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
        private readonly unsafe struct Writer<T1, T2, T3, T4> : ISupplier<PipeWriter, CancellationToken, ValueTask<FlushResult>>
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
        private readonly unsafe struct Writer<T1, T2, T3, T4, T5> : ISupplier<PipeWriter, CancellationToken, ValueTask<FlushResult>>
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
            where TWriter : struct, ISupplier<PipeWriter, CancellationToken, ValueTask<FlushResult>>
        {
            var result = await writer.Invoke(output, token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);
        }

        public unsafe ValueTask WriteAsync<T>(T value, CancellationToken token)
            where T : unmanaged
            => WriteAsync(output, new Writer<T>(value, &PipeExtensions.WriteAsync), token);

        public unsafe ValueTask WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token)
            => WriteAsync(output, new Writer<ReadOnlyMemory<byte>, LengthFormat?>(input, lengthFormat, &PipeExtensions.WriteBlockAsync), token);

        unsafe ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
        {
            return WriteAsync(output, new Writer<ReadOnlyMemory<byte>>(input, &WriteMemoryAsync), token);

            static ValueTask<FlushResult> WriteMemoryAsync(PipeWriter output, ReadOnlyMemory<byte> input, CancellationToken token)
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

        public unsafe ValueTask WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token)
        {
            if (chars.Length > stringLengthThreshold)
                return output.WriteStringAsync(chars, context, lengthFormat: lengthFormat, token: token);

            return WriteAsync(output, new Writer<ReadOnlyMemory<char>, EncodingContext, LengthFormat?>(chars, context, lengthFormat, &WriteAndFlushOnceAsync), token);

            static ValueTask<FlushResult> WriteAndFlushOnceAsync(PipeWriter output, ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token)
            {
                output.WriteString(chars.Span, context, lengthFormat: lengthFormat);
                return output.FlushAsync(token);
            }
        }

        unsafe ValueTask IAsyncBinaryWriter.WriteByteAsync(byte value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<byte, LengthFormat, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteByteAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<short, LengthFormat, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteInt16Async), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<int, LengthFormat, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteInt32Async), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<long, LengthFormat, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteInt64Async), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteSingleAsync(float value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<float, LengthFormat, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteSingleAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteDoubleAsync(double value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<double, LengthFormat, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteDoubleAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteDecimalAsync(decimal value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<decimal, LengthFormat, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteDecimalAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteGuidAsync(Guid value, LengthFormat lengthFormat, EncodingContext context, string? format, CancellationToken token)
            => WriteAsync(output, new Writer<Guid, LengthFormat, EncodingContext, string?>(value, lengthFormat, context, format, &PipeExtensions.WriteGuidAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteDateTimeAsync(DateTime value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<DateTime, LengthFormat, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteDateTimeAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteDateTimeOffsetAsync(DateTimeOffset value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<DateTimeOffset, LengthFormat, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteDateTimeOffsetAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteTimeSpanAsync(TimeSpan value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<TimeSpan, LengthFormat, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteTimeSpanAsync), token);

        unsafe ValueTask IAsyncBinaryWriter.WriteBigIntegerAsync(BigInteger value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            => WriteAsync(output, new Writer<BigInteger, LengthFormat, EncodingContext, string?, IFormatProvider?>(value, lengthFormat, context, format, provider, &PipeExtensions.WriteBigIntegerAsync), token);

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