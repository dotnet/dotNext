using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Buffers
{
    using DecodingContext = Text.DecodingContext;

    [StructLayout(LayoutKind.Auto)]
    internal struct StringReader<TBuffer> : IBufferReader<string>
        where TBuffer : struct, IBuffer<char>
    {
        private readonly Decoder decoder;
        private readonly Encoding encoding;
        private readonly TBuffer result;
        private int length, resultOffset;

        internal StringReader(in DecodingContext context, in TBuffer result)
        {
            decoder = context.GetDecoder();
            encoding = context.Encoding;
            length = result.Length;
            this.result = result;
            resultOffset = 0;
        }

        internal readonly bool IsCompleted => length == 0;

        internal readonly Span<char> Result => result.Span.Slice(0, resultOffset);

        readonly int IBufferReader<string>.RemainingBytes => length;

        readonly string IBufferReader<string>.Complete() => new string(Result);

        void IBufferReader<string>.Append(ReadOnlySpan<byte> bytes, ref int consumedBytes)
        {
            length -= bytes.Length;
            resultOffset += decoder.GetChars(bytes, result.Span.Slice(resultOffset), length == 0);
        }
    }
}