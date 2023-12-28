using System.Buffers;

namespace DotNext.Buffers;

internal static class BufferReader
{
    internal static SequencePosition Append<TParser>(this ref TParser parser, ReadOnlySequence<byte> input)
        where TParser : struct, IBufferReader
    {
        var position = input.Start;
        for (int consumedBytes, remainingBytes; (remainingBytes = parser.RemainingBytes) > 0; position = input.GetPosition(consumedBytes, position))
        {
            var block = input.NextBlock(ref position).TrimLength(remainingBytes);
            parser.Invoke(block);
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

    private static ReadOnlySpan<byte> NextBlock(this in ReadOnlySequence<byte> sequence, ref SequencePosition position)
        => sequence.TryGet(ref position, out var block, advance: false) && !block.IsEmpty ? block.Span : throw new EndOfStreamException();
}