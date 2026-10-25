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
            => AdvancedHelpers.CompareUnaligned(in Unsafe.AsRef<byte>(first), in Unsafe.AsRef<byte>(second), length);
        
        /// <summary>
        /// Computes equality between two blocks of memory.
        /// </summary>
        /// <param name="first">A pointer to the first memory block.</param>
        /// <param name="second">A pointer to the second memory block.</param>
        /// <param name="length">Length of first and second memory blocks, in bytes.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same data; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public static unsafe bool Equals([In] void* first, [In] void* second, nuint length)
            => AdvancedHelpers.EqualsUnaligned(in Unsafe.AsRef<byte>(first), in Unsafe.AsRef<byte>(second), length);
        
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
            => RuntimeHelpers.Copy(in *input, out *output);
        
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
            => RuntimeHelpers.Swap(ref *first, ref *second);
        
        /// <summary>
        /// Computes transient hash code of the specified pointer.
        /// </summary>
        /// <param name="pointer">The pointer value.</param>
        /// <returns>The hash code of the pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe int PointerHashCode([In] void* pointer)
            => ((nuint*)&pointer)->GetHashCode();

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="Span{T}"/>.
        /// </summary>
        /// <typeparam name="T">Blittable type.</typeparam>
        /// <param name="pointer">The typed pointer.</param>
        /// <returns>The span of contiguous memory.</returns>
        [CLSCompliant(false)]
        public static unsafe Span<byte> AsBytes<T>(T* pointer)
            where T : unmanaged, allows ref struct
            => MemoryMarshal.AsBytes(ref *pointer);
    }

    /// <summary>
    /// Extends <see cref="Volatile"/> with pointer to pointer volatile operations.
    /// </summary>
    extension(Volatile)
    {
        /// <summary>
        /// Reads the pointer from the specified location. On systems that require it, inserts a memory barrier
        /// that prevents the processor from reordering memory operations as follows: if a read or write appears
        /// after this method in the code, the processor cannot move it before this method.
        /// </summary>
        /// <remarks>
        /// Unlike <see langword="volatile"/> field modifier, this method can be used for the location
        /// obtained through the pointer.
        /// </remarks>
        /// <typeparam name="T">The type of the value referenced by the pointer.</typeparam>
        /// <param name="location">The location of the pointer to read.</param>
        /// <returns>The pointer that was read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe T* Read<T>(T** location)
            where T : unmanaged
            => (T*)Volatile.Read(ref *(nint*)location);

        /// <summary>
        /// Writes the pointer to the specified location. On systems that require it, inserts a memory barrier
        /// that prevents the processor from reordering memory operations as follows: if a read or write appears
        /// before this method in the code, the processor cannot move it after this method.
        /// </summary>
        /// <remarks>
        /// Unlike <see langword="volatile"/> field modifier, this method can be used for the location
        /// obtained through the pointer.
        /// </remarks>
        /// <typeparam name="T">The type of the value referenced by the pointer.</typeparam>
        /// <param name="location">The location of the pointer to write.</param>
        /// <param name="value">The pointer to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void Write<T>(T** location, T* value)
            where T : unmanaged
            => Volatile.Write(ref *(nint*)location, (nint)value);
    }
}