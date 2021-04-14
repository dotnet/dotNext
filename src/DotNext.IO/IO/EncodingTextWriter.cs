using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;

    internal sealed class EncodingTextWriter<TWriter> : TextBufferWriter<byte, TWriter>
        where TWriter : class, IBufferWriter<byte>
    {
        private const int CharBufferSize = 64;

        internal EncodingTextWriter(TWriter writer, Encoding encoding, IFormatProvider? provider, Action<TWriter>? flush, Func<TWriter, CancellationToken, Task>? flushAsync)
            : base(writer, provider, flush, flushAsync)
        {
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        }

        public override Encoding Encoding { get; }

        public override void Write(ReadOnlySpan<char> buffer)
        {
            var bufferSize = Encoding.GetMaxByteCount(buffer.Length);
            using var rental = bufferSize <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[bufferSize] : new MemoryRental<byte>(bufferSize);
            bufferSize = Encoding.GetBytes(buffer, rental.Span);
            WriteCore(rental.Span.Slice(0, bufferSize));
        }

        public override void Write(decimal value)
        {
            var writer = new BufferWriterSlim<char>(stackalloc char[CharBufferSize]);
            try
            {
                writer.WriteDecimal(value, string.Empty, FormatProvider);
                Write(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        public override void Write(double value)
        {
            var writer = new BufferWriterSlim<char>(stackalloc char[CharBufferSize]);
            try
            {
                writer.WriteDouble(value, string.Empty, FormatProvider);
                Write(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        public override void Write(float value)
        {
            var writer = new BufferWriterSlim<char>(stackalloc char[CharBufferSize]);
            try
            {
                writer.WriteSingle(value, string.Empty, FormatProvider);
                Write(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        public override void Write(int value)
        {
            var writer = new BufferWriterSlim<char>(stackalloc char[CharBufferSize]);
            try
            {
                writer.WriteInt32(value, string.Empty, FormatProvider);
                Write(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        public override void Write(long value)
        {
            var writer = new BufferWriterSlim<char>(stackalloc char[CharBufferSize]);
            try
            {
                writer.WriteInt64(value, string.Empty, FormatProvider);
                Write(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        public override void Write(uint value)
        {
            var writer = new BufferWriterSlim<char>(stackalloc char[CharBufferSize]);
            try
            {
                writer.WriteUInt32(value, string.Empty, FormatProvider);
                Write(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        public override void Write(ulong value)
        {
            var writer = new BufferWriterSlim<char>(stackalloc char[CharBufferSize]);
            try
            {
                writer.WriteUInt64(value, string.Empty, FormatProvider);
                Write(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private protected override void Write(DateTime value)
        {
            var writer = new BufferWriterSlim<char>(stackalloc char[CharBufferSize]);
            try
            {
                writer.WriteDateTime(value, string.Empty, FormatProvider);
                Write(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private protected override void Write(DateTimeOffset value)
        {
            var writer = new BufferWriterSlim<char>(stackalloc char[CharBufferSize]);
            try
            {
                writer.WriteDateTimeOffset(value, string.Empty, FormatProvider);
                Write(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private protected override void Write(TimeSpan value)
        {
            var writer = new BufferWriterSlim<char>(stackalloc char[CharBufferSize]);
            try
            {
                writer.WriteTimeSpan(value, string.Empty, FormatProvider);
                Write(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }
    }
}