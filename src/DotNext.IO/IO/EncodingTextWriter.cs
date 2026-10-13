using System.Buffers;
using System.Text;

namespace DotNext.IO;

internal sealed class EncodingTextWriter<TWriter> : TextBufferWriter<byte, TWriter>
    where TWriter : class, IBufferWriter<byte>
{
    internal EncodingTextWriter(TWriter writer, Encoding encoding, IFormatProvider? provider, Action<TWriter>? flush, Func<TWriter, CancellationToken, Task>? flushAsync)
        : base(writer, provider, flush, flushAsync)
    {
        Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
    }

    public override Encoding Encoding { get; }

    public override void Write(ReadOnlySpan<char> chars)
        => Encoding.GetBytes(chars, writer);
}