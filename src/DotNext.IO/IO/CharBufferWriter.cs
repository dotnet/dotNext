using System.Buffers;
using System.Diagnostics;
using System.Text;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.IO;

using Buffers.Text;

internal sealed unsafe class CharBufferWriter<TWriter> : TextBufferWriter<char, TWriter>
    where TWriter : class, IBufferWriter<char>
{
    private readonly delegate*<TWriter, ReadOnlySpan<char>, void> writeImpl;

    internal CharBufferWriter(TWriter writer, IFormatProvider? provider, Action<TWriter>? flush, Func<TWriter, CancellationToken, Task>? flushAsync)
        : base(writer, provider, flush, flushAsync)
    {
        writeImpl = writer is IConsumer<ReadOnlySpan<char>> ?
            &DirectWrite :
            &BuffersExtensions.Write<char>;

        static void DirectWrite(TWriter output, ReadOnlySpan<char> input)
        {
            Debug.Assert(output is IConsumer<ReadOnlySpan<char>>);
            Unsafe.As<IConsumer<ReadOnlySpan<char>>>(output).Invoke(input);
        }
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(ReadOnlySpan<char> buffer) => writeImpl(writer, buffer);

    protected override void WriteValue<TFormattable>(TFormattable formattable)
        => writer.Format(formattable, provider: FormatProvider);

    public override string ToString()
        => writer is ArrayBufferWriter<char> buffer ? buffer.WrittenSpan.ToString() : writer.ToString() ?? string.Empty;
}