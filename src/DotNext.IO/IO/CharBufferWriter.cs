using System.Buffers;
using System.Diagnostics;
using System.Text;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.IO;

using Buffers;

internal sealed unsafe class CharBufferWriter<TWriter> : TextBufferWriter<char, TWriter>
    where TWriter : class, IBufferWriter<char>
{
    private readonly delegate*<TWriter, ReadOnlySpan<char>, void> writeImpl;

    internal CharBufferWriter(TWriter writer, IFormatProvider? provider, Action<TWriter>? flush, Func<TWriter, CancellationToken, Task>? flushAsync)
        : base(writer, provider, flush, flushAsync)
    {
        writeImpl = writer is IReadOnlySpanConsumer<char> ?
            &DirectWrite :
            &BuffersExtensions.Write<char>;

        static void DirectWrite(TWriter output, ReadOnlySpan<char> input)
        {
            Debug.Assert(output is IReadOnlySpanConsumer<char>);
            Unsafe.As<IReadOnlySpanConsumer<char>>(output).Invoke(input);
        }
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(ReadOnlySpan<char> buffer) => writeImpl(writer, buffer);

    public override void Write(decimal value)
        => writer.WriteFormattable(value, provider: FormatProvider);

    public override void Write(double value)
        => writer.WriteFormattable(value, provider: FormatProvider);

    public override void Write(float value)
        => writer.WriteFormattable(value, provider: FormatProvider);

    public override void Write(int value)
        => writer.WriteFormattable(value, provider: FormatProvider);

    public override void Write(long value)
        => writer.WriteFormattable(value, provider: FormatProvider);

    public override void Write(uint value)
        => writer.WriteFormattable(value, provider: FormatProvider);

    public override void Write(ulong value)
        => writer.WriteFormattable(value, provider: FormatProvider);

    private protected override void Write(DateTime value)
        => writer.WriteFormattable(value, provider: FormatProvider);

    private protected override void Write(DateTimeOffset value)
        => writer.WriteFormattable(value, provider: FormatProvider);

    private protected override void Write(TimeSpan value)
        => writer.WriteFormattable(value, provider: FormatProvider);

    public override string ToString()
        => writer is ArrayBufferWriter<char> buffer ? buffer.BuildString() : writer.ToString() ?? string.Empty;
}