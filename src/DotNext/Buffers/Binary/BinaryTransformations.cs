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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<T> LoadAsVector128<T>(ReadOnlySpan<T> input)
        where T : unmanaged
        => Unsafe.ReadUnaligned<Vector128<T>>(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(input)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<T> LoadAsVector256<T>(ReadOnlySpan<T> input)
        where T : unmanaged
        => Unsafe.ReadUnaligned<Vector256<T>>(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(input)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreAsVector128<T>(Vector128<T> input, Span<T> output)
        where T : unmanaged
        => Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(output)), input);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreAsVector256<T>(Vector256<T> input, Span<T> output)
        where T : unmanaged
        => Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(output)), input);
}