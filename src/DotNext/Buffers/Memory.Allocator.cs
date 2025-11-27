using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

public static partial class Memory
{
    /// <summary>
    /// Extends <see cref="ArrayPool{T}"/> type.
    /// </summary>
    /// <param name="pool">The array pool.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    extension<T>(ArrayPool<T> pool)
    {
        private MemoryOwner<T> Allocate(int length)
            => new(pool, length);

        /// <summary>
        /// Converts array pool to the memory allocator.
        /// </summary>
        /// <returns>The array allocator.</returns>
        public MemoryAllocator<T> ToAllocator()
            => pool.Allocate;
    }

    /// <summary>
    /// Extends <see cref="MemoryPool{T}"/> type.
    /// </summary>
    /// <param name="pool">The memory pool.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    extension<T>(MemoryPool<T> pool)
    {
        private MemoryOwner<T> Allocate(int length)
            => new(pool, length);

        /// <summary>
        /// Converts memory pool to the memory allocator.
        /// </summary>
        /// <returns>The memory allocator.</returns>
        public MemoryAllocator<T> ToAllocator()
            => pool.Allocate;
    }

    /// <summary>
    /// Extends <see cref="Func{Int32, IMemoryOwner}"/> type.
    /// </summary>
    /// <param name="provider">The memory provider.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    extension<T>(Func<int, IMemoryOwner<T>> provider)
    {
        private MemoryOwner<T> Allocate(int length)
            => new(provider, length);

        /// <summary>
        /// Converts memory provider to the memory allocator.
        /// </summary>
        /// <returns>The memory allocator.</returns>
        public MemoryAllocator<T> ToAllocator()
            => provider.Allocate;
    }

    /// <summary>
    /// Extends <see cref="MemoryAllocator{T}"/> delegate.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    extension<T>(MemoryAllocator<T>? allocator)
    {
        /// <summary>
        /// Allocates memory of at least <paramref name="length"/> size.
        /// </summary>
        /// <param name="length">The number of items in the rented memory.</param>
        /// <returns>The allocated memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryOwner<T> AllocateAtLeast(int length)
        {
            MemoryOwner<T> result;
            if (allocator is null)
            {
                result = AllocateAtLeast<T>(length);
            }
            else
            {
                result = allocator(length);
                result.Expand();
            }

            return result;
        }

        /// <summary>
        /// Allocates memory of <paramref name="length"/> size.
        /// </summary>
        /// <param name="length">The number of items in the rented memory.</param>
        /// <returns>The allocated memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryOwner<T> AllocateExactly(int length)
        {
            MemoryOwner<T> result;
            if (allocator is null)
            {
                result = AllocateExactly<T>(length);
            }
            else
            {
                result = allocator(length);
                result.Truncate(length);
            }

            return result;
        }
    }

    /// <summary>
    /// Returns array allocator.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <returns>The array allocator.</returns>
    public static MemoryAllocator<T> GetArrayAllocator<T>()
    {
        return AllocateArray;

        static MemoryOwner<T> AllocateArray(int length)
            => new(GC.AllocateUninitializedArray<T>(length, pinned: false));
    }

    /// <summary>
    /// Returns an allocator of pinned arrays.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <returns>The array allocator.</returns>
    public static MemoryAllocator<T> GetPinnedArrayAllocator<T>()
        where T : unmanaged
    {
        return AllocateArray;

        static MemoryOwner<T> AllocateArray(int length)
            => new(GC.AllocateUninitializedArray<T>(length, pinned: true));
    }

    /// <summary>
    /// Rents a block of memory of at least
    /// <paramref name="length"/> size from <see cref="ArrayPool{T}.Shared"/> pool.
    /// </summary>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    /// <param name="length">The number of items in the rented memory.</param>
    /// <returns>The allocated memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryOwner<T> AllocateAtLeast<T>(int length)
        => new(ArrayPool<T>.Shared, length, exactSize: false);

    /// <summary>
    /// Rents a block of memory of the specified size from <see cref="ArrayPool{T}.Shared"/> pool.
    /// </summary>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    /// <param name="length">The number of items in the rented memory.</param>
    /// <returns>The allocated memory.</returns>
    public static MemoryOwner<T> AllocateExactly<T>(int length)
        => new(ArrayPool<T>.Shared, length, exactSize: true);
}