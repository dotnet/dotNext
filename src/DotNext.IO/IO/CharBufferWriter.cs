using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;

    internal sealed class CharBufferWriter<TWriter> : TextBufferWriter<char, TWriter>
        where TWriter : class, IBufferWriter<char>
    {
        internal CharBufferWriter(TWriter writer, IFormatProvider? provider, Action<TWriter>? flush, Func<TWriter, CancellationToken, Task>? flushAsync)
            : base(writer, provider, flush, flushAsync)
        {
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(ReadOnlySpan<char> buffer) => WriteCore(buffer);

        public override void Write(decimal value)
            => writer.WriteDecimal(value, string.Empty, FormatProvider);

        public override void Write(double value)
            => writer.WriteDouble(value, string.Empty, FormatProvider);

        public override void Write(float value)
            => writer.WriteSingle(value, string.Empty, FormatProvider);

        public override void Write(int value)
            => writer.WriteInt32(value, string.Empty, FormatProvider);

        public override void Write(long value)
            => writer.WriteInt64(value, string.Empty, FormatProvider);

        public override void Write(uint value)
            => writer.WriteUInt32(value, string.Empty, FormatProvider);

        public override void Write(ulong value)
            => writer.WriteUInt64(value, string.Empty, FormatProvider);

        private protected override void Write(DateTime value)
            => writer.WriteDateTime(value, string.Empty, FormatProvider);

        private protected override void Write(DateTimeOffset value)
            => writer.WriteDateTimeOffset(value, string.Empty, FormatProvider);

        private protected override void Write(TimeSpan value)
            => writer.WriteTimeSpan(value, string.Empty, FormatProvider);

        public override string ToString()
            => writer is ArrayBufferWriter<char> buffer ? buffer.BuildString() : writer.ToString() ?? string.Empty;
    }
}