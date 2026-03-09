using System.Buffers;

namespace DotNext.Buffers;

internal static class BufferReader
{
    extension<TParser>(ref TParser parser) where TParser : struct, IBufferReader, allows ref struct
    {
        internal SequencePosition Append(in ReadOnlySequence<byte> input)
        {
            var position = input.Start;
            for (int consumedBytes, remainingBytes;
                 (remainingBytes = parser.RemainingBytes) > 0
                 && input.TryGet(ref position, out var block, advance: false) && !block.IsEmpty;
                 position = input.GetPosition(consumedBytes, position))
            {
                block %= remainingBytes;
                parser.Invoke(block.Span);
                consumedBytes = block.Length;
            }

            return position;
        }

        internal void EndOfStream()
        {
            if (TParser.ThrowOnPartialData && parser.RemainingBytes > 0)
                throw new EndOfStreamException();
        }
    }

    internal static TResult EndOfStream<TResult, TParser>(this ref TParser parser)
        where TParser : struct, IBufferReader, ISupplier<TResult>, allows ref struct
        => parser.RemainingBytes is 0 || !TParser.ThrowOnPartialData ? parser.Invoke() : throw new EndOfStreamException();
}