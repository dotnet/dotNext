using System.Buffers;
using System.Runtime.CompilerServices;
using BinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Buffers;

internal static partial class BufferReader
{
    internal static void Append<TResult, TParser>(this ref TParser parser, in ReadOnlySequence<byte> input, ref SequencePosition position)
        where TParser : struct, IBufferReader<TResult>
    {
        for (int bytesToConsume; parser.RemainingBytes > 0 && input.TryGet(ref position, out var block, advance: false) && !block.IsEmpty; position = input.GetPosition(bytesToConsume, position))
        {
            bytesToConsume = Math.Min(block.Length, parser.RemainingBytes);
            parser.Append(block.Span.Slice(0, bytesToConsume), ref bytesToConsume);
        }
    }

    internal static SequencePosition Append<TResult, TParser>(this ref TParser parser, ReadOnlySequence<byte> input)
        where TParser : struct, IBufferReader<TResult>
    {
        var position = input.Start;
        Append<TResult, TParser>(ref parser, in input, ref position);
        return position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReverseIfNeeded(this ref int value, bool littleEndian)
    {
        if (littleEndian != BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReverseIfNeeded(this ref long value, bool littleEndian)
    {
        if (littleEndian != BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReverseIfNeeded(this ref short value, bool littleEndian)
    {
        if (littleEndian != BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReverseIfNeeded(this ref ulong value, bool littleEndian)
    {
        if (littleEndian != BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReverseIfNeeded(this ref uint value, bool littleEndian)
    {
        if (littleEndian != BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReverseIfNeeded(this ref ushort value, bool littleEndian)
    {
        if (littleEndian != BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
    }
}