using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Versioning;

namespace DotNext.Buffers.Binary;

/// <summary>
/// Provides various binary transformations.
/// </summary>
public static partial class BinaryTransformations
{
    [RequiresPreviewFeatures]
    private interface IUnaryTransformation<T>
        where T : unmanaged
    {
        public static abstract T Transform(T value);
    }

    [RequiresPreviewFeatures]
    private interface IBinaryTransformation<T>
        where T : unmanaged
    {
        public static abstract T Transform(T x, T y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<T> LoadVector128<T>(ref T input)
        where T : unmanaged
        => Unsafe.ReadUnaligned<Vector128<T>>(ref Unsafe.As<T, byte>(ref input));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<T> LoadVector128<T>(ReadOnlySpan<T> input)
        where T : unmanaged
        => LoadVector128(ref MemoryMarshal.GetReference(input));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<T> LoadVector256<T>(ref T input)
        where T : unmanaged
        => Unsafe.ReadUnaligned<Vector256<T>>(ref Unsafe.As<T, byte>(ref input));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<T> LoadVector256<T>(ReadOnlySpan<T> input)
        where T : unmanaged
        => LoadVector256(ref MemoryMarshal.GetReference(input));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreVector128<T>(Vector128<T> input, ref T output)
        where T : unmanaged
        => Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref output), input);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreVector128<T>(Vector128<T> input, Span<T> output)
        where T : unmanaged
        => StoreVector128(input, ref MemoryMarshal.GetReference(output));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreVector256<T>(Vector256<T> input, ref T output)
        where T : unmanaged
        => Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref output), input);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreVector256<T>(Vector256<T> input, Span<T> output)
        where T : unmanaged
        => StoreVector256(input, ref MemoryMarshal.GetReference(output));
}