using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext.Buffers.Binary;

public static partial class BinaryTransformations
{
    [RequiresPreviewFeatures]
    private interface IBitwiseBinaryTransformation
    {
        public static abstract Vector<byte> Invoke(Vector<byte> x, Vector<byte> y);

        public static abstract nuint Invoke(nuint x, nuint y);
    }

    [RequiresPreviewFeatures]
    private static unsafe void BitwiseTransformation<TTransformation>([In] ref byte x, ref byte y, int length)
        where TTransformation : struct, IBitwiseBinaryTransformation
    {
        // iterate by Vector
        if (Vector.IsHardwareAccelerated)
        {
            for (; length >= Vector<byte>.Count; length -= Vector<byte>.Count)
            {
                Unsafe.WriteUnaligned<Vector<byte>>(
                    ref y,
                    TTransformation.Invoke(Unsafe.ReadUnaligned<Vector<byte>>(ref x), Unsafe.ReadUnaligned<Vector<byte>>(ref y)));

                x = ref Unsafe.Add(ref x, Vector<byte>.Count);
                y = ref Unsafe.Add(ref y, Vector<byte>.Count);
            }
        }

        // iterate by nuint.Size
        for (; length >= UIntPtr.Size; length -= UIntPtr.Size)
        {
            Unsafe.WriteUnaligned<nuint>(
                ref y,
                TTransformation.Invoke(Unsafe.ReadUnaligned<nuint>(ref x), Unsafe.ReadUnaligned<nuint>(ref y)));

            x = ref Unsafe.Add(ref x, UIntPtr.Size);
            y = ref Unsafe.Add(ref y, UIntPtr.Size);
        }

        // iterate by byte
        for (; length > 0; length -= 1)
        {
            y = (byte)TTransformation.Invoke(x, y);
            x = ref Unsafe.Add(ref x, 1);
            y = ref Unsafe.Add(ref y, 1);
        }
    }

    [RequiresPreviewFeatures]
    private static unsafe void BitwiseTransformation<T, TTransformation>(ReadOnlySpan<T> x, Span<T> y)
        where T : unmanaged
        where TTransformation : struct, IBitwiseBinaryTransformation
    {
        for (int maxLength = Array.MaxLength / sizeof(T), count; !x.IsEmpty; x = x.Slice(count), y = y.Slice(count))
        {
            count = Math.Min(maxLength, x.Length);

            BitwiseTransformation<TTransformation>(
                ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(x))),
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(y)),
                count);
        }
    }

#pragma warning disable CA2252  // TODO: Remove in .NET 7

    /// <summary>
    /// Performs bitwise AND operation between two vectors in-place.
    /// </summary>
    /// <typeparam name="T">The type of elements in vector.</typeparam>
    /// <param name="x">The first vector.</param>
    /// <param name="y">The second vector. It will be replaced with a result of operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="x"/> is not equal to <paramref name="y"/>.</exception>
    public static void BitwiseAnd<T>(this ReadOnlySpan<T> x, Span<T> y)
        where T : unmanaged
    {
        if (x.Length != y.Length)
            throw new ArgumentOutOfRangeException(nameof(x));

        BitwiseTransformation<T, BitwiseAndTransformation>(x, y);
    }

    /// <summary>
    /// Performs bitwise OR operation between two vectors in-place.
    /// </summary>
    /// <typeparam name="T">The type of elements in vector.</typeparam>
    /// <param name="x">The first vector.</param>
    /// <param name="y">The second vector. It will be replaced with a result of operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="x"/> is not equal to <paramref name="y"/>.</exception>
    public static void BitwiseOr<T>(this ReadOnlySpan<T> x, Span<T> y)
        where T : unmanaged
    {
        if (x.Length != y.Length)
            throw new ArgumentOutOfRangeException(nameof(x));

        BitwiseTransformation<T, BitwiseOrTransformation>(x, y);
    }

    /// <summary>
    /// Performs bitwise XOR operation between two vectors in-place.
    /// </summary>
    /// <typeparam name="T">The type of elements in vector.</typeparam>
    /// <param name="x">The first vector.</param>
    /// <param name="y">The second vector. It will be replaced with a result of operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="x"/> is not equal to <paramref name="y"/>.</exception>
    public static void BitwiseXor<T>(this ReadOnlySpan<T> x, Span<T> y)
        where T : unmanaged
    {
        if (x.Length != y.Length)
            throw new ArgumentOutOfRangeException(nameof(x));

        BitwiseTransformation<T, BitwiseXorTransformation>(x, y);
    }
#pragma warning restore CA2252

    [RequiresPreviewFeatures]
    private readonly struct BitwiseAndTransformation : IBitwiseBinaryTransformation
    {
        static Vector<byte> IBitwiseBinaryTransformation.Invoke(Vector<byte> x, Vector<byte> y) => x & y;

        static nuint IBitwiseBinaryTransformation.Invoke(nuint x, nuint y) => x & y;
    }

    [RequiresPreviewFeatures]
    private readonly struct BitwiseOrTransformation : IBitwiseBinaryTransformation
    {
        static Vector<byte> IBitwiseBinaryTransformation.Invoke(Vector<byte> x, Vector<byte> y) => x | y;

        static nuint IBitwiseBinaryTransformation.Invoke(nuint x, nuint y) => x | y;
    }

    [RequiresPreviewFeatures]
    private readonly struct BitwiseXorTransformation : IBitwiseBinaryTransformation
    {
        static Vector<byte> IBitwiseBinaryTransformation.Invoke(Vector<byte> x, Vector<byte> y) => x ^ y;

        static nuint IBitwiseBinaryTransformation.Invoke(nuint x, nuint y) => x ^ y;
    }
}