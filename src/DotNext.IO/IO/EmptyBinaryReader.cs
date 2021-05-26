using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;
    using DecodingContext = Text.DecodingContext;

    internal sealed class EmptyBinaryReader : IAsyncBinaryReader
    {
        internal static readonly EmptyBinaryReader Instance = new();

        private EmptyBinaryReader()
        {
        }

        private static ValueTask<T> EndOfStream<T>()
#if NETSTANDARD2_1
            => new (Task.FromException<T>(new EndOfStreamException()));
#else
            => ValueTask.FromException<T>(new EndOfStreamException());
#endif

        private static ValueTask EndOfStream()
#if NETSTANDARD2_1
            => new (Task.FromException(new EndOfStreamException()));
#else
            => ValueTask.FromException(new EndOfStreamException());
#endif

        private static Task GetCompletedOrCanceledTask(CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        public ValueTask<T> ReadAsync<T>(CancellationToken token)
            where T : unmanaged
            => EndOfStream<T>();

        public ValueTask ReadAsync(Memory<byte> output, CancellationToken token)
            => output.IsEmpty ? new() : EndOfStream();

        public ValueTask<MemoryOwner<byte>> ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
            => EndOfStream<MemoryOwner<byte>>();

        public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token)
            => length == 0 ? new(string.Empty) : EndOfStream<string>();

        public ValueTask<string> ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
            => EndOfStream<string>();

        public Task CopyToAsync(Stream output, CancellationToken token)
            => GetCompletedOrCanceledTask(token);

        public Task CopyToAsync(PipeWriter output, CancellationToken token)
            => GetCompletedOrCanceledTask(token);

        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(bool littleEndian, CancellationToken token)
            => EndOfStream<long>();

        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<long>();

        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(bool littleEndian, CancellationToken token)
            => EndOfStream<int>();

        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<int>();

        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(bool littleEndian, CancellationToken token)
            => EndOfStream<short>();

        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<short>();

        ValueTask<float> IAsyncBinaryReader.ReadSingleAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<float>();

        ValueTask<double> IAsyncBinaryReader.ReadDoubleAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<double>();

        ValueTask<byte> IAsyncBinaryReader.ReadByteAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<byte>();

        ValueTask<decimal> IAsyncBinaryReader.ReadDecimalAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<decimal>();

        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<DateTime>();

        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<DateTime>();

        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<DateTimeOffset>();

        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<DateTimeOffset>();

        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<TimeSpan>();

        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, TimeSpanStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<TimeSpan>();

        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
            => EndOfStream<Guid>();

        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(LengthFormat lengthFormat, DecodingContext context, string format, CancellationToken token)
            => EndOfStream<Guid>();

        ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
            => EndOfStream<BigInteger>();

        ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(LengthFormat lengthFormat, bool littleEndian, CancellationToken token)
            => EndOfStream<BigInteger>();

        ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(int length, bool littleEndian, CancellationToken token)
            => EndOfStream<BigInteger>();

        ValueTask IAsyncBinaryReader.SkipAsync(int length, CancellationToken token)
            => length == 0 ? new() : EndOfStream();

        bool IAsyncBinaryReader.TryGetSpan(out ReadOnlySpan<byte> bytes)
        {
            bytes = ReadOnlySpan<byte>.Empty;
            return true;
        }

        Task IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, CancellationToken token)
            => GetCompletedOrCanceledTask(token);

        Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> consumer, TArg arg, CancellationToken token)
            => GetCompletedOrCanceledTask(token);

        Task IAsyncBinaryReader.CopyToAsync<TArg>(ReadOnlySpanAction<byte, TArg> consumer, TArg arg, CancellationToken token)
            => GetCompletedOrCanceledTask(token);

        Task IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
            => GetCompletedOrCanceledTask(token);
    }
}