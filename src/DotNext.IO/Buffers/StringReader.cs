using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Buffers;

using DecodingContext = DotNext.Text.DecodingContext;

[StructLayout(LayoutKind.Auto)]
internal struct StringReader<TBuffer> : IBufferReader<string>, IBufferReader<int>
    where TBuffer : struct, IBuffer<char>
{
    private readonly Decoder decoder;

    // not readonly to avoid defensive copying
    private TBuffer result;
    private int length, resultOffset;

    internal StringReader(in DecodingContext context, TBuffer result)
    {
        decoder = context.GetDecoder();
        length = result.Length;
        this.result = result;
        resultOffset = 0;
    }

    public readonly int RemainingBytes => length;

    string IBufferReader<string>.Complete() => new(Complete());

    readonly int IBufferReader<int>.Complete() => resultOffset;

    internal Span<char> Complete() => result.Span.Slice(0, resultOffset);

    public void Append(scoped ReadOnlySpan<byte> bytes, scoped ref int consumedBytes)
    {
        length -= bytes.Length;
        resultOffset += decoder.GetChars(bytes, result.Span.Slice(resultOffset), length == 0);
    }
}