using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent
{
    using static Runtime.InteropServices.Memory;

    /// <summary>
    /// A thread-safe variant of <see cref="List{T}"/> in which all mutative operations are implemented by making a snapshot copy of the underlying array. 
    /// </summary>
    /// <remarks>
    /// This list is perfect for scenarios when reads are frequent and concurrent but writes not. Read operation never cause synchronization of the list.
    /// The enumerator doesn't track additions, removals or changes in the list since enumerator was created. As a result, dirty reads are possible.
    /// </remarks>
    /// <typeparam name="T">The type of elements held in this collection.</typeparam>
    [Serializable]
    public class CopyOnWriteList<T> : IReadOnlyList<T>, IList<T>, ICloneable
    {
        /// <summary>
        /// Represents enumerator over elements in the list.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator : IEnumerator<T>
        {
            private const long InitialPosition = -1L;
            private readonly T[] snapshot;
            private long position;

            internal Enumerator(T[] backingStore)
            {
                snapshot = backingStore;
                position = InitialPosition;
            }

            /// <summary>
            /// Gets the element in the collection at the current position of the enumerator.
            /// </summary>
            public ref readonly T Current => ref snapshot.GetReadonlyRef(position);

            T IEnumerator<T>.Current => Current;

            object IEnumerator.Current => Current;

            void IDisposable.Dispose() => this = default;

            /// <summary>
            /// Advances the enumerator to the next element.
            /// </summary>
            /// <returns><see lanword="true"/> if the enumerator was successfully advanced to the next element; <see langword="false"/> if the enumerator has passed the end of the collection.</returns>
            public bool MoveNext() => ++position < snapshot.LongLength;

            void IEnumerator.Reset() => position = InitialPosition;
        }

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

        private CopyOnWriteList(T[] backingStore) => this.backingStore = backingStore;

        /// <summary>
        /// Creates copy of this list.
        /// </summary>
        /// <returns>The copy of this list.</returns>
        public CopyOnWriteList<T> Clone()
            => new CopyOnWriteList<T>(Snapshot.ToArray());

        object ICloneable.Clone() => Clone();

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
        /// Gets the snapshot view of the current list.
        /// </summary>
        public ReadOnlyMemory<T> Snapshot => new ReadOnlyMemory<T>(backingStore);

        /// <summary>
        /// Gets or sets list item.
        /// </summary>
        /// <param name="index">The index of the list item.</param>
        /// <returns>The list item.</returns>
        /// <exception cref="IndexOutOfRangeException">Invalid index of the item in this list.</exception>
        public T this[long index]
        {
            get => backingStore[index];
            set
            {
                var newArray = Snapshot.ToArray();
                newArray[index] = value;
                ReplaceStore(newArray);
            }
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
        public int IndexOf(T item) => Array.IndexOf(backingStore, item);

        /// <summary>
        /// Returns the zero-based index of the last occurrence of a value in this list.
        /// </summary>
        /// <param name="item">The object to locate in this list.</param>
        /// <returns>The zero-based index of the last occurrence of <paramref name="item"/>, if found; otherwise, -1.</returns>
        public int LastIndexOf(T item) => Array.LastIndexOf(backingStore, item);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the first occurrence within the entire list.
        /// </summary>
        /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
        /// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type <typeparamref name="T"/>.</returns>
        public T Find(Predicate<T> match) => Array.Find(backingStore, match);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the last occurrence within the entire list.
        /// </summary>
        /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
        /// <returns>The last element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type <typeparamref name="T"/>.</returns>
        public T FindLast(Predicate<T> match) => Array.FindLast(backingStore, match);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the entire list
        /// </summary>
        /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
        /// <returns>The zero-based index of the first occurrence of an element that matches the conditions defined by match, if found; otherwise, -1.</returns>
        public int FindIndex(Predicate<T> match) => Array.FindIndex(backingStore, match);

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the entire list
        /// </summary>
        /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
        /// <returns>The zero-based index of the last occurrence of an element that matches the conditions defined by match, if found; otherwise, -1.</returns>
        public int FindLastIndex(Predicate<T> match) => Array.FindLastIndex(backingStore, match);

        /// <summary>
        /// Determines whether an item is in this list.
        /// </summary>
        /// <param name="item">The object to locate in this list.</param>
        /// <returns><see langword="true"/>, if <paramref name="item"/> is found in this list; otherwise, <see langword="false"/>.</returns>
        public bool Contains(T item) => IndexOf(item) >= 0;

        /// <summary>
        ///  Determines whether this list contains elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The predicate that defines the conditions of the elements to search for.</param>
        /// <returns><see langword="true"/> if array contains one or more elements that match the conditions defined by the specified predicate; otherwise, <see langword="false"/>.</returns>
        public bool Exists(Predicate<T> match) => Array.Exists(backingStore, match);

        /// <summary>
        /// Copies the entire list to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from this list.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex) => backingStore.CopyTo(array, arrayIndex);

        [MethodImpl(MethodImplOptions.Synchronized)]
        private T[] ReplaceStore(T[] newStore)
        {
            var oldStore = backingStore;
            backingStore = newStore;
            return oldStore;
        }

        /// <summary>
        /// Replaces all items in this list with given array.
        /// </summary>
        /// <param name="array">The array of new items.</param>
        public void Set(ReadOnlySpan<T> array) => ReplaceStore(array.ToArray());

        /// <summary>
        /// Replaces all items in this list with new items.
        /// </summary>
        /// <typeparam name="G">The type of source items.</typeparam>
        /// <param name="items">The source items to be converted and placed into this list.</param>
        /// <param name="converter">The convert of source items.</param>
        public void Set<G>(ICollection<G> items, in ValueFunc<G, T> converter)
        {
            if (items.Count == 0)
            {
                ReplaceStore(Array.Empty<T>());
                return;
            }
            var array = new T[items.Count];
            var index = 0L;
            foreach (var item in items)
                array[index++] = converter.Invoke(item);
            ReplaceStore(array);
        }

        /// <summary>
        /// Replaces all items in this list with new items.
        /// </summary>
        /// <typeparam name="G">The type of source items.</typeparam>
        /// <param name="items">The source items to be converted and placed into this list.</param>
        /// <param name="converter">The convert of source items.</param>
        public void Set<G>(ICollection<G> items, Converter<G, T> converter)
            => Set(items, converter.AsValueFunc(true));

        /// <summary>
        /// Removes all items from this list.
        /// </summary>
        public void Clear()
        {
            var oldStore = ReplaceStore(Array.Empty<T>());
            Array.Clear(oldStore, 0, oldStore.Length);  //help GC
        }

        /// <summary>
        /// Removes all items from this list and performs cleanup operation for each item.
        /// </summary>
        /// <param name="cleaner">The action used to clean item from this list.</param>
        public void Clear(in ValueAction<T> cleaner)
        {
            var oldStore = ReplaceStore(Array.Empty<T>());
            for (var i = 0L; i < oldStore.LongLength; i++)
            {
                ref var item = ref oldStore[i];
                cleaner.Invoke(item);
                item = default;
            }
        }

        /// <summary>
        /// Removes all items from this list and performs cleanup operation for each item.
        /// </summary>
        /// <param name="cleaner">The action used to clean item from this list.</param>
        public void Clear(Action<T> cleaner) => Clear(new ValueAction<T>(cleaner, true));

        /// <summary>
        /// Removes the element at the specified index of this list.
        /// </summary>
        /// <remarks>
        /// This operation causes reallocation of underlying array.
        /// </remarks>
        /// <param name="index">The zero-based index of the element to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is incorrect.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveAt(long index) => backingStore = backingStore.RemoveAt(index);

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
        public void Insert(long index, T item) => backingStore = backingStore.Insert(item, index);

        /// <summary>
        /// Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <returns>The number of elements removed from this list.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public long RemoveAll(ValueFunc<T, bool> match)
        {
            backingStore = backingStore.RemoveAll(match, out var count);
            return count;
        }

        /// <summary>
        /// Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <returns>The number of elements removed from this list.</returns>
        public long RemoveAll(Predicate<T> match) => RemoveAll(match.AsValueFunc(true));

        /// <summary>
        /// Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <param name="callback">The delegate that is used to accept removed items.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveAll(in ValueFunc<T, bool> match, in ValueAction<T> callback)
            => backingStore = backingStore.RemoveAll(match, callback);

        /// <summary>
        /// Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <param name="callback">The delegate that is used to accept removed items.</param>
        public void RemoveAll(Predicate<T> match, Action<T> callback) => RemoveAll(match.AsValueFunc(true), new ValueAction<T>(callback));

        void IList<T>.Insert(int index, T item) => Insert(index, item);

        void IList<T>.RemoveAt(int index) => RemoveAt(index);

        /// <summary>
        /// Gets enumerator over snapshot of this list.
        /// </summary>
        /// <returns>The enumerator over snapshot of this list.</returns>
        public Enumerator GetEnumerator() => new Enumerator(backingStore);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => backingStore.GetEnumerator();

        /// <summary>
        /// Obtains snapshot of the underlying array in the form of the span.
        /// </summary>
        /// <param name="list">The list to be converted into span.</param>
        public static explicit operator ReadOnlySpan<T>(CopyOnWriteList<T> list) => list is null ? default : new ReadOnlySpan<T>(list.backingStore);
    }
}
