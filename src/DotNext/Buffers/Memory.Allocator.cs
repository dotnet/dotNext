using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

public static partial class Memory
{
    private static MemoryOwner<T> Allocate<T>(this ArrayPool<T> pool, int length)
        => new(pool, length);

    /// <summary>
    /// Converts array pool to the memory allocator.
    /// </summary>
    /// <param name="pool">The array pool.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    /// <returns>The array allocator.</returns>
    public static MemoryAllocator<T> ToAllocator<T>(this ArrayPool<T> pool)
        => pool.Allocate;

    private static MemoryOwner<T> Allocate<T>(this MemoryPool<T> pool, int length)
        => new(pool, length);

    /// <summary>
    /// Converts memory pool to the memory allocator.
    /// </summary>
    /// <param name="pool">The memory pool.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    /// <returns>The memory allocator.</returns>
    public static MemoryAllocator<T> ToAllocator<T>(this MemoryPool<T> pool)
        => pool.Allocate;

    private static MemoryOwner<T> Allocate<T>(this Func<int, IMemoryOwner<T>> provider, int length)
        => new(provider, length);

    /// <summary>
    /// Converts memory provider to the memory allocator.
    /// </summary>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    /// <param name="provider">The memory provider.</param>
    /// <returns>The memory allocator.</returns>
    public static MemoryAllocator<T> ToAllocator<T>(this Func<int, IMemoryOwner<T>> provider)
        => provider.Allocate;

    /// <summary>
    /// Allocates memory of at least <paramref name="length"/> size.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="length">The number of items in the rented memory.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    /// <returns>The allocated memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryOwner<T> AllocateAtLeast<T>(this MemoryAllocator<T>? allocator, int length)
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
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="length">The number of items in the rented memory.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    /// <returns>The allocated memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryOwner<T> AllocateExactly<T>(this MemoryAllocator<T>? allocator, int length)
    {
        MemoryOwner<T> result;
        if (allocator is null)
        {
            result = AllocateAtLeast<T>(length);
        }
        else
        {
            result = allocator(length);
            result.Truncate(length);
        }

        return result;
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