using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Collections.Generic;

using Buffers;
using static Runtime.Intrinsics;

public static partial class Sequence
{
    /// <summary>
    /// Creates a copy of the elements from the collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="enumerable">The collection to copy.</param>
    /// <param name="sizeHint">The approximate size of the collection, if known.</param>
    /// <param name="allocator">The allocator of the memory block used to place copied elements.</param>
    /// <returns>Rented memory block containing a copy of the elements from the collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enumerable"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sizeHint"/> is less than zero.</exception>
    public static MemoryOwner<T> Copy<T>(this IEnumerable<T> enumerable, int sizeHint = 0, MemoryAllocator<T>? allocator = null)
    {
        ArgumentNullException.ThrowIfNull(enumerable);

        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

        return enumerable switch
        {
            List<T> typedList => Span.Copy(CollectionsMarshal.AsSpan(typedList), allocator),
            T[] array => Span.Copy<T>(array, allocator),
            string str => ReinterpretCast<MemoryOwner<char>, MemoryOwner<T>>(str.AsSpan().Copy(Unsafe.As<MemoryAllocator<char>>(allocator))),
            ArraySegment<T> segment => Span.Copy<T>(segment.AsSpan(), allocator),
            ICollection<T> collection => collection.Count == 0 ? default : allocator is null ? CopyCollection(collection) : CopySlow(collection, collection.Count, allocator),
            IReadOnlyCollection<T> collection => collection.Count == 0 ? default : CopySlow(enumerable, collection.Count, allocator),
            _ => CopySlow(enumerable, GetSize(enumerable, sizeHint), allocator),
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        static MemoryOwner<T> CopyCollection(ICollection<T> collection)
        {
            Debug.Assert(collection is { Count: > 0 });

            var array = ArrayPool<T>.Shared.Rent(collection.Count);
            collection.CopyTo(array, 0);
            return new(ArrayPool<T>.Shared, array, collection.Count);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static MemoryOwner<T> CopySlow(IEnumerable<T> enumerable, int sizeHint, MemoryAllocator<T>? allocator)
        {
            using var writer = new BufferWriterSlim<T>(sizeHint, allocator);
            foreach (var item in enumerable)
                writer.Add(item);

            MemoryOwner<T> result;
            if (!writer.TryDetachBuffer(out result))
                result = writer.WrittenSpan.Copy(allocator);

            return result;
        }

        static int GetSize(IEnumerable<T> enumerable, int sizeHint)
            => enumerable.TryGetNonEnumeratedCount(out var result) ? result : sizeHint;
    }

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

        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

        using var buffer = new PooledBufferWriter<T> { BufferAllocator = allocator, Capacity = sizeHint };

        await foreach (var item in enumerable.WithCancellation(token).ConfigureAwait(false))
            buffer.Add(item);

        return buffer.DetachBuffer();
    }
}