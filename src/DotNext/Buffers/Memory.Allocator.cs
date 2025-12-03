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

    /// <param name="allocator">The memory allocator.</param>
    extension<T>(MemoryAllocator<T> allocator)
    {
        /// <summary>
        /// Allocates memory of at least <paramref name="length"/> size.
        /// </summary>
        /// <param name="length">The number of items in the rented memory.</param>
        /// <returns>The allocated memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryOwner<T> AllocateAtLeast(int length)
            => allocator.Invoke(length);

        /// <summary>
        /// Allocates memory of <paramref name="length"/> size.
        /// </summary>
        /// <param name="length">The number of items in the rented memory.</param>
        /// <returns>The allocated memory.</returns>]
        public MemoryOwner<T> AllocateExactly(int length)
        {
            var result = allocator(length);
            result.Truncate(length);
            return result;
        }
    }

    /// <summary>
    /// Extends <see cref="MemoryAllocator{T}"/> delegate.
    /// </summary>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    extension<T>(MemoryAllocator<T>? allocator)
    {
        /// <summary>
        /// Gets the array allocator.
        /// </summary>
        /// <value>The array allocator.</value>
        public static MemoryAllocator<T> ArrayAllocator
        {
            get
            {
                return AllocateArray;

                static MemoryOwner<T> AllocateArray(int length)
                    => new(GC.AllocateUninitializedArray<T>(length, pinned: false));
            }
        }

        /// <summary>
        /// Gets the default allocator that uses <see cref="ArrayPool{T}.Shared"/> pool.
        /// </summary>
        public static MemoryAllocator<T> Default
        {
            get
            {
                return AllocateUsingArrayPool;

                static MemoryOwner<T> AllocateUsingArrayPool(int length)
                    => new(ArrayPool<T>.Shared, length, exactSize: false);
            }
        }

        /// <summary>
        /// Gets <see cref="Memory.get_Default{T}"/> allocator if the current is <see langword="null"/>.
        /// </summary>
        public MemoryAllocator<T> DefaultIfNull => allocator ?? get_Default<T>();
    }

    /// <summary>
    /// Extends <see cref="MemoryAllocator{T}"/> with static members.
    /// </summary>
    /// <typeparam name="T">The blittable type.</typeparam>
    extension<T>(MemoryAllocator<T>) where T : unmanaged
    {
        /// <summary>
        /// Returns an allocator of pinned arrays.
        /// </summary>
        /// <value>The array allocator.</value>
        public static MemoryAllocator<T> PinnedArrayAllocator
        {
            get
            {
                return AllocateArray;

                static MemoryOwner<T> AllocateArray(int length)
                    => new(GC.AllocateUninitializedArray<T>(length, pinned: true));
            }
        }
    }
}