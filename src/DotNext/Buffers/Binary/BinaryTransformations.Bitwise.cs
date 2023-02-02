using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext.Buffers.Binary;

public static partial class BinaryTransformations
{
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

        Transform<T, BitwiseAndTransformation>(x, y);
    }

    /// <summary>
    /// Performs bitwise AND-NOT operation between two vectors in-place.
    /// </summary>
    /// <typeparam name="T">The type of elements in vector.</typeparam>
    /// <param name="x">The first vector.</param>
    /// <param name="y">The second vector. It will be replaced with a result of operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="x"/> is not equal to <paramref name="y"/>.</exception>
    public static void AndNot<T>(this ReadOnlySpan<T> x, Span<T> y)
        where T : unmanaged
    {
        if (x.Length != y.Length)
            throw new ArgumentOutOfRangeException(nameof(x));

        Transform<T, BitwiseAndNotTransformation>(x, y);
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

        Transform<T, BitwiseOrTransformation>(x, y);
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

        Transform<T, BitwiseXorTransformation>(x, y);
    }

    /// <summary>
    /// Inverts all bits within the provided vector in-place.
    /// </summary>
    /// <typeparam name="T">The type of elements in vector.</typeparam>
    /// <param name="values">The vector to modify.</param>
    public static void OnesComplement<T>(this Span<T> values)
        where T : unmanaged
        => Transform<T, OnesComplementTransformation>(values);
#pragma warning restore CA2252

    [RequiresPreviewFeatures]
    private static void Transform<TTransformation>(ref byte x, int length)
        where TTransformation : struct, IUnaryTransformation<nuint>, IUnaryTransformation<Vector<byte>>
    {
        // iterate by Vector
        if (Vector.IsHardwareAccelerated)
        {
            for (; length >= Vector<byte>.Count; length -= Vector<byte>.Count)
            {
                Unsafe.WriteUnaligned(
                    ref x,
                    TTransformation.Transform(Unsafe.ReadUnaligned<Vector<byte>>(ref x)));

                x = ref Unsafe.Add(ref x, Vector<byte>.Count);
            }
        }

        // iterate by nuint.Size
        for (; length >= UIntPtr.Size; length -= UIntPtr.Size)
        {
            Unsafe.WriteUnaligned(ref x, TTransformation.Transform(Unsafe.ReadUnaligned<nuint>(ref x)));
            x = ref Unsafe.Add(ref x, UIntPtr.Size);
        }

        // iterate by byte
        for (; length > 0; length -= 1)
        {
            x = (byte)TTransformation.Transform(x);
            x = ref Unsafe.Add(ref x, 1);
        }
    }

    [RequiresPreviewFeatures]
    private static unsafe void Transform<T, TTransformation>(Span<T> values)
        where T : unmanaged
        where TTransformation : struct, IUnaryTransformation<nuint>, IUnaryTransformation<Vector<byte>>
    {
        for (int maxLength = Array.MaxLength / sizeof(T), count; !values.IsEmpty; values = values.Slice(count))
        {
            count = Math.Min(maxLength, values.Length);

            Transform<TTransformation>(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(values)),
                count * sizeof(T));
        }
    }

    [RequiresPreviewFeatures]
    private static void Transform<TTransformation>([In] ref byte x, ref byte y, int length)
        where TTransformation : struct, IBinaryTransformation<nuint>, IBinaryTransformation<Vector<byte>>
    {
        // iterate by Vector
        if (Vector.IsHardwareAccelerated)
        {
            for (; length >= Vector<byte>.Count; length -= Vector<byte>.Count)
            {
                Unsafe.WriteUnaligned(
                    ref y,
                    TTransformation.Transform(Unsafe.ReadUnaligned<Vector<byte>>(ref x), Unsafe.ReadUnaligned<Vector<byte>>(ref y)));

                x = ref Unsafe.Add(ref x, Vector<byte>.Count);
                y = ref Unsafe.Add(ref y, Vector<byte>.Count);
            }
        }

        // iterate by nuint.Size
        for (; length >= UIntPtr.Size; length -= UIntPtr.Size)
        {
            Unsafe.WriteUnaligned(
                ref y,
                TTransformation.Transform(Unsafe.ReadUnaligned<nuint>(ref x), Unsafe.ReadUnaligned<nuint>(ref y)));

            x = ref Unsafe.Add(ref x, UIntPtr.Size);
            y = ref Unsafe.Add(ref y, UIntPtr.Size);
        }

        // iterate by byte
        for (; length > 0; length -= 1)
        {
            y = (byte)TTransformation.Transform(x, y);
            x = ref Unsafe.Add(ref x, 1);
            y = ref Unsafe.Add(ref y, 1);
        }
    }

    [RequiresPreviewFeatures]
    private static unsafe void Transform<T, TTransformation>(ReadOnlySpan<T> x, Span<T> y)
        where T : unmanaged
        where TTransformation : struct, IBinaryTransformation<nuint>, IBinaryTransformation<Vector<byte>>
    {
        for (int maxLength = Array.MaxLength / sizeof(T), count; !x.IsEmpty; x = x.Slice(count), y = y.Slice(count))
        {
            count = Math.Min(maxLength, x.Length);

            Transform<TTransformation>(
                ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(x))),
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(y)),
                count * sizeof(T));
        }
    }

    [RequiresPreviewFeatures]
    private readonly struct BitwiseAndTransformation : IBinaryTransformation<nuint>, IBinaryTransformation<Vector<byte>>
    {
        static Vector<byte> IBinaryTransformation<Vector<byte>>.Transform(Vector<byte> x, Vector<byte> y) => Vector.BitwiseAnd(x, y);

        static nuint IBinaryTransformation<nuint>.Transform(nuint x, nuint y) => x & y;
    }

    [RequiresPreviewFeatures]
    private readonly struct BitwiseOrTransformation : IBinaryTransformation<nuint>, IBinaryTransformation<Vector<byte>>
    {
        static Vector<byte> IBinaryTransformation<Vector<byte>>.Transform(Vector<byte> x, Vector<byte> y) => Vector.BitwiseOr(x, y);

        static nuint IBinaryTransformation<nuint>.Transform(nuint x, nuint y) => x | y;
    }

    [RequiresPreviewFeatures]
    private readonly struct BitwiseXorTransformation : IBinaryTransformation<nuint>, IBinaryTransformation<Vector<byte>>
    {
        static Vector<byte> IBinaryTransformation<Vector<byte>>.Transform(Vector<byte> x, Vector<byte> y) => Vector.Xor(x, y);

        static nuint IBinaryTransformation<nuint>.Transform(nuint x, nuint y) => x ^ y;
    }

    [RequiresPreviewFeatures]
    private readonly struct BitwiseAndNotTransformation : IBinaryTransformation<nuint>, IBinaryTransformation<Vector<byte>>
    {
        static Vector<byte> IBinaryTransformation<Vector<byte>>.Transform(Vector<byte> x, Vector<byte> y) => Vector.AndNot(x, y);

        static nuint IBinaryTransformation<nuint>.Transform(nuint x, nuint y) => x & ~y;
    }

    [RequiresPreviewFeatures]
    private readonly struct OnesComplementTransformation : IUnaryTransformation<nuint>, IUnaryTransformation<Vector<byte>>
    {
        static Vector<byte> IUnaryTransformation<Vector<byte>>.Transform(Vector<byte> value) => Vector.OnesComplement(value);

        static nuint IUnaryTransformation<nuint>.Transform(nuint value) => ~value;
    }
}