using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Delimits a section of a list.
    /// </summary>
    /// <remarks>
    /// This collection is read-only and does not allow the addition or removal of elements. However individual elements
    /// of the list can be modified using indexer.
    /// </remarks>
    /// <typeparam name="T">The type of elements in the section.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ListSegment<T> : IList<T>, IReadOnlyList<T>
    {
        private readonly int startIndex;
        private readonly IList<T> list;

        /// <summary>
        /// Initializes a new segment of the list.
        /// </summary>
        /// <param name="list">The list containing the range of elements to delimit.</param>
        /// <param name="range">The range of elements.</param>
        public ListSegment(IList<T> list, Range range)
        {
            (startIndex, Count) = range.GetOffsetAndLength(list.Count);
            this.list = list;
        }

        /// <inheritdoc/>
        bool ICollection<T>.IsReadOnly => true;

        /// <summary>
        /// Gets the number of elements in the segment.
        /// </summary>
        public int Count { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ToAbsoluteIndex(int index)
            => index.IsBetween(0, Count, BoundType.LeftClosed) ? index + startIndex : throw new ArgumentOutOfRangeException(nameof(index));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ToRelativeIndex(ref int index)
        {
            index -= startIndex;
            return index.IsBetween(0, Count, BoundType.LeftClosed);
        }

        /// <summary>
        /// Gets or sets element at the specified index.
        /// </summary>
        /// <param name="index">The index of the element in this segment.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than zero; or greater than or equal to <see cref="Count"/>.</exception>
        public T this[int index]
        {
            get => list[ToAbsoluteIndex(index)];
            set => list[ToAbsoluteIndex(index)] = value;
        }

        /// <summary>
        /// Determines the index of a specific item.
        /// </summary>
        /// <param name="item">The object to locate in this section.</param>
        /// <returns>The index of <paramref name="item"/> in this section; otherwise, <c>-1</c>.</returns>
        public int IndexOf(T item)
        {
            var index = list.IndexOf(item);
            return ToRelativeIndex(ref index) ? index : -1;
        }

        /// <summary>
        /// Attempts to get span over elements in this segment.
        /// </summary>
        /// <param name="span">The span over elements in this segment.</param>
        /// <returns><see langword="true"/> if the underlying list supports conversion to span; otherwise <see langword="false"/>.</returns>
        public bool TryGetSpan(out Span<T> span)
        {
            switch (list)
            {
                case List<T> typedList:
                    span = CollectionsMarshal.AsSpan(typedList).Slice(startIndex, Count);
                    break;
                case T[] array:
                    span = new Span<T>(array, startIndex, Count);
                    break;
                default:
                    span = default;
                    return false;
            }

            return true;
        }

        /// <inheritdoc/>
        void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

        /// <inheritdoc/>
        void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

        /// <inheritdoc/>
        void ICollection<T>.Add(T item) => throw new NotSupportedException();

        /// <inheritdoc/>
        void ICollection<T>.Clear() => throw new NotSupportedException();

        /// <summary>
        /// Determines whether this section contains a specified value.
        /// </summary>
        /// <param name="item">The object to locate in this section.</param>
        /// <returns><see langword="true"/> if item is found in this section; otherwise, <see langword="false"/>.</returns>
        public bool Contains(T item) => IndexOf(item) >= 0;

        /// <summary>
        /// Copies the elements in this section to an array.
        /// </summary>
        /// <param name="array">The destination of the elements copied from this section.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            switch (list)
            {
                case List<T> typedList:
                    typedList.CopyTo(startIndex, array, arrayIndex, Count);
                    break;
                case T[] source:
                    Array.Copy(source, startIndex, array, arrayIndex, Count);
                    break;
                default:
                    for (var i = 0; i < Count; i++)
                        array[i] = list[i + startIndex];
                    break;
            }
        }

        /// <inheritdoc/>
        bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

        /// <summary>
        /// Gets enumerator of elements in this section.
        /// </summary>
        /// <returns>The enumerator of elements in this section.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            var enumerator = list.GetEnumerator();
            enumerator.Skip(startIndex);
            return enumerator.Limit(Count);
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
