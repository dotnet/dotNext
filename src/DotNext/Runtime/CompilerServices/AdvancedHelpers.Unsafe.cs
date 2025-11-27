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
        internal static ref TTo InToRef<TFrom, TTo>(scoped ref readonly TFrom source)
            => ref Unsafe.As<TFrom, TTo>(ref Unsafe.AsRef(in source));
        
        /// <summary>
        /// Returns an address of the given by-ref parameter.
        /// </summary>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <param name="value">The object whose address is obtained.</param>
        /// <returns>An address of the given object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe nint AddressOf<T>(ref readonly T value)
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
            where T : unmanaged
        {
            Unsafe.SkipInit(out destination);
            Copy(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in source)), ref Unsafe.As<T, byte>(ref destination), checked((nuint)count * (nuint)sizeof(T)));
        }
    }
}