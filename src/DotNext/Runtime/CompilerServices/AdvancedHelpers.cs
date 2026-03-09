using System.Diagnostics.CodeAnalysis;
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
    private static unsafe ref byte Advance<T>(this ref byte ptr)
        where T : unmanaged, allows ref struct
        => ref Unsafe.Add(ref ptr, sizeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe ref byte Advance<T>([In] this ref byte address, [In, Out] nuint* length)
        where T : unmanaged, allows ref struct
    {
        *length -= (nuint)sizeof(T);
        return ref address.Advance<T>();
    }

    private static unsafe bool IsZero([In] ref byte address, nuint length)
    {
        var result = false;

        if (Vector.IsHardwareAccelerated && Vector<byte>.Count > sizeof(nuint))
        {
            while (length >= (nuint)Vector<byte>.Count)
            {
                if (Unsafe.ReadUnaligned<Vector<byte>>(ref address) == Vector<byte>.Zero)
                    address = ref address.Advance<Vector<byte>>(&length);
                else
                    goto exit;
            }
        }

        while (length >= (nuint)sizeof(nuint))
        {
            if (Unsafe.ReadUnaligned<nuint>(ref address) is 0U)
                address = ref address.Advance<nuint>(&length);
            else
                goto exit;
        }

        while (length > 0)
        {
            if (address is 0)
                address = ref address.Advance<byte>(&length);
            else
                goto exit;
        }

        result = true;
        exit:
        return result;
    }
    
    internal static int CompareUnaligned(ref byte first, ref byte second, nuint length)
    {
        var comparison = 0;
        for (nuint count; length > 0 && comparison is 0; length -= count, first = ref Unsafe.Add(ref first, count), second = ref Unsafe.Add(ref second, count))
        {
            count = length > int.MaxValue ? int.MaxValue : length;
            comparison = MemoryMarshal.CreateReadOnlySpan(ref first, (int)count).SequenceCompareTo(MemoryMarshal.CreateReadOnlySpan(ref second, (int)count));
        }

        return comparison;
    }
    
    internal static bool EqualsUnaligned(ref byte first, ref byte second, nuint length)
    {
        for (nuint count; length > 0; length -= count, first = ref Unsafe.Add(ref first, count), second = ref Unsafe.Add(ref second, count))
        {
            count = length > int.MaxValue ? int.MaxValue : length;
            if (!MemoryMarshal.CreateReadOnlySpan(ref first, (int)count).SequenceEqual(MemoryMarshal.CreateReadOnlySpan(ref second, (int)count)))
                return false;
        }

        return true;
    }
    
    private static void Copy([In] ref byte source, [In] ref byte destination, nuint length)
    {
        for (uint count; length > 0; length -= count, source = ref Unsafe.Add(ref source, count), destination = ref Unsafe.Add(ref destination, count))
        {
            count = uint.CreateSaturating(length);
            Unsafe.CopyBlockUnaligned(ref destination, ref source, count);
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    [ExcludeFromCodeCoverage]
    private readonly ref struct AlignmentHelperType<T>
        where T : allows ref struct
    {
        private readonly byte field1;
        private readonly T field2;
    }
}