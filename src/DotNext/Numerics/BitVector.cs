using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Numerics;

/// <summary>
/// Allows to convert bit vectors to scalar values.
/// </summary>
public static class BitVector
{
    private interface IValueBuilder<TResult>
        where TResult : unmanaged
    {
        void SetBit(int position, bool bit);

        TResult Result { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct UInt32Builder : IValueBuilder<uint>
    {
        private uint result;

        void IValueBuilder<uint>.SetBit(int position, bool bit) => result |= (uint)bit.ToByte() << position;

        readonly uint IValueBuilder<uint>.Result => result;
    }

    [StructLayout(LayoutKind.Auto)]
    private struct UInt64Builder : IValueBuilder<ulong>
    {
        private ulong result;

        void IValueBuilder<ulong>.SetBit(int position, bool bit) => result |= (ulong)bit.ToByte() << position;

        readonly ulong IValueBuilder<ulong>.Result => result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TResult VectorToScalar<TResult, TBuilder>(ReadOnlySpan<bool> bits)
        where TResult : unmanaged
        where TBuilder : struct, IValueBuilder<TResult>
    {
        var result = new TBuilder();

        for (var position = 0; position < bits.Length; position++)
            result.SetBit(position, bits[position]);

        return result.Result;
    }

    /// <summary>
    /// Converts bit vector to 8-bit unsigned integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>8-bit unsigned integer reconstructed from the bits.</returns>
    public static byte ToByte(ReadOnlySpan<bool> bits)
        => (byte)VectorToScalar<uint, UInt32Builder>(bits.TrimLength(8));

    /// <summary>
    /// Converts bit vector to 16-bit signed integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>16-bit signed integer reconstructed from the bits.</returns>
    public static short ToInt16(ReadOnlySpan<bool> bits)
        => (short)VectorToScalar<uint, UInt32Builder>(bits.TrimLength(16));

    /// <summary>
    /// Converts bit vector to 16-bit unsigned integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>16-bit unsigned integer reconstructed from the bits.</returns>
    [CLSCompliant(false)]
    public static ushort ToUInt16(ReadOnlySpan<bool> bits)
        => (ushort)VectorToScalar<uint, UInt32Builder>(bits.TrimLength(16));

    /// <summary>
    /// Converts bit vector to 32-bit signed integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>32-bit signed integer reconstructed from the bits.</returns>
    public static int ToInt32(ReadOnlySpan<bool> bits)
        => (int)VectorToScalar<uint, UInt32Builder>(bits.TrimLength(32));

    /// <summary>
    /// Converts bit vector to 32-bit unsigned integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>32-bit unsigned integer reconstructed from the bits.</returns>
    [CLSCompliant(false)]
    public static uint ToUInt32(ReadOnlySpan<bool> bits)
        => VectorToScalar<uint, UInt32Builder>(bits.TrimLength(32));

    /// <summary>
    /// Converts bit vector to 64-bit signed integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>64-bit signed integer reconstructed from the bits.</returns>
    public static long ToInt64(ReadOnlySpan<bool> bits)
        => (long)VectorToScalar<ulong, UInt64Builder>(bits.TrimLength(64));

    /// <summary>
    /// Converts bit vector to 64-bit unsigned integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>64-bit unsigned integer reconstructed from the bits.</returns>
    [CLSCompliant(false)]
    public static ulong ToUInt64(ReadOnlySpan<bool> bits)
        => VectorToScalar<ulong, UInt64Builder>(bits.TrimLength(64));
}