using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Versioning;

namespace DotNext.Buffers.Binary;

using Intrinsics = Runtime.Intrinsics;

public static partial class BinaryTransformations
{
    [RequiresPreviewFeatures]
    private interface IEndiannessTransformation<T> : ITransformation<T>
        where T : unmanaged
    {
        public static abstract T ReverseEndianness(T value);

        Vector128<byte> ReorderMask { get; }
    }

    [RequiresPreviewFeatures]
    private static unsafe void ReverseEndianness<T, TTransformation>(Span<T> buffer, TTransformation transformation)
        where T : unmanaged
        where TTransformation : struct, IEndiannessTransformation<T>
    {
        if (Ssse3.IsSupported)
        {
            if (Avx2.IsSupported)
            {
                for (var reorderMask256 = Vector256.Create(transformation.ReorderMask, transformation.ReorderMask); buffer.Length >= Vector256<T>.Count; buffer = buffer.Slice(Vector256<T>.Count))
                {
                    TTransformation.StoreAsVector256(
                        buffer,
                        Avx2.Shuffle(TTransformation.LoadAsVector256(buffer).AsByte(), reorderMask256).As<byte, T>());
                }
            }

            for (; buffer.Length >= Vector128<T>.Count; buffer = buffer.Slice(Vector128<T>.Count))
            {
                TTransformation.StoreAsVector128(
                        buffer,
                        Ssse3.Shuffle(TTransformation.LoadAsVector128(buffer).AsByte(), transformation.ReorderMask).As<byte, T>());
            }
        }

        // software fallback
        foreach (ref var item in buffer)
            item = TTransformation.ReverseEndianness(item);
    }

    [RequiresPreviewFeatures]
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
                item = TTransformation.ReverseEndianness(item);
                break;
            default:
                ReverseEndianness(buffer, new TTransformation());
                break;
        }
    }

#pragma warning disable CA2252  // TODO: Remove in .NET 7

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
#pragma warning restore CA2252

    [RequiresPreviewFeatures]
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

        static ushort IEndiannessTransformation<ushort>.ReverseEndianness(ushort value) => BinaryPrimitives.ReverseEndianness(value);

        static unsafe Vector128<ushort> ITransformation<ushort>.LoadAsVector128(ReadOnlySpan<ushort> buffer)
        {
            fixed (ushort* ptr = buffer)
            {
                return Ssse3.LoadVector128(ptr);
            }
        }

        static unsafe void ITransformation<ushort>.StoreAsVector128(Span<ushort> buffer, Vector128<ushort> items)
        {
            fixed (ushort* ptr = buffer)
            {
                Ssse3.Store(ptr, items);
            }
        }

        static unsafe Vector256<ushort> ITransformation<ushort>.LoadAsVector256(ReadOnlySpan<ushort> buffer)
        {
            fixed (ushort* ptr = buffer)
            {
                return Avx2.LoadVector256(ptr);
            }
        }

        static unsafe void ITransformation<ushort>.StoreAsVector256(Span<ushort> buffer, Vector256<ushort> items)
        {
            fixed (ushort* ptr = buffer)
            {
                Avx2.Store(ptr, items);
            }
        }
    }

    [RequiresPreviewFeatures]
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

        static uint IEndiannessTransformation<uint>.ReverseEndianness(uint value) => BinaryPrimitives.ReverseEndianness(value);

        static unsafe Vector128<uint> ITransformation<uint>.LoadAsVector128(ReadOnlySpan<uint> buffer)
        {
            fixed (uint* ptr = buffer)
            {
                return Ssse3.LoadVector128(ptr);
            }
        }

        static unsafe void ITransformation<uint>.StoreAsVector128(Span<uint> buffer, Vector128<uint> items)
        {
            fixed (uint* ptr = buffer)
            {
                Ssse3.Store(ptr, items);
            }
        }

        static unsafe Vector256<uint> ITransformation<uint>.LoadAsVector256(ReadOnlySpan<uint> buffer)
        {
            fixed (uint* ptr = buffer)
            {
                return Avx2.LoadVector256(ptr);
            }
        }

        static unsafe void ITransformation<uint>.StoreAsVector256(Span<uint> buffer, Vector256<uint> items)
        {
            fixed (uint* ptr = buffer)
            {
                Avx2.Store(ptr, items);
            }
        }
    }

    [RequiresPreviewFeatures]
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

        static ulong IEndiannessTransformation<ulong>.ReverseEndianness(ulong value) => BinaryPrimitives.ReverseEndianness(value);

        static unsafe Vector128<ulong> ITransformation<ulong>.LoadAsVector128(ReadOnlySpan<ulong> buffer)
        {
            fixed (ulong* ptr = buffer)
            {
                return Ssse3.LoadVector128(ptr);
            }
        }

        static unsafe void ITransformation<ulong>.StoreAsVector128(Span<ulong> buffer, Vector128<ulong> items)
        {
            fixed (ulong* ptr = buffer)
            {
                Ssse3.Store(ptr, items);
            }
        }

        static unsafe Vector256<ulong> ITransformation<ulong>.LoadAsVector256(ReadOnlySpan<ulong> buffer)
        {
            fixed (ulong* ptr = buffer)
            {
                return Avx2.LoadVector256(ptr);
            }
        }

        static unsafe void ITransformation<ulong>.StoreAsVector256(Span<ulong> buffer, Vector256<ulong> items)
        {
            fixed (ulong* ptr = buffer)
            {
                Avx2.Store(ptr, items);
            }
        }
    }
}