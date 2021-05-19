using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using BinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Buffers
{
    internal static partial class BufferReader
    {
        private static void Append<TResult, TParser>(ref TParser parser, ref SequenceReader<byte> reader)
            where TParser : struct, IBufferReader<TResult>
        {
            while (parser.RemainingBytes > 0 && reader.Remaining > 0L)
            {
                var block = reader.UnreadSpan;
                var bytesToConsume = Math.Min(block.Length, parser.RemainingBytes);
                parser.Append(block.Slice(0, bytesToConsume), ref bytesToConsume);
                reader.Advance(bytesToConsume);
            }
        }

        internal static void Append<TResult, TParser>(this ref TParser parser, ReadOnlySequence<byte> input, out SequencePosition consumed)
            where TParser : struct, IBufferReader<TResult>
        {
            var reader = new SequenceReader<byte>(input);
            try
            {
                Append<TResult, TParser>(ref parser, ref reader);
            }
            finally
            {
                consumed = reader.Position;
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