using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace DotNext.Buffers.Binary;

using Intrinsics = Runtime.Intrinsics;

public static partial class BinaryTransformations
{
    private interface IEndiannessTransformation<T> : IUnaryTransformation<T>
        where T : unmanaged
    {
        Vector128<byte> ReorderMask { get; }
    }

    private static void ReverseEndianness<T, TTransformation>(Span<T> buffer, TTransformation transformation)
        where T : unmanaged
        where TTransformation : struct, IEndiannessTransformation<T>
    {
        if (Ssse3.IsSupported)
        {
            if (Avx2.IsSupported)
            {
                for (var reorderMask256 = Vector256.Create(transformation.ReorderMask, transformation.ReorderMask); buffer.Length >= Vector256<T>.Count; buffer = buffer.Slice(Vector256<T>.Count))
                {
                    var vector = LoadVector256<T>(buffer);
                    vector = Avx2.Shuffle(vector.AsByte(), reorderMask256).As<byte, T>();
                    StoreVector256(vector, buffer);
                }
            }

            for (Vector128<T> vector; buffer.Length >= Vector128<T>.Count; buffer = buffer.Slice(Vector128<T>.Count))
            {
                vector = LoadVector128<T>(buffer);
                vector = Ssse3.Shuffle(vector.AsByte(), transformation.ReorderMask).As<byte, T>();
                StoreVector128(vector, buffer);
            }
        }

        // software fallback
        foreach (ref var item in buffer)
            item = TTransformation.Transform(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReverseEndianness<T, TTransformation>(Span<T> buffer)
        where T : unmanaged
        where TTransformation : struct, IEndiannessTransformation<T>
    {
        switch (buffer.Length)
        {
            case 0:
                break;
            case 1:
                ref var item = ref buffer[0];
                item = TTransformation.Transform(item);
                break;
            default:
                ReverseEndianness(buffer, new TTransformation());
                break;
        }
    }

    /// <summary>
    /// Reverse endianness of 16-bit signed integers in-place.
    /// </summary>
    /// <param name="buffer">The buffer to modify.</param>
    public static void ReverseEndianness(this Span<short> buffer)
        => ReverseEndianness<ushort, UInt16Transformation>(Intrinsics.ReinterpretCast<short, ushort>(buffer));

    /// <summary>
    /// Reverse endianness of 16-bit unsigned integers in-place.
    /// </summary>
    /// <param name="buffer">The buffer to modify.</param>
    [CLSCompliant(false)]
    public static void ReverseEndianness(this Span<ushort> buffer)
        => ReverseEndianness<ushort, UInt16Transformation>(buffer);

    /// <summary>
    /// Reverse endianness of 32-bit signed integers in-place.
    /// </summary>
    /// <param name="buffer">The buffer to modify.</param>
    public static void ReverseEndianness(this Span<int> buffer)
        => ReverseEndianness<uint, UInt32Transformation>(Intrinsics.ReinterpretCast<int, uint>(buffer));

    /// <summary>
    /// Reverse endianness of 32-bit unsigned integers in-place.
    /// </summary>
    /// <param name="buffer">The buffer to modify.</param>
    [CLSCompliant(false)]
    public static void ReverseEndianness(this Span<uint> buffer)
        => ReverseEndianness<uint, UInt32Transformation>(buffer);

    /// <summary>
    /// Reverse endianness of 64-bit signed integers in-place.
    /// </summary>
    /// <param name="buffer">The buffer to modify.</param>
    public static void ReverseEndianness(this Span<long> buffer)
        => ReverseEndianness<ulong, UInt64Transformation>(Intrinsics.ReinterpretCast<long, ulong>(buffer));

    /// <summary>
    /// Reverse endianness of 64-bit unsigned integers in-place.
    /// </summary>
    /// <param name="buffer">The buffer to modify.</param>
    [CLSCompliant(false)]
    public static void ReverseEndianness(this Span<ulong> buffer)
        => ReverseEndianness<ulong, UInt64Transformation>(buffer);

    [StructLayout(LayoutKind.Auto)]
    private readonly struct UInt16Transformation : IEndiannessTransformation<ushort>
    {
        public UInt16Transformation() => ReorderMask = Vector128.Create(
                (byte)1,
                0,
                3,
                2,
                5,
                4,
                7,
                6,
                9,
                8,
                11,
                10,
                13,
                12,
                15,
                14);

        public Vector128<byte> ReorderMask { get; }

        static ushort IUnaryTransformation<ushort>.Transform(ushort value) => BinaryPrimitives.ReverseEndianness(value);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct UInt32Transformation : IEndiannessTransformation<uint>
    {
        public UInt32Transformation() => ReorderMask = Vector128.Create(
                (byte)3,
                2,
                1,
                0,
                7,
                6,
                5,
                4,
                11,
                10,
                9,
                8,
                15,
                14,
                13,
                12);

        public Vector128<byte> ReorderMask { get; }

        static uint IUnaryTransformation<uint>.Transform(uint value) => BinaryPrimitives.ReverseEndianness(value);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct UInt64Transformation : IEndiannessTransformation<ulong>
    {
        public UInt64Transformation() => ReorderMask = Vector128.Create(
                (byte)7,
                6,
                5,
                4,
                3,
                2,
                1,
                0,
                15,
                14,
                13,
                12,
                11,
                10,
                9,
                8);

        public Vector128<byte> ReorderMask { get; }

        static ulong IUnaryTransformation<ulong>.Transform(ulong value) => BinaryPrimitives.ReverseEndianness(value);
    }
}