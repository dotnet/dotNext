using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotNext.Collections.Concurrent
{
    /// <summary>
    /// A thread-safe variant of <see cref="List{T}"/> in which all mutative operations are implemented by making a snapshot copy of the underlying array. 
    /// </summary>
    /// <remarks>
    /// This list is perfrect for scenarios when reads are frequent and concurrent but writes not. Read operation never cause synchronization of the list.
    /// The enumerator doesn't track additions, removals or changes in the list since enumerator was created. As a result, dirty reads are possible.
    /// </remarks>
    /// <typeparam name="T">The type of elements held in this collection.</typeparam>
    [Serializable]
    public class CopyOnWriteList<T>: IReadOnlyList<T>, IList<T>
    {
        private volatile T[] backingStore;

        /// <summary>
        /// Initializes a new list containing elements copied from the given read-only collection.
        /// </summary>
        /// <param name="collection">The source of the items in the creating list.</param>
        public CopyOnWriteList(IReadOnlyCollection<T> collection)
        {
            backingStore = new T[collection.Count];
            var index = 0L;
            foreach (var item in collection)
                backingStore[index++] = item;
        }

        /// <summary>
        /// Initializes a new empty list.
        /// </summary>
        public CopyOnWriteList() => backingStore = Array.Empty<T>();

        bool ICollection<T>.IsReadOnly => false;

        int ICollection<T>.Count => backingStore.Length;

        int IReadOnlyCollection<T>.Count => backingStore.Length;

        /// <summary>
        /// Gets the number of elements in this list.
        /// </summary>
        public long Count => backingStore.LongLength;

        /// <summary>
        /// Gets or sets list item.
        /// </summary>
        /// <param name="index">The index of the list item.</param>
        /// <returns>The list item.</returns>
        /// <exception cref="IndexOutOfRangeException">Invalid index of the item in this list.</exception>
        public T this[long index]
        {
            get => backingStore[index];
            [MethodImpl(MethodImplOptions.Synchronized)]
            set => backingStore[index] = value;
        }

        T IList<T>.this[int index]
        {
            get => this[index];
            set => this[index] = value;
        }

        T IReadOnlyList<T>.this[int index] => this[index];

        private static T[] Add(T[] backingStore, T item)
        {
            var index = backingStore.LongLength;
            var newStore = new T[index + 1L];
            backingStore.CopyTo(newStore, 0L);
            newStore[index] = item;
            return newStore;
        }

        /// <summary>
        /// Adds an item to the end of this list.
        /// </summary>
        /// <remarks>
        /// This operation causes reallocation of underlying array.
        /// </remarks>
        /// <param name="item">The item to be added to the end of this list. <see langword="null"/> is allowed.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Add(T item) => backingStore = Add(backingStore, item);

        /// <summary>
        /// Returns the zero-based index of the first occurrence of a value in this list.
        /// </summary>
        /// <param name="item">The object to locate in this list.</param>
        /// <returns>The zero-based index of the first occurrence of <paramref name="item"/>, if found; otherwise, -1.</returns>
        public int IndexOf(T item) => ((IList<T>)backingStore).IndexOf(item);

        /// <summary>
        /// Determines whether an item is in this list.
        /// </summary>
        /// <param name="item">The object to locate in this list.</param>
        /// <returns><see langword="true"/>, if <paramref name="item"/> is found in this list; otherwise, <see langword="false"/>.</returns>
        public bool Contains(T item) => ((IList<T>)backingStore).Contains(item);

        /// <summary>
        /// Copies the entire list to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from this list.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex) => backingStore.CopyTo(array, arrayIndex);

        /// <summary>
        /// Removes all items from this list.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear() => backingStore = Array.Empty<T>();

        private static T[] RemoveAt(T[] backingStore, long index)
        {
            if (index < 0L || index >= backingStore.LongLength)
                throw new ArgumentOutOfRangeException(nameof(index));
            else if (backingStore.LongLength == 1L)
                return Array.Empty<T>();
            else
            {
                var newStore = new T[backingStore.LongLength - 1L];
                Array.Copy(backingStore, 0L, newStore, 0L, index);
                Array.Copy(backingStore, index + 1L, newStore, index, backingStore.LongLength - index - 1L);
                return newStore;
            }
        }

        /// <summary>
        /// Removes the element at the specified index of this list.
        /// </summary>
        /// <remarks>
        /// This operation causes reallocation of underlying array.
        /// </remarks>
        /// <param name="index">The zero-based index of the element to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is incorrect.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveAt(long index) => backingStore = RemoveAt(backingStore, index);

        /// <summary>
        /// Removes the first occurrence of an item from this list.
        /// </summary>
        /// <remarks>
        /// This operation causes reallocation of underlying array.
        /// </remarks>
        /// <param name="item">The item to remove from this list.</param>
        /// <returns><see langword="true"/> if item is successfully removed; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Remove(T item)
        {
            var index = IndexOf(item);
            if (index >= 0L)
            {
                RemoveAt(index);
                return true;
            }
            else
                return false;
        }

        private static T[] Insert(T[] backingStore, long index, T item)
        {
            if (index < 0L || index > backingStore.LongLength)
                throw new ArgumentOutOfRangeException(nameof(index));
            var newStore = new T[backingStore.LongLength + 1L];
            Array.Copy(backingStore, 0L, newStore, 0L, index);
            Array.Copy(backingStore, index, newStore, index + 1L, backingStore.LongLength - index);
            return newStore;
        }

        /// <summary>
        /// Inserts an element into this list at the specified index.
        /// </summary>
        /// <remarks>
        /// This operation causes reallocation of underlying array.
        /// </remarks>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The object to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is incorrect.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Insert(long index, T item) => backingStore = Insert(backingStore, index, item);

        private static T[] RemoveAll(T[] backingStore, Predicate<T> match, out long count)
        {
            if (backingStore.LongLength == 0L)
            {
                count = 0L;
                return backingStore;
            }
            var newLength = 0L;
            var tempArray = new T[backingStore.LongLength];
            foreach (var item in backingStore)
                if (!match(item))
                    tempArray[newLength++] = item;
            count = backingStore.LongLength - newLength;
            if (count == 0L)
                return backingStore;
            else if (newLength == 0L)
                return Array.Empty<T>();
            else
            {
                backingStore = new T[newLength];
                Array.Copy(tempArray, 0L, backingStore, 0L, newLength);
                return backingStore;
            }
        }

        /// <summary>
        /// Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <returns>The number of elements removed from this list.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public long RemoveAll(Predicate<T> match)
        {
            backingStore = RemoveAll(backingStore, match, out var count);
            return count;
        }

        void IList<T>.Insert(int index, T item) => Insert(index, item);

        void IList<T>.RemoveAt(int index) => RemoveAt(index);

        /// <summary>
        /// Gets enumerator over snapshot of this list.
        /// </summary>
        /// <returns>The enumerator over snapshot of this list.</returns>
        public IEnumerator<T> GetEnumerator() => ((IReadOnlyList<T>)backingStore).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
