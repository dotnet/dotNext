using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

/// <summary>
/// Provides low-level extensions to work with the memory.
/// </summary>
public static class MemoryMarshalExtensions
{
    /// <summary>
    /// Extends <see cref="MemoryMarshal"/> type.
    /// </summary>
    extension(MemoryMarshal)
    {
        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Span<byte> AsBytes<T>(ref T value)
            where T : unmanaged, allows ref struct
            => MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), sizeof(T));

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> AsReadOnlyBytes<T>(ref readonly T value)
            where T : unmanaged, allows ref struct
            => AsBytes(ref Unsafe.AsRef(in value));
    }
}