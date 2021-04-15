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
        private const int ConversionBufferSize = 64;

        internal EncodingTextWriter(TWriter writer, Encoding encoding, IFormatProvider? provider, Action<TWriter>? flush, Func<TWriter, CancellationToken, Task>? flushAsync)
            : base(writer, provider, flush, flushAsync)
        {
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        }

        public override Encoding Encoding { get; }

        public override void Write(ReadOnlySpan<char> chars)
        {
#if NETSTANDARD2_1
            const int maxInputElements = 1024;
            int byteCount;
            Span<byte> output;

            if (chars.Length <= maxInputElements)
            {
                // fast path - the input is small enough
                byteCount = Encoding.GetByteCount(chars);
                output = writer.GetSpan(byteCount);
                writer.Advance(Encoding.GetBytes(chars, output));
            }
            else
            {
                // slow path - decode by chunks
                for (var encoder = Encoding.GetEncoder(); !chars.IsEmpty; writer.Advance(byteCount))
                {
                    byteCount = chars.Length <= maxInputElements ?
                        encoder.GetByteCount(chars, true) :
                        encoder.GetByteCount(chars.Slice(0, maxInputElements), false);

                    output = writer.GetSpan(byteCount);
                    encoder.Convert(chars, output, true, out var charsProduced, out byteCount, out _);

                    chars = chars.Slice(charsProduced);
                }
            }
#else
            Encoding.GetBytes(chars, writer);
#endif
        }

        public override void Write(decimal value)
        {
            var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
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
            var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
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
            var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
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
            var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
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
            var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
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
            var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
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
            var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
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
            var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
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
            var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
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
            var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
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