using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using BinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Buffers
{
    internal static partial class BufferReader
    {
        internal static void Append<TResult, TParser>(this ref TParser parser, in ReadOnlySequence<byte> input, out SequencePosition consumed)
            where TParser : struct, IBufferReader<TResult>
        {
            consumed = input.Start;
            if (input.Length > 0)
            {
                for (int bytesToConsume; parser.RemainingBytes > 0 && input.TryGet(ref consumed, out var block, false) && block.Length > 0; consumed = input.GetPosition(bytesToConsume, consumed))
                {
                    bytesToConsume = Math.Min(block.Length, parser.RemainingBytes);
                    block = block.Slice(0, bytesToConsume);
                    parser.Append(block.Span, ref bytesToConsume);
                }
            }
            else
            {
                parser.EndOfStream();
            }
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
}