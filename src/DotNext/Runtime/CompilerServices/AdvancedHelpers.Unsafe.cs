using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices;

partial class AdvancedHelpers
{
    /// <summary>
    /// Extends <see cref="Unsafe"/> type.
    /// </summary>
    extension(Unsafe)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref TTo InToRef<TFrom, TTo>(ref readonly TFrom source)
            where TFrom : allows ref struct
            where TTo : allows ref struct
            => ref Unsafe.As<TFrom, TTo>(ref Unsafe.AsRef(in source));
        
        /// <summary>
        /// Returns an address of the given by-ref parameter.
        /// </summary>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <param name="value">The object whose address is obtained.</param>
        /// <returns>An address of the given object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe nint AddressOf<T>(ref readonly T value)
            where T : allows ref struct
            => (nint)Unsafe.AsPointer(ref Unsafe.AsRef(in value));
        
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
            where T : unmanaged, allows ref struct
        {
            Unsafe.SkipInit(out destination);
            Copy(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in source)), ref Unsafe.As<T, byte>(ref destination), checked((nuint)count * (nuint)sizeof(T)));
        }
        
        /// <summary>
        /// Gets the alignment requirement for type <typeparamref name="T"/>, in bytes.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The alignment of the type <typeparamref name="T"/>.</returns>
        /// <seealso href="https://en.cppreference.com/w/c/language/_Alignof">_Alignof operator in C++</seealso>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignOf<T>()
            where T : allows ref struct
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
            where T1 : allows ref struct
            where T2 : allows ref struct
            => Unsafe.SizeOf<T1>() == Unsafe.SizeOf<T2>() && AlignOf<T1>() == AlignOf<T2>();
        
        internal static ref byte GetRawData(object obj)
            => ref Unsafe.As<RawData>(obj).Data;
    }
}

file abstract class RawData
{
    internal byte Data;

    private RawData() => throw new NotImplementedException();
}