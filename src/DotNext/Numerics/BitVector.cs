using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Numerics;

/// <summary>
/// Allows to convert bit vectors to scalar values.
/// </summary>
public static class BitVector
{
    // TODO: Rewrite using generic math
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

    [StructLayout(LayoutKind.Auto)]
    private struct UIntPtrVector : IBitVector<nuint>
    {
        private const nuint One = 1;
        private nuint result;

        bool IBitVector<nuint>.this[int position]
        {
            readonly get => ((result >> position) & 1UL) != 0UL;
            set => result = (result & ~(One << position)) | ((nuint)value.ToInt32() << position);
        }

        nuint IBitVector<nuint>.Value
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

    /// <summary>
    /// Converts bit vector to platform-dependent signed integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>Platform-dependent signed integer reconstructed from the bits.</returns>
    public static nint ToInt(ReadOnlySpan<bool> bits)
        => (nint)VectorToScalar<nuint, UIntPtrVector>(bits.TrimLength(IntPtr.Size << 3));

    /// <summary>
    /// Converts bit vector to platform-dependent unsigned integer.
    /// </summary>
    /// <param name="bits">A sequence of bits.</param>
    /// <returns>Platform-dependent unsigned integer reconstructed from the bits.</returns>
    [CLSCompliant(false)]
    public static nuint ToUInt(ReadOnlySpan<bool> bits)
        => VectorToScalar<nuint, UIntPtrVector>(bits.TrimLength(UIntPtr.Size << 3));

    private static void GetBits<TValue, TVector>(TValue value, Span<bool> bits)
        where TValue : unmanaged
        where TVector : struct, IBitVector<TValue>
    {
        var vector = new TVector { Value = value };

        for (var position = 0; position < bits.Length; position++)
            bits[position] = vector[position];
    }

    private static void Get8Bits(byte input, ref bool output)
    {
        Debug.Assert(Sse2.IsSupported);

        var onevec = Vector128.Create((byte)1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var bitmask = Vector128.Create(
            0B_0000_0001,
            0B_0000_0010,
            0B_0000_0100,
            0B_0000_1000,
            0B_0001_0000,
            0B_0010_0000,
            0B_0100_0000,
            0B_1000_0000,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);

        var result = Sse2.Min(Sse2.And(Vector128.Create(input), bitmask), onevec);
        Unsafe.WriteUnaligned(ref Unsafe.As<bool, byte>(ref output), result.GetLower());
    }

    private static void Get16Bits(ushort input, ref bool output)
    {
        Debug.Assert(Avx2.IsSupported);

        var onevec = Vector256.Create((ushort)1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var bitmask = Vector256.Create(
            (ushort)0B_0000_0000_0000_0001,
            0B_0000_0000_0000_0010,
            0B_0000_0000_0000_0100,
            0B_0000_0000_0000_1000,
            0B_0000_0000_0001_0000,
            0B_0000_0000_0010_0000,
            0B_0000_0000_0100_0000,
            0B_0000_0000_1000_0000,
            0B_0000_0001_0000_0000,
            0B_0000_0010_0000_0000,
            0B_0000_0100_0000_0000,
            0B_0000_1000_0000_0000,
            0B_0001_0000_0000_0000,
            0B_0010_0000_0000_0000,
            0B_0100_0000_0000_0000,
            0B_1000_0000_0000_0000);

        // normalize first 8 bits for each 128-bit subvector
        var shuffleMask = Vector256.Create(
            0,
            2,
            4,
            6,
            8,
            10,
            12,
            14,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            0,
            2,
            4,
            6,
            8,
            10,
            12,
            14,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue,
            byte.MaxValue);

        var result = Avx2.Shuffle(
            Avx2.Min(Avx2.And(Vector256.Create(input), bitmask), onevec).AsByte(),
            shuffleMask);

        Unsafe.WriteUnaligned(ref Unsafe.As<bool, byte>(ref output), result.GetLower().GetLower());
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref Unsafe.As<bool, byte>(ref output), 8), result.GetUpper().GetLower());
    }

    /// <summary>
    /// Extracts bits from 8-bit unsigned integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    public static void FromByte(byte value, Span<bool> bits)
    {
        bits = bits.TrimLength(8);

        if (Sse2.IsSupported)
        {
            Get8Bits(value, ref MemoryMarshal.GetReference(bits));
        }
        else
        {
            GetBits<uint, UInt32Vector>(value, bits);
        }
    }

    /// <summary>
    /// Extracts bits from 8-bit signed integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    [CLSCompliant(false)]
    public static void FromSByte(sbyte value, Span<bool> bits)
        => FromByte((byte)value, bits);

    /// <summary>
    /// Extracts bits from 16-bit signed integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    public static void FromInt16(short value, Span<bool> bits)
        => FromUInt16((ushort)value, bits);

