using System.Buffers;
using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Collections.Generic
{
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
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="sizeHint"/> is less than zero.</exception>
        public static MemoryOwner<T> Copy<T>(this IEnumerable<T> enumerable, int sizeHint = 0, MemoryAllocator<T>? allocator = null)
        {
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

            static MemoryOwner<T> CopyCollection(ICollection<T> collection)
            {
                var array = ArrayPool<T>.Shared.Rent(collection.Count);
                collection.CopyTo(array, 0);
                return new(ArrayPool<T>.Shared, array, collection.Count);
            }

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
    }
}