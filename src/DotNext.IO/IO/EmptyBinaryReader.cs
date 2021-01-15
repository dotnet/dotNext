using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;
    using DecodingContext = Text.DecodingContext;

    internal sealed class EmptyBinaryReader : IAsyncBinaryReader
    {
        private static ValueTask<T> EndOfStream<T>()
            => new ValueTask<T>(Task.FromException<T>(new EndOfStreamException()));

        private static ValueTask EndOfStream()
            => new ValueTask(Task.FromException(new EndOfStreamException()));

        public ValueTask<T> ReadAsync<T>(CancellationToken token)
            where T : unmanaged
            => EndOfStream<T>();

        public ValueTask ReadAsync(Memory<byte> output, CancellationToken token)
            => EndOfStream();

        public ValueTask<MemoryOwner<byte>> ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
            => EndOfStream<MemoryOwner<byte>>();

        public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token)
            => EndOfStream<string>();

        public ValueTask<string> ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
            => EndOfStream<string>();

        public Task CopyToAsync(Stream output, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        public Task CopyToAsync(PipeWriter output, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

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

        Task IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> consumer, TArg arg, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        Task IAsyncBinaryReader.CopyToAsync<TArg>(ReadOnlySpanAction<byte, TArg> consumer, TArg arg, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        Task IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
    }
}