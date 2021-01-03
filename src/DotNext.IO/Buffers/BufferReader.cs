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
                for (int bytesToConsume; parser.RemainingBytes > 0 && input.TryGet(ref consumed, out var block, false) && !block.IsEmpty; consumed = input.GetPosition(bytesToConsume, consumed))
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
        private static unsafe void ReverseIfNeeded<T>(ref T value, bool littleEndian, delegate*<T, T> reverse)
            where T : unmanaged
        {
            if (littleEndian != BitConverter.IsLittleEndian)
                value = reverse(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ReverseIfNeeded(this ref int value, bool littleEndian)
            => ReverseIfNeeded(ref value, littleEndian, &BinaryPrimitives.ReverseEndianness);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ReverseIfNeeded(this ref long value, bool littleEndian)
            => ReverseIfNeeded(ref value, littleEndian, &BinaryPrimitives.ReverseEndianness);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ReverseIfNeeded(this ref short value, bool littleEndian)
            => ReverseIfNeeded(ref value, littleEndian, &BinaryPrimitives.ReverseEndianness);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ReverseIfNeeded(this ref ulong value, bool littleEndian)
            => ReverseIfNeeded(ref value, littleEndian, &BinaryPrimitives.ReverseEndianness);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ReverseIfNeeded(this ref uint value, bool littleEndian)
            => ReverseIfNeeded(ref value, littleEndian, &BinaryPrimitives.ReverseEndianness);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ReverseIfNeeded(this ref ushort value, bool littleEndian)
            => ReverseIfNeeded(ref value, littleEndian, &BinaryPrimitives.ReverseEndianness);
    }
}