    /// <summary>
    /// Extracts bits from 16-bit unsigned integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    [CLSCompliant(false)]
    public static void FromUInt16(ushort value, Span<bool> bits)
    {
        bits = bits.TrimLength(16);

        if (Avx2.IsSupported)
        {
            Get16Bits(value, ref MemoryMarshal.GetReference(bits));
        }
        else if (Sse2.IsSupported)
        {
            ref var output = ref MemoryMarshal.GetReference(bits);
            ref var input = ref Unsafe.As<ushort, byte>(ref value);
            Get8Bits(input, ref output);
            Get8Bits(Unsafe.Add(ref input, 1), ref Unsafe.Add(ref output, 8));
        }
        else
        {
            GetBits<uint, UInt32Vector>(value, bits);
        }
    }

    /// <summary>
    /// Extracts bits from 32-bit signed integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    public static void FromInt32(int value, Span<bool> bits)
        => FromUInt32((uint)value, bits);

    /// <summary>
    /// Extracts bits from 32-bit unsigned integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    [CLSCompliant(false)]
    public static void FromUInt32(uint value, Span<bool> bits)
    {
        bits = bits.TrimLength(32);

        if (Avx2.IsSupported)
        {
            ref var output = ref MemoryMarshal.GetReference(bits);
            ref var input = ref Unsafe.As<uint, ushort>(ref value);
            Get16Bits(input, ref output);
            Get16Bits(Unsafe.Add(ref input, 1), ref Unsafe.Add(ref output, 16));
        }
        else if (Sse2.IsSupported)
        {
            ref var output = ref MemoryMarshal.GetReference(bits);
            ref var input = ref Unsafe.As<uint, byte>(ref value);
            Get8Bits(input, ref output);
            Get8Bits(Unsafe.Add(ref input, 1), ref Unsafe.Add(ref output, 8));
            Get8Bits(Unsafe.Add(ref input, 2), ref Unsafe.Add(ref output, 16));
            Get8Bits(Unsafe.Add(ref input, 3), ref Unsafe.Add(ref output, 24));
        }
        else
        {
            GetBits<uint, UInt32Vector>(value, bits);
        }
    }

    /// <summary>
    /// Extracts bits from 64-bit signed integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    public static void FromInt64(long value, Span<bool> bits)
        => FromUInt64((ulong)value, bits);

    /// <summary>
    /// Extracts bits from 64-bit unsigned integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    [CLSCompliant(false)]
    public static void FromUInt64(ulong value, Span<bool> bits)
    {
        bits = bits.TrimLength(64);

        if (Avx2.IsSupported)
        {
            ref var output = ref MemoryMarshal.GetReference(bits);
            ref var input = ref Unsafe.As<ulong, ushort>(ref value);

            Get16Bits(input, ref output);
            Get16Bits(Unsafe.Add(ref input, 1), ref Unsafe.Add(ref output, 16));
            Get16Bits(Unsafe.Add(ref input, 2), ref Unsafe.Add(ref output, 32));
            Get16Bits(Unsafe.Add(ref input, 3), ref Unsafe.Add(ref output, 48));
        }
        else if (Sse2.IsSupported)
        {
            ref var output = ref MemoryMarshal.GetReference(bits);
            ref var input = ref Unsafe.As<ulong, byte>(ref value);
            Get8Bits(input, ref output);
            Get8Bits(Unsafe.Add(ref input, 1), ref Unsafe.Add(ref output, 8));
            Get8Bits(Unsafe.Add(ref input, 2), ref Unsafe.Add(ref output, 16));
            Get8Bits(Unsafe.Add(ref input, 3), ref Unsafe.Add(ref output, 24));
            Get8Bits(Unsafe.Add(ref input, 4), ref Unsafe.Add(ref output, 32));
            Get8Bits(Unsafe.Add(ref input, 5), ref Unsafe.Add(ref output, 40));
            Get8Bits(Unsafe.Add(ref input, 6), ref Unsafe.Add(ref output, 48));
            Get8Bits(Unsafe.Add(ref input, 7), ref Unsafe.Add(ref output, 56));
        }
        else
        {
            GetBits<ulong, UInt64Vector>(value, bits);
        }
    }

    /// <summary>
    /// Extracts bits from platform-dependent signed integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    public static void FromInt(nint value, Span<bool> bits)
    {
        switch (IntPtr.Size)
        {
            case sizeof(int):
                FromInt32((int)value, bits);
                break;
            case sizeof(long):
                FromInt64((long)value, bits);
                break;
            default:
                GetBits<nuint, UIntPtrVector>((nuint)value, bits.TrimLength(IntPtr.Size << 3));
                break;
        }
    }

    /// <summary>
    /// Extracts bits from platform-dependent signed integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="bits">The buffer for extracted bits.</param>
    [CLSCompliant(false)]
    public static void FromUInt(nuint value, Span<bool> bits)
    {
        switch (UIntPtr.Size)
        {
            case sizeof(uint):
                FromUInt32((uint)value, bits);
                break;
            case sizeof(ulong):
                FromUInt64((ulong)value, bits);
                break;
            default:
                GetBits<nuint, UIntPtrVector>(value, bits.TrimLength(UIntPtr.Size << 3));
                break;
        }
    }
}