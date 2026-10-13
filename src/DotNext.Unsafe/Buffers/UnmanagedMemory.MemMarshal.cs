using System.Runtime.InteropServices;

namespace DotNext.Buffers;

partial class UnmanagedMemory
{
    /// <summary>
    /// Extends <see cref="Memory{T}"/> type.
    /// </summary>
    extension(MemoryMarshal)
    {
        /// <summary>
        /// Wraps unmanaged pointer to <see cref="Memory{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of elements in the memory.</typeparam>
        /// <param name="pointer">The pointer to a sequence of elements.</param>
        /// <param name="length">The number of elements.</param>
        /// <returns>The memory block that represents the data located at the pointer.</returns>
        [CLSCompliant(false)]
        public static unsafe Memory<T> AsMemory<T>(T* pointer, int length)
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(pointer);
            ArgumentOutOfRangeException.ThrowIfNegative(length);

            return length > 0
                ? new UnmanagedMemory<T>(pointer, length).Memory
                : Memory<T>.Empty;
        }
    }
}