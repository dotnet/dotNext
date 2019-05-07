using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace DotNext.Collections.Generic
{
    using Runtime.InteropServices;
    using System.Collections;

    /// <summary>
    /// Represents a strongly typed list of objects that is allocated in unmanaged memory.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    [Serializable]
    public struct UnmanagedList<T> : IList<T>, IDisposable, IUnmanagedList<T>, ISerializable
        where T : unmanaged
    {
        private const string CountSerData = "Count";
        private const string ArraySerData = "Array";
        private const int DefaultCapacity = 4;

        private UnmanagedArray<T> array;
        private int count;

        /// <summary>
        /// Allocates a new list in the unmanaged memory
        /// with desired initial capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity of the list.</param>
        public UnmanagedList(int capacity)
        {
            array = new UnmanagedArray<T>(capacity);
            count = 0;
        }

        private UnmanagedList(SerializationInfo info, StreamingContext context)
        {
            count = info.GetInt32(CountSerData);
            array = (UnmanagedArray<T>)info.GetValue(ArraySerData, typeof(UnmanagedArray<T>));
        }

        private UnmanagedList(int count, UnmanagedArray<T> array)
        {
            this.array = array;
            this.count = count;
        }

        /// <summary>
        /// Indicates that this list is empty.
        /// </summary>
        public bool IsEmpty => array.IsEmpty || count == 0;

        /// <summary>
        /// Gets capacity of this list.
        /// </summary>
        public int Capacity => (int)array.Length;

        private void EnsureCapacity(int capacity)
        {
            if (array.Length < capacity)
                array.Length = array.IsEmpty ? DefaultCapacity : checked(array.Length * 2).Max(capacity);
        }

        /// <summary>
        /// Gets or sets item in this list.
        /// </summary>
        /// <param name="index">The index of the item.</param>
        /// <returns>The list item.</returns>
        /// <exception cref="IndexOutOfRangeException">Index out of range.</exception>
        public T this[int index]
        {
            get => index >= 0 && index < count ? array[index] : throw new IndexOutOfRangeException(ExceptionMessages.InvalidIndexValue(count));

            set
            {
                if (index >= 0 && index < count)
                    array[index] = value;
                else
                    throw new IndexOutOfRangeException(ExceptionMessages.InvalidIndexValue(count));
            }
        }

        /// <summary>
        /// Gets number of elements in this list.
        /// </summary>
        public int Count => count;

        bool ICollection<T>.IsReadOnly => false;

        Pointer<T> IUnmanagedMemory<T>.Pointer => array;

        private Span<T> Span => (Span<T>)array.Slice(0, count);

        Span<T> IUnmanagedMemory<T>.Span => Span;

        long IUnmanagedMemory.Size => array.Size;

        /// <summary>
        /// Gets address of the unmanaged memory.
        /// </summary>
        public IntPtr Address => array.Address;

        /// <summary>
        /// Adds a new item to this collection.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        public void Add(T item)
        {
            EnsureCapacity(count + 1);
            array[count] = item;
            count += 1;
        }

        /// <summary>
        /// Removes all elements in this list.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            array.Clear();
            count = 0;
        }

        /// <summary>
        /// Determines whether an element is in this list.
        /// </summary>
        /// <param name="item">The item to be checked.</param>
        /// <returns><see langword="true"/>, if item is found in this list; otherwise, <see langword="false"/>.</returns>
        public bool Contains(T item) => IndexOf(item) >= 0;

        /// <summary>
        /// Copies the entire list to a compatible one-dimensional array, 
        /// starting at the specified index of the target array.
        /// </summary>
        /// <param name="output">The destination of the elements copied from this list.</param>
        /// <param name="arrayIndex">The index in <paramref name="output"/> at which copying begins.</param>
        /// <param name="count">The number of elements to copy.</param>
        public void CopyTo(T[] output, int arrayIndex, int count)
            => array.WriteTo(output, arrayIndex, this.count.Min(count));

        /// <summary>
        /// Copies the entire list to a compatible one-dimensional array, 
        /// starting at the specified index of the target array.
        /// </summary>
        /// <param name="output">The destination of the elements copied from this list.</param>
        /// <param name="arrayIndex">The index in <paramref name="output"/> at which copying begins.</param>
        public void CopyTo(T[] output, int arrayIndex)
            => CopyTo(output, arrayIndex, count);

        /// <summary>
        /// Copies the entire list to a compatible one-dimensional array, 
        /// starting at the specified index of the target array.
        /// </summary>
        /// <param name="output">The destination of the elements copied from this list.</param>
        /// <param name="arrayIndex">The index in <paramref name="output"/> at which copying begins.</param>
        /// <param name="count">The number of elements to copy.</param>
        public void CopyTo(UnmanagedArray<T> output, int arrayIndex, int count)
            => array.WriteTo(output, arrayIndex, this.count.Min(count));

        /// <summary>
        /// Copies the entire list to a compatible one-dimensional array, 
        /// starting at the specified index of the target array.
        /// </summary>
        /// <param name="output">The destination of the elements copied from this list.</param>
        /// <param name="arrayIndex">The index in <paramref name="output"/> at which copying begins.</param>
        public void CopyTo(UnmanagedArray<T> output, int arrayIndex)
            => CopyTo(output, arrayIndex, count);

        /// <summary>
        /// Allocates a new array and copies items from this list
        /// into the allocated array.
        /// </summary>
        /// <returns>The array containing items from this list.</returns>
        public UnmanagedArray<T> ToArray()
        {
            var result = default(UnmanagedArray<T>);
            if (count > 0)
            {
                result = new UnmanagedArray<T>(count);
                array.WriteTo(result);
            }
            return result;
        }

        /// <summary>
        /// Gets enumerator over all items in this list.
        /// </summary>
        /// <returns>The enumerator over all items in this list.</returns>
        public Pointer<T>.Enumerator GetEnumerator()
        {
            Pointer<T> pointer = array;
            return pointer.GetEnumerator(count);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first occurrence within the entire list.
        /// </summary>
        /// <param name="item">The object to locate in the list.</param>
        /// <param name="comparer">The equality check function used to compare the given item with the list item.</param>
        /// <returns>The zero-based index of the first occurence of the given item; otherwise, -1.</returns>
        public int IndexOf(T item, IEqualityComparer<T> comparer) => (int)array.IndexOf(item, 0, count, comparer);

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first occurrence within the entire list.
        /// </summary>
        /// <param name="item">The object to locate in the list.</param>
        /// <returns>The zero-based index of the first occurence of the given item; otherwise, -1.</returns>
        public int IndexOf(T item) => IndexOf(item, EqualityComparer<T>.Default);

        /// <summary>
        /// Searches item matching to the given predicate in this list, and returns 
        /// the index of its first occurrence.
        /// </summary>
        /// <param name="predicate">The predicate used to check item.</param>
        /// <returns>The index of the matched item; or -1, if value doesn't exist in this list.</returns>
        public int Find(Predicate<T> predicate) => (int)array.Find(predicate, 0, count);

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the last occurrence within the entire list.
        /// </summary>
        /// <param name="item">The object to locate in the list.</param>
        /// <returns>The zero-based index of the last occurence of the given item; otherwise, -1.</returns>
        public int LastIndexOf(T item) => LastIndexOf(item, EqualityComparer<T>.Default);

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the last occurrence within the entire list.
        /// </summary>
        /// <param name="item">The object to locate in the list.</param>
        /// <param name="comparer">The equality check function used to compare the given item with the list item.</param>
        /// <returns>The zero-based index of the last occurence of the given item; otherwise, -1.</returns>
        public int LastIndexOf(T item, IEqualityComparer<T> comparer) => (int)array.LastIndexOf(item, 0, count, comparer);

        /// <summary>
        /// Searches item matching to the given predicate in this list, and returns 
        /// the index of its last occurrence.
        /// </summary>
        /// <param name="predicate">The predicate used to check item.</param>
        /// <returns>The index of the matched item; or -1, if value doesn't exist in this list.</returns>
        public int FindLast(Predicate<T> predicate) => (int)array.FindLast(predicate, 0, count);

        /// <summary>
        /// Uses a binary search algorithm to locate a specific element in the sorted list.
        /// </summary>
        /// <param name="item">The value to locate.</param>
        /// <param name="comparison">The comparison algorithm.</param>
        /// <returns>The index of the item; or -1, if item doesn't exist in the list.</returns>
        public int BinarySearch(T item, IComparer<T> comparison) => (int)array.BinarySearch(item, 0, count, comparison);

        /// <summary>
        /// Uses a binary search algorithm to locate a specific element in the sorted list.
        /// </summary>
        /// <param name="item">The value to locate.</param>
        /// <returns>The index of the item; or -1, if item doesn't exist in the list.</returns>
        public int BinarySearch(T item) => (int)array.BinarySearch(item, 0, count, Comparer<T>.Default);

        /// <summary>
        /// Sorts the items in this list according with given comparer.
        /// </summary>
        /// <param name="comparison">The items comparison algorithm.</param>
        public void Sort(IComparer<T> comparison) => array.Sort(0, count, comparison);

        /// <summary>
        /// Sorts the items in this list in ascending order.
        /// </summary>
        public void Sort() => Sort(Comparer<T>.Default);

        /// <summary>
        /// Inserts an element into the list at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which item should be inserted.</param>
        /// <param name="item">The value to insert.</param>
        public void Insert(int index, T item)
        {
            if (index < 0 || index > count)
                throw new ArgumentOutOfRangeException(nameof(index), index, ExceptionMessages.InvalidIndexValue(count));
            EnsureCapacity(count + 1);
            if (index < count)
            {
                var pointer = array + index;
                pointer.WriteTo(pointer + 1, count - index);
            }
            array[index] = item;
            count += 1;
        }

        /// <summary>
        /// Removes the first occurrence of a specific value from the list.
        /// </summary>
        /// <param name="item">The value to remove from this list.</param>
        /// <returns><see langword="true"/>, if item is successfully removed; otherwise, <see langword="false"/>.</returns>
        public bool Remove(T item)
        {
            var index = IndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Removes the last occurrence of a specific value from the list.
        /// </summary>
        /// <param name="item">The value to remove from this list.</param>
        /// <returns><see langword="true"/>, if item is successfully removed; otherwise, <see langword="false"/>.</returns>
        public bool RemoveLast(T item)
        {
            var index = LastIndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Removes the element at the specified index from the list.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">The index is invalid.</exception>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException(nameof(index), index, ExceptionMessages.InvalidIndexValue(count));
            var pointer = array + index + 1;
            pointer.WriteTo(pointer - 1, count - index);
            count -= 1;
        }

        /// <summary>
        /// Sets the capacity to the actual number of elements in this list.
        /// </summary>
        public void TrimExcess()
        {
            if (count > 0 && array.Length > 0)
                array.Length = count;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Releases unmanaged memory associated with this list.
        /// </summary>
        public void Dispose()
        {
            array.Dispose();
            this = default;
        }

        Pointer<U> IUnmanagedMemory.ToPointer<U>() => array.ToPointer<U>();

        /// <summary>
        /// Returns deep copy of this list.
        /// </summary>
        /// <returns>The deep copy of this list.</returns>
        public UnmanagedList<T> Copy() => new UnmanagedList<T>(count, array.Copy());

        object ICloneable.Clone() => Copy();

        /// <summary>
        /// Provides unstructured access to the unmanaged memory utilized by the list.
        /// </summary>
        /// <param name="list">The list allocated in the unmanaged memory.</param>
        public static implicit operator UnmanagedMemory(UnmanagedList<T> list) => new UnmanagedMemory(list.Address, (long)list.count * Pointer<T>.Size);

        /// <summary>
        /// Returns span over elements in the list.
        /// </summary>
        /// <param name="list">The unmanaged list to be converted into span.</param>
        public static implicit operator Span<T>(UnmanagedList<T> list) => list.Span;

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(CountSerData, count);
            info.AddValue(ArraySerData, array);
        }
    }
}
