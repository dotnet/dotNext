using static InlineIL.IL;
using static InlineIL.IL.Emit;

using System.Runtime.CompilerServices;

namespace DotNext.Threading;

using Diagnostics.CodeAnalysis;

partial class Atomic
{
    /// <summary>
    /// Extends <see cref="System.Threading.Volatile"/> with pointer to pointer volatile operations.
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
        [MemoryOrdering(MemoryOrdering.Acquire)]
        public static unsafe T* Read<T>(T** location)
            where T : unmanaged
            => Read(in *location);

        /// <summary>
        /// Reads the pointer from the specified location. On systems that require it, inserts a memory barrier
        /// that prevents the processor from reordering memory operations as follows: if a read or write appears
        /// after this method in the code, the processor cannot move it before this method.
        /// </summary>
        /// <remarks>
        /// Unlike <see langword="volatile"/> field modifier, this method can be used for any location
        /// passed by reference, such as an array element or a field of the struct accessed by reference.
        /// </remarks>
        /// <typeparam name="T">The type of the value referenced by the pointer.</typeparam>
        /// <param name="location">The location of the pointer to read.</param>
        /// <returns>The pointer that was read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        [MemoryOrdering(MemoryOrdering.Acquire)]
        public static unsafe T* Read<T>(ref readonly T* location)
            where T : unmanaged
        {
            Ldarg(nameof(location));
            Volatile();
            Ldind_I();
            return ReturnPointer<T>();
        }

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
        [MemoryOrdering(MemoryOrdering.Release)]
        public static unsafe void Write<T>(T** location, T* value)
            where T : unmanaged
            => Write(ref *location, value);

        /// <summary>
        /// Writes the pointer to the specified location. On systems that require it, inserts a memory barrier
        /// that prevents the processor from reordering memory operations as follows: if a read or write appears
        /// before this method in the code, the processor cannot move it after this method.
        /// </summary>
        /// <remarks>
        /// Unlike <see langword="volatile"/> field modifier, this method can be used for any location
        /// passed by reference, such as an array element or a field of the struct accessed by reference.
        /// </remarks>
        /// <typeparam name="T">The type of the value referenced by the pointer.</typeparam>
        /// <param name="location">The location of the pointer to write.</param>
        /// <param name="value">The pointer to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        [MemoryOrdering(MemoryOrdering.Release)]
        public static unsafe void Write<T>(ref T* location, T* value)
            where T : unmanaged
        {
            Ldarg(nameof(location));
            Ldarg(nameof(value));
            Volatile();
            Stind_I();
        }
    }
}