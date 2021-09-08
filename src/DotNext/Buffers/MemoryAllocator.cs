using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

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
    /// Allocates memory.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="length">The number of items in the rented memory.</param>
    /// <param name="exactSize">
    /// <see langword="true"/> to ask allocator to allocate exactly <paramref name="length"/>;
    /// <see langword="false"/> to allocate at least <paramref name="length"/>.
    /// </param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    /// <returns>The allocated memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryOwner<T> Invoke<T>(this MemoryAllocator<T>? allocator, int length, bool exactSize)
    {
        MemoryOwner<T> result;
        if (allocator is null)
        {
            result = Allocate<T>(length, exactSize);
        }
        else
        {
            result = allocator(length);
            if (!exactSize)
                result.Expand();
        }

        return result;
    }

    /// <summary>
    /// Returns array allocator.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <returns>The array allocator.</returns>
    public static MemoryAllocator<T> CreateArrayAllocator<T>()
    {
        return AllocateArray;

        static MemoryOwner<T> AllocateArray(int length)
            => new(GC.AllocateUninitializedArray<T>(length, false));
    }

    /// <summary>
    /// Rents a block of memory from <see cref="ArrayPool{T}.Shared"/> pool.
    /// </summary>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    /// <param name="length">The number of items in the rented memory.</param>
    /// <param name="exactSize">
    /// <see langword="true"/> to ask allocator to allocate exactly <paramref name="length"/>;
    /// <see langword="false"/> to allocate at least <paramref name="length"/>.
    /// </param>
    /// <returns>The allocated memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryOwner<T> Allocate<T>(int length, bool exactSize)
        => new(ArrayPool<T>.Shared, length, exactSize);
}