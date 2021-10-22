using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Numerics;

/// <summary>
/// Allows to convert bit vectors to scalar values.
/// </summary>
public static class BitVector
{
    private interface IBitVector<TValue>
        where TValue : unmanaged
    {
        bool this[int position] { get; set; }

        TValue Value { get; set; }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct UInt32Vector : IBitVector<uint>
    {
        private uint result;

        bool IBitVector<uint>.this[int position]
        {
            readonly get => ((result >> position) & 1U) != 0U;
            set => result = (result & ~(1U << position)) | ((uint)value.ToInt32() << position);
        }

        uint IBitVector<uint>.Value
        {
            readonly get => result;
            set => result = value;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct UInt64Vector : IBitVector<ulong>
    {
        private ulong result;

        bool IBitVector<ulong>.this[int position]
        {
            readonly get => ((result >> position) & 1UL) != 0UL;
            set => result = (result & ~(1UL << position)) | ((ulong)value.ToInt32() << position);
        }

        ulong IBitVector<ulong>.Value
        {
            readonly get => result;
            set => result = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TValue VectorToScalar<TValue, TVector>(ReadOnlySpan<bool> bits)
        where TValue : unmanaged
        where TVector : struct, IBitVector<TValue>
    {
        var result = new TVector();

        for (var position = 0; position < bits.Length; position++)
            result[position] = bits[position];

        return result.Value;
    }

    /// <summary>
    /// Converts bit vector to 8-bit unsigned integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>8-bit unsigned integer reconstructed from the bits.</returns>
    public static byte ToByte(ReadOnlySpan<bool> bits)
        => (byte)VectorToScalar<uint, UInt32Vector>(bits.TrimLength(8));

    /// <summary>
    /// Converts bit vector to 8-bit signed integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>8-bit unsigned integer reconstructed from the bits.</returns>
    [CLSCompliant(false)]
    public static sbyte ToSByte(ReadOnlySpan<bool> bits)
        => (sbyte)VectorToScalar<uint, UInt32Vector>(bits.TrimLength(8));

    /// <summary>
    /// Converts bit vector to 16-bit signed integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>16-bit signed integer reconstructed from the bits.</returns>
    public static short ToInt16(ReadOnlySpan<bool> bits)
        => (short)VectorToScalar<uint, UInt32Vector>(bits.TrimLength(16));

    /// <summary>
    /// Converts bit vector to 16-bit unsigned integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>16-bit unsigned integer reconstructed from the bits.</returns>
    [CLSCompliant(false)]
    public static ushort ToUInt16(ReadOnlySpan<bool> bits)
        => (ushort)VectorToScalar<uint, UInt32Vector>(bits.TrimLength(16));

    /// <summary>
    /// Converts bit vector to 32-bit signed integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>32-bit signed integer reconstructed from the bits.</returns>
    public static int ToInt32(ReadOnlySpan<bool> bits)
        => (int)VectorToScalar<uint, UInt32Vector>(bits.TrimLength(32));

    /// <summary>
    /// Converts bit vector to 32-bit unsigned integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>32-bit unsigned integer reconstructed from the bits.</returns>
    [CLSCompliant(false)]
    public static uint ToUInt32(ReadOnlySpan<bool> bits)
        => VectorToScalar<uint, UInt32Vector>(bits.TrimLength(32));

    /// <summary>
    /// Converts bit vector to 64-bit signed integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>64-bit signed integer reconstructed from the bits.</returns>
    public static long ToInt64(ReadOnlySpan<bool> bits)
        => (long)VectorToScalar<ulong, UInt64Vector>(bits.TrimLength(64));

    /// <summary>
    /// Converts bit vector to 64-bit unsigned integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>64-bit unsigned integer reconstructed from the bits.</returns>
    [CLSCompliant(false)]
    public static ulong ToUInt64(ReadOnlySpan<bool> bits)
        => VectorToScalar<ulong, UInt64Vector>(bits.TrimLength(64));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GetBits<TValue, TVector>(TValue value, Span<bool> bits)
        where TValue : unmanaged
        where TVector : struct, IBitVector<TValue>
    {
        var vector = new TVector { Value = value };

        for (var position = 0; position < bits.Length; position++)
            bits[position] = vector[position];
    }

    /// <summary>
    /// Extracts bits from 8-bit unsigned integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    public static void FromByte(byte value, Span<bool> bits)
        => GetBits<uint, UInt32Vector>(value, bits.TrimLength(8));

    /// <summary>
    /// Extracts bits from 8-bit signed integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    [CLSCompliant(false)]
    public static void FromSByte(sbyte value, Span<bool> bits)
        => GetBits<uint, UInt32Vector>((byte)value, bits.TrimLength(8));

    /// <summary>
    /// Extracts bits from 16-bit signed integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    public static void FromInt16(short value, Span<bool> bits)
        => GetBits<uint, UInt32Vector>((ushort)value, bits.TrimLength(16));

    /// <summary>
    /// Extracts bits from 16-bit unsigned integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    [CLSCompliant(false)]
    public static void FromUInt16(ushort value, Span<bool> bits)
        => GetBits<uint, UInt32Vector>(value, bits.TrimLength(16));

    /// <summary>
    /// Extracts bits from 32-bit signed integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    public static void FromInt32(int value, Span<bool> bits)
        => GetBits<uint, UInt32Vector>((uint)value, bits.TrimLength(32));

    /// <summary>
    /// Extracts bits from 32-bit unsigned integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    [CLSCompliant(false)]
    public static void FromUInt32(uint value, Span<bool> bits)
        => GetBits<uint, UInt32Vector>(value, bits.TrimLength(32));

    /// <summary>
    /// Extracts bits from 64-bit signed integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    public static void FromInt64(long value, Span<bool> bits)
        => GetBits<ulong, UInt64Vector>((ulong)value, bits.TrimLength(64));

    /// <summary>
    /// Extracts bits from 64-bit unsigned integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    [CLSCompliant(false)]
    public static void FromUInt64(ulong value, Span<bool> bits)
        => GetBits<ulong, UInt64Vector>(value, bits.TrimLength(64));
}