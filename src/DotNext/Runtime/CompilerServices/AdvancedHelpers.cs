using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Represents advanced helpers.
/// </summary>
public static partial class AdvancedHelpers
{
    /// <summary>
    /// Converts typed reference into managed pointer.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="reference">The typed reference.</param>
    /// <returns>A managed pointer to the value represented by reference.</returns>
    /// <exception cref="InvalidCastException"><typeparamref name="T"/> is not identical to the type stored in the typed reference.</exception>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T AsRef<T>(this TypedReference reference)
        where T : allows ref struct
    {
        Ldarg(nameof(reference));
        Refanyval<T>();
        return ref ReturnRef<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe ref readonly byte Advance<T>(this ref readonly byte address, scoped ref nuint length)
        where T : unmanaged, allows ref struct
    {
        length -= (nuint)sizeof(T);
        return ref Unsafe.Add(ref Unsafe.AsRef(in address), sizeof(T));
    }

    internal static unsafe bool IsZero(ref readonly byte address, nuint length)
    {
        var result = false;

        if (Vector.IsHardwareAccelerated && Vector<byte>.Count > sizeof(nuint))
        {
            while (length >= (nuint)Vector<byte>.Count)
            {
                if (Vector.LoadUnsafe(in address) == Vector<byte>.Zero)
                    address = ref address.Advance<Vector<byte>>(ref length);
                else
                    goto exit;
            }
        }

        while (length >= (nuint)sizeof(nuint))
        {
            if (Unsafe.ReadUnaligned<nuint>(in address) is 0U)
                address = ref address.Advance<nuint>(ref length);
            else
                goto exit;
        }

        while (length > 0)
        {
            if (address is 0)
                address = ref address.Advance<byte>(ref length);
            else
                goto exit;
        }

        result = true;
        exit:
        return result;
    }
    
    internal static int CompareUnaligned(ref readonly byte first, ref readonly byte second, nuint length)
    {
        var comparison = 0;
        for (int count;
             length > 0 && comparison is 0;
             length -= (uint)count,
             first = ref Unsafe.Add(ref Unsafe.AsRef(in first), count),
             second = ref Unsafe.Add(ref Unsafe.AsRef(in second), count))
        {
            count = int.CreateSaturating(length);
            comparison = MemoryMarshal.CreateReadOnlySpan(in first, count)
                .SequenceCompareTo(MemoryMarshal.CreateReadOnlySpan(in second, count));
        }

        return comparison;
    }

    internal static bool EqualsUnaligned(ref readonly byte first, ref readonly byte second, nuint length)
    {
        for (int count;
             length > 0;
             length -= (uint)count,
             first = ref Unsafe.Add(ref Unsafe.AsRef(in first), count),
             second = ref Unsafe.Add(ref Unsafe.AsRef(in second), count))
        {
            count = int.CreateSaturating(length);
            if (!MemoryMarshal.CreateReadOnlySpan(in first, count)
                    .SequenceEqual(MemoryMarshal.CreateReadOnlySpan(in second, count)))
                return false;
        }

        return true;
    }

    private static void Copy(ref readonly byte source, ref byte destination, nuint length)
    {
        for (uint count;
             length > 0;
             length -= count,
             source = ref Unsafe.Add(ref Unsafe.AsRef(in source), count),
             destination = ref Unsafe.Add(ref destination, count))
        {
            count = uint.CreateSaturating(length);
            Unsafe.CopyBlockUnaligned(ref destination, in source, count);
        }
    }
}

