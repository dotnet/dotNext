using System.Buffers;

namespace DotNext.Buffers;

internal static class BufferReader
{
    internal static SequencePosition Append<TParser>(this ref TParser parser, in ReadOnlySequence<byte> input)
        where TParser : struct, IBufferReader
    {
        var position = input.Start;
        for (int consumedBytes, remainingBytes; (remainingBytes = parser.RemainingBytes) > 0 && input.TryGet(ref position, out var block, advance: false) && !block.IsEmpty; position = input.GetPosition(consumedBytes, position))
        {
            block = block.TrimLength(remainingBytes);
            parser.Invoke(block.Span);
            consumedBytes = block.Length;
        }

        return position;
    }

    internal static void EndOfStream<TParser>(this ref TParser parser)
        where TParser : struct, IBufferReader
    {
        if (TParser.ThrowOnPartialData && parser.RemainingBytes > 0)
            throw new EndOfStreamException();
    }

    internal static TResult EndOfStream<TResult, TParser>(this ref TParser parser)
        where TParser : struct, IBufferReader, ISupplier<TResult>
        => parser.RemainingBytes is 0 || !TParser.ThrowOnPartialData ? parser.Invoke() : throw new EndOfStreamException();
}