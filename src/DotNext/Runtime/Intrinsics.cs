using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Runtime;

/// <summary>
/// Represents highly optimized runtime intrinsic methods.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class Intrinsics
{
    /// <summary>
    /// Provides the fast way to check whether the specified type accepts  <see langword="null"/> value as valid value.
    /// </summary>
    /// <remarks>
    /// This method always returns <see langword="true"/> for all reference types and <see cref="Nullable{T}"/>.
    /// On mainstream implementations of .NET CLR, this method is replaced by constant value by JIT compiler with zero runtime overhead.
    /// </remarks>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns><see langword="true"/> if <typeparamref name="T"/> is nullable type; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullable<T>()
    {
        Unsafe.SkipInit(out T value);
        return value is null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref TTo InToRef<TFrom, TTo>(ref readonly TFrom source)
    {
        PushInRef(in source);
        return ref ReturnRef<TTo>();
    }

    /// <summary>
    /// Indicates that specified value type is the default value.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">Value to check.</param>
    /// <returns><see langword="true"/>, if value is default value; otherwise, <see langword="false"/>.</returns>
    public static bool IsDefault<T>(in T value) => Unsafe.SizeOf<T>() switch
    {
        0 => true,
        sizeof(byte) => InToRef<T, byte>(in value) is 0,
        sizeof(ushort) => Unsafe.ReadUnaligned<ushort>(ref InToRef<T, byte>(value)) is 0,
        sizeof(uint) => Unsafe.ReadUnaligned<uint>(ref InToRef<T, byte>(value)) is 0U,
        sizeof(ulong) => Unsafe.ReadUnaligned<ulong>(ref InToRef<T, byte>(value)) is 0UL,
        _ => IsZero(ref InToRef<T, byte>(in value), (nuint)Unsafe.SizeOf<T>()),
    };

    /// <summary>
    /// Returns the runtime handle associated with type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type which runtime handle should be obtained.</typeparam>
    /// <returns>The runtime handle representing type <typeparamref name="T"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RuntimeTypeHandle TypeOf<T>()
    {
        Ldtoken(Type<T>());
        return Return<RuntimeTypeHandle>();
    }

    /// <summary>
    /// Provides unified behavior of type cast for reference and value types.
    /// </summary>
    /// <remarks>
    /// This method never returns <see langword="null"/> because it treats <see langword="null"/>
    /// value passed to <paramref name="obj"/> as invalid object of type <typeparamref name="T"/>.
    /// </remarks>
    /// <param name="obj">The object to cast.</param>
    /// <typeparam name="T">Conversion result.</typeparam>
    /// <returns>The result of conversion.</returns>
    /// <exception cref="InvalidCastException"><paramref name="obj"/> is <see langword="null"/> or not of type <typeparamref name="T"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Cast<T>(object? obj)
        where T : notnull
    {
        if (obj is null)
            ThrowInvalidCastException();

        return (T)obj;

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowInvalidCastException() => throw new InvalidCastException();
    }

    /// <summary>
    /// Computes transient hash code of the specified pointer.
    /// </summary>
    /// <param name="pointer">The pointer value.</param>
    /// <returns>The hash code of the pointer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe int PointerHashCode([In] void* pointer)
    {
        Ldarga(nameof(pointer));
        Call(Method(Type<UIntPtr>(), nameof(UIntPtr.GetHashCode)));
        return Return<int>();
    }

    /// <summary>
    /// Returns an address of the given by-ref parameter.
    /// </summary>
    /// <typeparam name="T">The type of object.</typeparam>
    /// <param name="value">The object whose address is obtained.</param>
    /// <returns>An address of the given object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint AddressOf<T>(ref readonly T value)
    {
        PushInRef(in value);
        Conv_I();
        return Return<IntPtr>();
    }

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
    {
        Ldarg(nameof(reference));
        Refanyval<T>();
        return ref ReturnRef<T>();
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

    /// <summary>
    /// Bitwise comparison of two memory blocks.
    /// </summary>
    /// <param name="first">The pointer to the first memory block.</param>
    /// <param name="second">The pointer to the second memory block.</param>
    /// <param name="length">The length of the first and second memory blocks.</param>
    /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
    [CLSCompliant(false)]
    public static unsafe int Compare([In] void* first, [In] void* second, nuint length)
        => CompareUnaligned(ref Unsafe.AsRef<byte>(first), ref Unsafe.AsRef<byte>(second), length);

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

    /// <summary>
    /// Computes equality between two blocks of memory.
    /// </summary>
    /// <param name="first">A pointer to the first memory block.</param>
    /// <param name="second">A pointer to the second memory block.</param>
    /// <param name="length">Length of first and second memory blocks, in bytes.</param>
    /// <returns><see langword="true"/>, if both memory blocks have the same data; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static unsafe bool Equals([In] void* first, [In] void* second, nuint length)
        => EqualsUnaligned(ref Unsafe.AsRef<byte>(first), ref Unsafe.AsRef<byte>(second), length);

    /// <summary>
    /// Copies one value into another.
    /// </summary>
    /// <typeparam name="T">The value type to copy.</typeparam>
    /// <param name="input">The reference to the source location.</param>
    /// <param name="output">The reference to the destination location.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy<T>(in T input, out T output)
        where T : struct
    {
        PushOutRef(out output);
        PushInRef(in input);
        Cpobj<T>();
        Ret();
    }

    /// <summary>
    /// Copies one value into another assuming unaligned memory access.
    /// </summary>
    /// <typeparam name="T">The value type to copy.</typeparam>
    /// <param name="input">The reference to the source location.</param>
    /// <param name="output">The reference to the destination location.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe void CopyUnaligned<T>([In] T* input, [Out] T* output)
        where T : unmanaged
        => Unsafe.WriteUnaligned(output, Unsafe.ReadUnaligned<T>(input));

    /// <summary>
    /// Copies one value into another.
    /// </summary>
    /// <typeparam name="T">The value type to copy.</typeparam>
    /// <param name="input">The reference to the source location.</param>
    /// <param name="output">The reference to the destination location.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe void Copy<T>([In] T* input, [Out] T* output)
        where T : unmanaged
        => Copy(in input[0], out output[0]);

    private static void Copy([In] ref byte source, [In] ref byte destination, nuint length)
    {
        for (nuint count; length > 0; length -= count, source = ref Unsafe.Add(ref source, count), destination = ref Unsafe.Add(ref destination, count))
        {
            count = length > uint.MaxValue ? uint.MaxValue : length;
            Unsafe.CopyBlockUnaligned(ref destination, ref source, (uint)count);
        }
    }

    /// <summary>
    /// Copies the specified number of elements from source address to the destination address.
    /// </summary>
    /// <param name="source">The address of the bytes to copy.</param>
    /// <param name="destination">The target address.</param>
    /// <param name="count">The number of elements to copy.</param>
    /// <typeparam name="T">The type of the element.</typeparam>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Copy<T>(in T source, out T destination, nuint count)
        where T : unmanaged
    {
        Unsafe.SkipInit(out destination);
        Copy(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in source)), ref Unsafe.As<T, byte>(ref destination), checked((nuint)count * (nuint)sizeof(T)));
    }

    /// <summary>
    /// Swaps two values.
    /// </summary>
    /// <param name="first">The first value to be replaced with <paramref name="second"/>.</param>
    /// <param name="second">The second value to be replaced with <paramref name="first"/>.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    public static void Swap<T>(ref T first, ref T second)
        => (second, first) = (first, second);

    /// <summary>
    /// Swaps two values.
    /// </summary>
    /// <param name="first">The first value to be replaced with <paramref name="second"/>.</param>
    /// <param name="second">The second value to be replaced with <paramref name="first"/>.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Swap<T>(T* first, T* second)
        where T : unmanaged
        => Swap(ref first[0], ref second[0]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe ref byte Advance<T>(this ref byte ptr)
        where T : unmanaged
        => ref Unsafe.Add(ref ptr, sizeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe ref byte Advance<T>([In] this ref byte address, [In, Out] nuint* length)
        where T : unmanaged
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

    /// <summary>
    /// Checks whether the specified object is exactly of the specified type.
    /// </summary>
    /// <param name="obj">The object to test.</param>
    /// <typeparam name="T">The expected type of object.</typeparam>
    /// <returns><see langword="true"/> if <paramref name="obj"/> is not <see langword="null"/> and of type <typeparamref name="T"/>; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsExactTypeOf<T>(object? obj) => obj?.GetType() == typeof(T);

    /// <summary>
    /// Gets length of the array.
    /// </summary>
    /// <remarks>
    /// This method supports one-dimensional as well as multi-dimensional arrays.
    /// </remarks>
    /// <param name="array">The array object.</param>
    /// <returns>The length of the array as native unsigned integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static nuint GetLength(this Array array)
    {
        Push(array);
        Ldlen();
        return Return<nuint>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<TOutput> ReinterpretCast<TInput, TOutput>(Span<TInput> input)
        where TInput : unmanaged
        where TOutput : unmanaged
    {
        Debug.Assert(Unsafe.SizeOf<TInput>() == Unsafe.SizeOf<TOutput>());

        return MemoryMarshal.CreateSpan(ref Unsafe.As<TInput, TOutput>(ref MemoryMarshal.GetReference(input)), input.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<TOutput> ReinterpretCast<TInput, TOutput>(ReadOnlySpan<TInput> input)
        where TInput : unmanaged
        where TOutput : unmanaged
    {
        Debug.Assert(Unsafe.SizeOf<TInput>() == Unsafe.SizeOf<TOutput>());

        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<TInput, TOutput>(ref MemoryMarshal.GetReference(input)), input.Length);
    }

    /// <summary>
    /// Determines whether the object overrides <see cref="object.Finalize()"/> method.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns><see langword="true"/> if <see cref="object.Finalize()"/> is overridden; otherwise, <see langword="false"/>.</returns>
    public static bool HasFinalizer(object obj)
    {
        Push(obj);
        Ldvirtftn(Method(Type<object>(), nameof(Finalize)));
        Ldftn(Method(Type<object>(), nameof(Finalize)));
        Ceq();
        Ldc_I4_0();
        Ceq();
        return Return<bool>();
    }

    [StructLayout(LayoutKind.Sequential)]
    [ExcludeFromCodeCoverage]
    private readonly struct AlignmentHelperType<T>
    {
        private readonly byte field1;
        private readonly T field2;
    }

    /// <summary>
    /// Gets the alignment requirement for type <typeparamref name="T"/>, in bytes.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>The alignment of the type <typeparamref name="T"/>.</returns>
    /// <seealso href="https://en.cppreference.com/w/c/language/_Alignof">_Alignof operator in C++</seealso>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignOf<T>()
        => Unsafe.SizeOf<AlignmentHelperType<T>>() - Unsafe.SizeOf<T>();

    /// <summary>
    /// Determines whether the two types are binary compatible, i.e. both types have the same
    /// size and memory alignment.
    /// </summary>
    /// <typeparam name="T1">The first type to compare.</typeparam>
    /// <typeparam name="T2">The second type to compare.</typeparam>
    /// <returns><see langword="true"/> if both types are binary compatible; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreCompatible<T1, T2>()
        => Unsafe.SizeOf<T1>() == Unsafe.SizeOf<T2>() && AlignOf<T1>() == AlignOf<T2>();
}