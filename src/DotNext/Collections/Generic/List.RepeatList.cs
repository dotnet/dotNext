using System.Collections;

namespace DotNext.Collections.Generic;

public static partial class List
{
    private sealed class RepeatList<T>(T item, int count) : IReadOnlyList<T>
    {
        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < count; i++)
                yield return item;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => count;

        public T this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)count, nameof(index));

                return item;
            }
        }
    }
}