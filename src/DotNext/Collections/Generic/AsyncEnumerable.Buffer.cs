namespace DotNext.Collections.Generic;

using Buffers;

/// <summary>
/// Provides extension methods for <see cref="IAsyncEnumerable{T}"/> interface.
/// </summary>
public static partial class AsyncEnumerable
{
    /// <summary>
    /// Creates a copy of the elements from the collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="enumerable">The collection to copy.</param>
    /// <param name="sizeHint">The approximate size of the collection, if known.</param>
    /// <param name="allocator">The allocator of the memory block used to place copied elements.</param>
    /// <param name="token">The token that can be used to cancel the enumeration.</param>
    /// <returns>Rented memory block containing a copy of the elements from the collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enumerable"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sizeHint"/> is less than zero.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task<MemoryOwner<T>> CopyAsync<T>(this IAsyncEnumerable<T> enumerable, int sizeHint = 0, MemoryAllocator<T>? allocator = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        using var buffer = new PooledBufferWriter<T>(allocator) { Capacity = sizeHint };

        await foreach (var item in enumerable.WithCancellation(token).ConfigureAwait(false))
            buffer.Add(item);

        return buffer.DetachBuffer();
    }
}