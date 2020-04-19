using System.Buffers;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents memory allocator.
    /// </summary>
    /// <param name="length">The number of items in the rented memory.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    /// <returns>The rented memory.</returns>
    public delegate MemoryOwner<T> MemoryAllocator<T>(int length);

    /// <summary>
    /// Represents interop layer between .NET memory pools
    /// and <see cref="MemoryAllocator{T}"/>.
    /// </summary>
    public static class MemoryAllocator
    {
        private static MemoryOwner<T> Allocate<T>(this ArrayPool<T> pool, int length)
            => new MemoryOwner<T>(pool, length);

        /// <summary>
        /// Converts array pool to the memory allocator.
        /// </summary>
        /// <param name="pool">The array pool.</param>
        /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
        /// <returns>The array allocator.</returns>
        public static MemoryAllocator<T> ToAllocator<T>(this ArrayPool<T> pool)
            => pool.Allocate;

        private static MemoryOwner<T> Allocate<T>(this MemoryPool<T> pool, int length)
            => new MemoryOwner<T>(pool, length);

        /// <summary>
        /// Converts memory pool to the memory allocator.
        /// </summary>
        /// <param name="pool">The memory pool.</param>
        /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
        /// <returns>The memory allocator.</returns>
        public static MemoryAllocator<T> ToAllocator<T>(this MemoryPool<T> pool)
            => pool.Allocate;
    }
}