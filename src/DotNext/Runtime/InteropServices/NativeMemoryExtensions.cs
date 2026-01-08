using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

using CompilerServices;

/// <summary>
/// Provides extensions for <see cref="NativeMemory"/> type.
/// </summary>
public static class NativeMemoryExtensions
{
    /// <summary>
    /// Extends <see cref="NativeMemory"/> type.
    /// </summary>
    extension(NativeMemory)
    {
        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <param name="first">The pointer to the first memory block.</param>
        /// <param name="second">The pointer to the second memory block.</param>
        /// <param name="length">The length of the first and second memory blocks.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        [CLSCompliant(false)]
        public static unsafe int Compare([In] void* first, [In] void* second, nuint length)
            => AdvancedHelpers.CompareUnaligned(ref Unsafe.AsRef<byte>(first), ref Unsafe.AsRef<byte>(second), length);
        
        /// <summary>
        /// Computes equality between two blocks of memory.
        /// </summary>
        /// <param name="first">A pointer to the first memory block.</param>
        /// <param name="second">A pointer to the second memory block.</param>
        /// <param name="length">Length of first and second memory blocks, in bytes.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same data; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public static unsafe bool Equals([In] void* first, [In] void* second, nuint length)
            => AdvancedHelpers.EqualsUnaligned(ref Unsafe.AsRef<byte>(first), ref Unsafe.AsRef<byte>(second), length);
        
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
            => AdvancedHelpers.Copy(in *input, out *output);
        
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
            => AdvancedHelpers.Swap(ref *first, ref *second);
        
        /// <summary>
        /// Computes transient hash code of the specified pointer.
        /// </summary>
        /// <param name="pointer">The pointer value.</param>
        /// <returns>The hash code of the pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe int PointerHashCode([In] void* pointer)
            => ((nuint*)&pointer)->GetHashCode();
    }
}