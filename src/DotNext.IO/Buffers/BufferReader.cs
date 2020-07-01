using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using BinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Buffers
{
    internal static class BufferReader
    {
        // TODO: Should be replaced with function pointer in C# 9
        internal static readonly ValueReader<long, NumberStyles> Int64Parser = long.Parse;
        internal static readonly ValueReader<ulong, NumberStyles> UInt64Parser = ulong.Parse;
        internal static readonly ValueReader<int, NumberStyles> Int32Parser = int.Parse;
        internal static readonly ValueReader<uint, NumberStyles> UInt32Parser = uint.Parse;
        internal static readonly ValueReader<short, NumberStyles> Int16Parser = short.Parse;
        internal static readonly ValueReader<ushort, NumberStyles> UInt16Parser = ushort.Parse;
        internal static readonly ValueReader<byte, NumberStyles> UInt8Parser = byte.Parse;
        internal static readonly ValueReader<sbyte, NumberStyles> Int8Parser = sbyte.Parse;
        internal static readonly ValueReader<float, NumberStyles> Float32Parser = float.Parse;
        internal static readonly ValueReader<double, NumberStyles> Float64Parser = double.Parse;
        internal static readonly ValueReader<decimal, NumberStyles> DecimalParser = decimal.Parse;
        internal static readonly ValueReader<DateTime, DateTimeStyles> DateTimeParser = ParseDateTime;
        internal static readonly ValueReader<DateTimeOffset, DateTimeStyles> DateTimeOffsetParser = ParseDateTimeOffset;

        private static DateTime ParseDateTime(ReadOnlySpan<char> value, DateTimeStyles style, IFormatProvider? provider)
            => DateTime.Parse(value, provider, style);

        private static DateTime ParseDateTime(this string[] formats, ReadOnlySpan<char> value, DateTimeStyles style, IFormatProvider? provider)
            => DateTime.ParseExact(value, formats, provider, style);

        internal static ValueReader<DateTime, DateTimeStyles> CreateDateTimeParser(string[] formats)
            => formats.ParseDateTime;

        private static DateTimeOffset ParseDateTimeOffset(ReadOnlySpan<char> value, DateTimeStyles style, IFormatProvider? provider)
            => DateTimeOffset.Parse(value, provider, style);

        internal static ValueReader<DateTimeOffset, DateTimeStyles> CreateDateTimeOffsetParser(string[] formats)
            => formats.ParseDateTimeOffset;

        private static DateTimeOffset ParseDateTimeOffset(this string[] formats, ReadOnlySpan<char> value, DateTimeStyles style, IFormatProvider? provider)
            => DateTimeOffset.ParseExact(value, formats, provider, style);

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