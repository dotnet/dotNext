using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext;

/// <summary>
/// Provides extension methods for type <see cref="Span{T}"/> and <see cref="ReadOnlySpan{T}"/>.
/// </summary>
public static partial class Span
{
    /// <summary>
    /// Extends blittable types.
    /// </summary>
    /// <typeparam name="T">The blittable type.</typeparam>
    extension<T>(T)
        where T : unmanaged, allows ref struct
    {
        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Span<byte> AsBytes(ref T value)
            => MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), sizeof(T));

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> AsReadOnlyBytes(ref readonly T value)
            => AsBytes(ref Unsafe.AsRef(in value));
        
        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="pointer">The typed pointer.</param>
        /// <returns>The span of contiguous memory.</returns>
        [CLSCompliant(false)]
        public static unsafe Span<byte> AsBytes(T* pointer)
            => AsBytes(ref *pointer);
    }
}