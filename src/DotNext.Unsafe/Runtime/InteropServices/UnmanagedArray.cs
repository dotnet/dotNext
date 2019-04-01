using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.InteropServices
{
    using Reflection;

	/// <summary>
	/// Represents typed array allocated in the unmanaged heap.
	/// </summary>
	/// <remarks>
    /// Allocated memory is not controlled by Garbage Collector.
	/// Therefore, it's developer responsibility to release unmanaged memory using <see cref="IDisposable.Dispose"/> call.
    /// </remarks>
	/// <typeparam name="T">Array element type.</typeparam>
	public unsafe struct UnmanagedArray<T> : IEquatable<UnmanagedArray<T>>, IUnmanagedList<T>
		where T : unmanaged
	{
		/// <summary>
		/// Represents GC-friendly reference to the unmanaged array.
		/// </summary>
		/// <remarks>
		/// Unmanaged array allocated using handle can be reclaimed by GC automatically.
		/// </remarks>
		public sealed class Handle : UnmanagedMemoryHandle<T>
		{

			private Handle(UnmanagedArray<T> array, bool ownsHandle)
				: base(array, ownsHandle)
			{
				Length = array.Length;
			}

			/// <summary>
			/// Initializes a new unmanaged array and associate it with the handle.
			/// </summary>
            /// <remarks>
            /// The handle instantiated with this constructor has ownership over unmanaged memory.
            /// Unmanaged memory will be released when Garbage Collector reclaims instance of this handle
            /// or <see cref="Dispose()"/> will be called directly.
            /// </remarks>
			/// <param name="length">Array length.</param>
            /// <param name="zeroMem">Sets all bytes of allocated memory to zero.</param>
			public Handle(long length, bool zeroMem = true)
				: this(new UnmanagedArray<T>(length), true)
			{

			}

            /// <summary>
            /// Initializes a new handle for the given array.
            /// </summary>
            /// <remarks>
            /// The handle instantiated with this constructor doesn't have ownership over unmanaged memory.
            /// </remarks>
            /// <param name="array">The unmanaged array.</param>
			public Handle(UnmanagedArray<T> array)
				: this(array, false)
			{
			}

            private protected override UnmanagedMemoryHandle Clone() => new Handle(Conversion<Handle, UnmanagedArray<T>>.Converter(this).Copy(), true);

            /// <summary>
            /// Obtains span object pointing to the allocated unmanaged array.
            /// </summary>
            public override Span<T> Span => new UnmanagedArray<T>(handle, Length);

            /// <summary>
            /// Gets number of elements in the unmanaged array.
            /// </summary>
            public long Length { get; }

            /// <summary>
            /// Gets number of bytes allocated for the unmanaged array, in bytes.
            /// </summary>
            public override long Size => Length * Pointer<T>.Size;

            /// <summary>
            /// Releases referenced unmanaged memory.
            /// </summary>
            /// <returns><see langword="true"/>, if this handle is valid; otherwise, <see langword="false"/>.</returns>
			protected override bool ReleaseHandle() => UnmanagedMemory.Release(handle);

			/// <summary>
			/// Converts handle into unmanaged array reference.
			/// </summary>
			/// <param name="handle">A handle to convert.</param>
			/// <exception cref="ObjectDisposedException">Handle is closed.</exception>
			public static implicit operator UnmanagedArray<T>(Handle handle)
			{
				if (handle is null)
					return default;
				else if (handle.IsClosed)
					throw handle.HandleClosed();
				else
					return new UnmanagedArray<T>(handle.handle, handle.Length);
			}
		}

        /// <summary>
        /// Represents empty array.
        /// </summary>
        public static UnmanagedArray<T> Empty => default(UnmanagedArray<T>);
        
        private readonly long length;
		private readonly Pointer<T> pointer;

		/// <summary>
		/// Allocates a new array in the unmanaged memory of the specified length.
		/// </summary>
		/// <param name="length">Array length. Cannot be less or equal than zero.</param>
        /// <param name="zeroMem">Sets all bytes of allocated memory to zero.</param>      
		/// <exception cref="ArgumentOutOfRangeException">Invalid length.</exception>
		public UnmanagedArray(long length, bool zeroMem = true)
		{
			if (length < 0)
				throw new ArgumentOutOfRangeException(nameof(length), length, ExceptionMessages.ArrayNegativeLength);
			else if ((this.length = length) > 0L)
            {
                var size = length * Pointer<T>.Size;
                pointer = new Pointer<T>(UnmanagedMemory.Alloc(size, zeroMem));
                GC.AddMemoryPressure(size);
            }
			else
				pointer = Pointer<T>.Null;
		}

		private UnmanagedArray(Pointer<T> pointer, long length)
		{
			this.length = length;
			this.pointer = pointer;
		}

		private UnmanagedArray(IntPtr pointer, long length)
			: this((T*)pointer, length)
		{
		}

        /// <summary>
        /// Indicates that this array is empty.
        /// </summary>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => length == 0L || pointer == Pointer<T>.Null;
        }

		/// <summary>
		/// Gets or sets length of this array.
		/// </summary>
        /// <remarks>
        /// If length is changed then the contents of this array have been copied to the array, and this array has been freed.      
        /// </remarks> 
        /// <exception cref="ArgumentOutOfRangeException">The new length value is invalid.</exception>           
		public long Length
        {
            get => length;
            set
            {
                if(value <= 0L)
                    throw new ArgumentOutOfRangeException(nameof(value));
                else if(value == length)
                    return;
                else if(IsEmpty)
                    this = new UnmanagedArray<T>(value);
                else
                    this = new UnmanagedArray<T>(UnmanagedMemory.Realloc(pointer, Pointer<T>.Size * value), value);
            }
        }

        int IReadOnlyCollection<T>.Count => (int)Length;

		/// <summary>
		/// Size of allocated memory, in bytes.
		/// </summary>
		public long Size => Pointer<T>.Size * Length;

        Pointer<T> IUnmanagedMemory<T>.Pointer => pointer;

        /// <summary>
        /// Gets address of the unmanaged memory.
        /// </summary>
        public IntPtr Address => pointer.Address;

        Span<T> IUnmanagedMemory<T>.Span => this;

        /// <summary>
        /// Fills the elements of this array with a specified value.
        /// </summary>
        /// <param name="value">The value to assign to each element of the array.</param>
        /// <exception cref="NullPointerException">This array is not allocated.</exception>
        public void Fill(T value) => pointer.Fill(value, length);

        /// <summary>
        /// Forms a slice out of the current span that begins at a specified index.
        /// </summary>
        /// <remarks>
        /// This method doesn't allocate a new array, just returns a view of the current array.
        /// Do not call <see cref="UnmanagedArray{T}.Dispose"/> for the returned array.
        /// </remarks>
        /// <param name="startIndex">The index at which to begin this slice.</param>
        /// <returns>An array that consists of all elements of the current array from <paramref name="startIndex"/> to the end of the array.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> has invalid value.</exception>
        public UnmanagedArray<T> Slice(long startIndex) => Slice(startIndex, Length);

        /// <summary>
        /// Forms a slice out of the current sarraypan starting at a specified index for a specified length.
        /// </summary>
        /// <remarks>
        /// This method doesn't allocate a new array, just returns a view of the current array.
        /// Do not call <see cref="UnmanagedArray{T}.Dispose"/> for the returned array.
        /// </remarks>
        /// <param name="startIndex">The index at which to begin this slice.</param>
        /// <param name="count">The desired length for the slice.</param>
        /// <returns>An array that consists of <paramref name="count"/> elements from the current array starting at <paramref name="startIndex"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="count"/> has invalid value.</exception>
        public UnmanagedArray<T> Slice(long startIndex, long count)
        {
            if(startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            else if(count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if(startIndex >= Length || count == 0L)
                return Empty;
            else
                return new UnmanagedArray<T>(pointer + startIndex, count.UpperBounded(Length - startIndex));
        }

        /// <summary>
        /// Searches item matching to the given predicate in a range of elements of the unmanaged array, and returns 
        /// the index of its last occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <param name="predicate">The predicate used to check item.</param>
        /// <param name="startIndex">The starting index of the search.</param>
        /// <param name="count">The number of elements to search.</param>
        /// <returns>The index of the matched item; or -1, if value doesn't exist in this array.</returns>
        public long FindLast(Predicate<T> predicate, long startIndex, long count)
        {
            if (count == 0 || IsEmpty)
                return -1;
            else
                count = count.UpperBounded(Length);
            for (var index = count - 1; index >= startIndex; index--)
                if (predicate((pointer + index).Value))
                    return index;
            return -1;
        }

        /// <summary>
        /// Searches item matching to the given predicate in a range of elements of the unmanaged array, and returns 
        /// the index of its last occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <param name="predicate">The predicate used to check item.</param>
        /// <param name="startIndex">The starting index of the search.</param>
        /// <returns>The index of the matched item; or -1, if value doesn't exist in this array.</returns>
        public long FindLast(Predicate<T> predicate, long startIndex) => FindLast(predicate, startIndex, Length);

        /// <summary>
        /// Searches item matching to the given predicate in the unmanaged array, and returns 
        /// the index of its last occurrence.
        /// </summary>
        /// <param name="predicate">The predicate used to check item.</param>
        /// <returns>The index of the matched item; or -1, if value doesn't exist in this array.</returns>
        public long FindLast(Predicate<T> predicate) => FindLast(predicate, 0);
                
        /// <summary>
        /// Searches for the specified object in a range of elements of the unmanaged array, and returns 
        /// the index of its last occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <param name="item">The value to locate in this array.</param>
        /// <param name="startIndex">The starting index of the search.</param>
        /// <param name="count">The number of elements to search.</param>
        /// <param name="comparer">The custom comparer used to compare array element with the given value.</param>
        /// <returns>The index of the last occurrence of value; or -1, if value doesn't exist in this array.</returns>
        public long LastIndexOf(T item, long startIndex, long count, IEqualityComparer<T> comparer)
        {
            if (count == 0 || IsEmpty)
                return -1;
            else
                count = count.UpperBounded(Length);
            for (var index = count - 1; index >= startIndex; index--)
                if (comparer.Equals((pointer + index).Value, item))
                    return index;
            return -1;
        }

        /// <summary>
        /// Searches for the specified object in a range of elements of the unmanaged array, and returns 
        /// the index of its last occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <param name="item">The value to locate in this array.</param>
        /// <param name="startIndex">The starting index of the search.</param>
        /// <param name="comparer">The custom comparer used to compare array element with the given value.</param>
        /// <returns>The index of the last occurrence of value; or -1, if value doesn't exist in this array.</returns>
        public long LastIndexOf(T item, long startIndex, IEqualityComparer<T> comparer) => LastIndexOf(item, startIndex, Length, comparer);

        /// <summary>
        /// Searches for the specified object in a range of elements of the unmanaged array, and returns 
        /// the index of its last occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="ValueType{T}.BitwiseComparer"/> comparer to compare elements in this array.
        /// </remarks>
        /// <param name="item">The value to locate in this array.</param>
        /// <param name="startIndex">The starting index of the search.</param>
        /// <returns>The index of the last occurrence of value; or -1, if value doesn't exist in this array.</returns>
        public long LastIndexOf(T item, long startIndex) => LastIndexOf(item, startIndex, ValueType<T>.BitwiseComparer.Instance);

        /// <summary>
        /// Searches for the specified object in a range of elements of the unmanaged array, and returns 
        /// the index of its last occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="ValueType{T}.BitwiseComparer"/> comparer
        /// to compare elements in this array.
        /// </remarks>
        /// <param name="item">The value to locate in this array.</param>
        /// <returns>The index of the last occurrence of value; or -1, if value doesn't exist in this array.</returns>
        public long LastIndexOf(T item) => LastIndexOf(item, 0);

        /// <summary>
        /// Searches item matching to the given predicate in a range of elements of the unmanaged array, and returns 
        /// the index of its first occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <param name="predicate">The predicate used to check item.</param>
        /// <param name="startIndex">The starting index of the search.</param>
        /// <param name="count">The number of elements to search.</param>
        /// <returns>The index of the matched item; or -1, if value doesn't exist in this array.</returns>
        public long Find(Predicate<T> predicate, long startIndex, long count)
        {
            if (count == 0 || IsEmpty)
                return -1;
            else
                count = count.UpperBounded(Length);
            for (var index = startIndex; index < count; index++)
                if (predicate((pointer + index).Value))
                    return index;
            return -1;
        }

        /// <summary>
        /// Searches item matching to the given predicate in a range of elements of the unmanaged array, and returns 
        /// the index of its first occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <param name="predicate">The predicate used to check item.</param>
        /// <param name="startIndex">The starting index of the search.</param>
        /// <returns>The index of the matched item; or -1, if value doesn't exist in this array.</returns>
        public long Find(Predicate<T> predicate, long startIndex) => Find(predicate, startIndex, Length);

        /// <summary>
        /// Searches item matching to the given predicate in the unmanaged array, and returns 
        /// the index of its first occurrence.
        /// </summary>
        /// <param name="predicate">The predicate used to check item.</param>
        /// <returns>The index of the matched item; or -1, if value doesn't exist in this array.</returns>
        public long Find(Predicate<T> predicate) => Find(predicate, 0);

        /// <summary>
        /// Searches for the specified object in a range of elements of the unmanaged array, and returns 
        /// the index of its first occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <param name="item">The value to locate in this array.</param>
        /// <param name="startIndex">The starting index of the search.</param>
        /// <param name="count">The number of elements to search.</param>
        /// <param name="comparer">The custom comparer used to compare array element with the given value.</param>
        /// <returns>The index of the first occurrence of value; or -1, if value doesn't exist in this array.</returns>
        public long IndexOf(T item, long startIndex, long count, IEqualityComparer<T> comparer)
        {
            if (count == 0 || IsEmpty)
                return -1;
            else
                count = count.UpperBounded(Length);
            for (var index = startIndex; index < count; index++)
                if (comparer.Equals((pointer + index).Value, item))
                    return index;
            return -1;
        }

        /// <summary>
        /// Searches for the specified object in a range of elements of the unmanaged array, and returns 
        /// the index of its first occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <param name="item">The value to locate in this array.</param>
        /// <param name="startIndex">The starting index of the search.</param>
        /// <param name="comparer">The custom comparer used to compare array element with the given value.</param>
        /// <returns>The index of the first occurrence of value; or -1, if value doesn't exist in this array.</returns>
        public long IndexOf(T item, long startIndex, IEqualityComparer<T> comparer) => IndexOf(item, startIndex, Length, comparer);

        /// <summary>
        /// Searches for the specified object in a range of elements of the unmanaged array, and returns 
        /// the index of its first occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="ValueType{T}.BitwiseEquals(T, T)"/> comparer
        /// to compare elements in this array.
        /// </remarks>
        /// <param name="item">The value to locate in this array.</param>
        /// <param name="startIndex">The starting index of the search.</param>
        /// <returns>The index of the first occurrence of value; or -1, if value doesn't exist in this array.</returns>
        public long IndexOf(T item, long startIndex) => IndexOf(item, startIndex, ValueType<T>.BitwiseComparer.Instance);

        /// <summary>
        /// Searches for the specified object in a range of elements of the unmanaged array, and returns 
        /// the index of its first occurrence. The range extends from a specified 
        /// index for a specified number of elements.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="ValueType{T}.BitwiseEquals(T, T)"/> comparer
        /// to compare elements in this array.
        /// </remarks>
        /// <param name="item">The value to locate in this array.</param>
        /// <returns>The index of the first occurrence of value; or -1, if value doesn't exist in this array.</returns>
        public long IndexOf(T item) => IndexOf(item, 0);

        /// <summary>
        /// Uses a binary search algorithm to locate a specific element in the sorted array.
        /// </summary>
        /// <param name="item">The value to locate.</param>
        /// <param name="startIndex">The starting index of the range to search.</param>
        /// <param name="count">The length of the range to search.</param>
        /// <param name="comparison">The comparison algorithm.</param>
        /// <returns>The index of the item; or -1, if item doesn't exist in the array.</returns>
        public long BinarySearch(T item, long startIndex, long count, Comparison<T> comparison)
        {
            count = count.UpperBounded(Length);
            count -= 1;
            while(startIndex <= count)
            {
                var mid = (startIndex + count) / 2;
                var cmd = comparison((pointer + mid).Value, item);
                if (cmd < 0)
                    startIndex = mid + 1;
                else if (cmd > 0)
                    count = mid - 1;
                else
                    return mid;
            }
            return -1;
        }

        /// <summary>
        /// Uses a binary search algorithm to locate a specific element in the sorted array.
        /// </summary>
        /// <param name="item">The value to locate.</param>
        /// <param name="comparison">The comparison algorithm.</param>
        /// <returns>The index of the item; or -1, if item doesn't exist in the array.</returns>
        public long BinarySearch(T item, Comparison<T> comparison) => BinarySearch(item, 0, Length, comparison);

        private long Partition(long startIndex, long endIndex, Comparison<T> comparison)
        {
            var pivot = (pointer + endIndex).Value;
            var i = startIndex - 1;

            for (var j = startIndex; j < endIndex; j++)
            {
                var jptr = pointer + j;
                if (comparison(jptr.Value, pivot) <= 0)
                {
                    i += 1;
                    (pointer + i).Swap(jptr);
                }
            }

            i += 1;
            (pointer + endIndex).Swap(pointer + i);
            return i;
        }

        private void QuickSort(long startIndex, long endIndex, Comparison<T> comparison)
        {
            if (startIndex < endIndex)
            {
                var partitionIndex = Partition(startIndex, endIndex, comparison);
                QuickSort(startIndex, partitionIndex - 1, comparison);
                QuickSort(partitionIndex + 1, endIndex, comparison);
            }
        }

        /// <summary>
        /// Sorts the range of this array.
        /// </summary>
        /// <param name="startIndex">The starting index of the range to sort.</param>
        /// <param name="count">The length of the range to sort.</param>
        /// <param name="comparison">The comparison algorithm.</param>
        public void Sort(long startIndex, long count, Comparison<T> comparison)
        {
            if (count == 0 || IsEmpty)
                return;
            QuickSort(startIndex, count.UpperBounded(Length) - 1, comparison);
        }

        /// <summary>
        /// Sorts this array.
        /// </summary>
        /// <param name="comparison">The comparison logic.</param>
        public void Sort(Comparison<T> comparison) => Sort(0, Length, comparison);

        /// <summary>
        /// Applies ascending sort of this array.
        /// </summary>
        /// <remarks>
        /// This method uses QuickSort algorithm.
        /// </remarks>
        public void Sort() => Sort(ValueType<T>.BitwiseCompare);

        /// <summary>
        /// Uses a binary search algorithm to locate a specific element in the sorted array.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="ValueType{T}.BitwiseCompare(T, T)"/> method
        /// to compare two values.
        /// </remarks>
        /// <param name="item">The value to locate.</param>
        /// <returns>The index of the item; or -1, if item doesn't exist in the array.</returns>
        public long BinarySearch(T item) => BinarySearch(item, ValueType<T>.BitwiseCompare);

        /// <summary>
        /// Gets pointer to array element.
        /// </summary>
        /// <param name="index">Index of the element.</param>
        /// <returns>Pointer to array element.</returns>
        /// <exception cref="IndexOutOfRangeException">Invalid index.</exception>
        public Pointer<T> ElementAt(long index)
            => index >= 0 && index < Length ?
            pointer + index :
            throw new IndexOutOfRangeException(ExceptionMessages.InvalidIndexValue(Length));
        
		/// <summary>
		/// Gets or sets array element.
		/// </summary>
		/// <param name="index">Element index.</param>
		/// <returns>Array element.</returns>
		/// <exception cref="NullPointerException">This array is not allocated.</exception>
		/// <exception cref="IndexOutOfRangeException">Invalid index.</exception>
		public T this[long index]
		{
            get => ElementAt(index).Value;
            set
            {
                var ptr = ElementAt(index);
                ptr.Value = value;
            }
		}

        T IReadOnlyList<T>.this[int index] => this[index];

        /// <summary>
        /// Obtains typed pointer to the unmanaged memory.
        /// </summary>
        /// <typeparam name="U">The type of the pointer.</typeparam>
        /// <returns>The typed pointer.</returns>
        public Pointer<U> ToPointer<U>() where U : unmanaged => pointer.As<U>();

        /// <summary>
        /// Copies elements from this array into other array. 
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <param name="offset">The position in the destination array from which copying begins.</param>
        /// <param name="count">The number of elements to be copied.</param>
        /// <returns>Actual number of copied elements.</returns>
		public long WriteTo(UnmanagedArray<T> destination, long offset, long count)
		{
			if (pointer.IsNull)
				throw new NullPointerException();
			else if (destination.pointer.IsNull)
				throw new ArgumentNullException(nameof(destination));
			else if (count < 0)
				throw new IndexOutOfRangeException();
			else if (destination.IsEmpty || (count + offset) >= destination.Length)
				return 0;
			pointer.WriteTo(destination.pointer + offset, count);
			return count;
		}

        /// <summary>
        /// Copies elements from this array to the destination array,
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <returns>The actual number of copied elements.</returns>
		public long WriteTo(UnmanagedArray<T> destination) => WriteTo(destination, 0, Length);

        /// <summary>
        /// Copies elements from this array to the managed array. 
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <param name="offset">The position in the destination array from which copying begins.</param>
        /// <param name="count">The number of elements to be copied.</param>
        /// <returns>Actual number of copied elements.</returns>
		public long WriteTo(T[] destination, long offset, long count)
			=> pointer.WriteTo(destination, offset, count.Min(Length));

        /// <summary>
        /// Copies elements from this array to the managed array. 
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <returns>Actual number of copied elements.</returns>
		public long WriteTo(T[] destination) => WriteTo(destination, 0, Length);

        /// <summary>
        /// Copies elements from given managed array to the this array. 
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="offset">The position in the source array from which copying begins.</param>
        /// <param name="count">The number of elements to be copied.</param>
        /// <returns>Actual number of copied elements.</returns>
		public long ReadFrom(T[] source, long offset, long count)
			=> pointer.ReadFrom(source, offset, count.Min(Length));

        /// <summary>
        /// Copies elements from given managed array to the this array. 
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <returns>Actual number of copied elements.</returns>
		public long ReadFrom(T[] source) => ReadFrom(source, 0L, source.LongLength);

        /// <summary>
        /// Copies elements from given unmanaged array to the this array. 
        /// </summary>
        /// <param name="source">The source unmanaged array.</param>
        /// <param name="offset">The position in the source array from which copying begins.</param>
        /// <param name="count">The number of elements to be copied.</param>
        /// <returns>Actual number of copied elements.</returns>
		public long ReadFrom(UnmanagedArray<T> source, long offset, long count)
			=> source.WriteTo(this, offset, count);

        /// <summary>
        /// Copies elements from given unmanaged array to the this array. 
        /// </summary>
        /// <param name="source">The source unmanaged array.</param>
        /// <returns>Actual number of copied elements.</returns>
		public long ReadFrom(UnmanagedArray<T> source) => ReadFrom(source, 0, source.Length);

        /// <summary>
        /// Reinterprets reference to the unmanaged array.
        /// </summary>
        /// <remarks>
        /// Size of <typeparamref name="U"/> must be a multiple of the size <typeparamref name="T"/>.
        /// </remarks>
        /// <typeparam name="U">New element type.</typeparam>
        /// <returns>Reinterpreted unmanaged array which points to the same memory as original array.</returns>
        /// <exception cref="GenericArgumentException{U}">Invalid size of target element type.</exception>
        public UnmanagedArray<U> As<U>()
            where U : unmanaged
            => Pointer<T>.Size % Pointer<U>.Size == 0 ?
                new UnmanagedArray<U>(pointer.As<U>(), Length * (Pointer<T>.Size / Pointer<U>.Size)) :
                throw new GenericArgumentException<U>(ExceptionMessages.TargetSizeMustBeMultipleOf);

		/// <summary>
		/// Converts this unmanaged array into managed array.
		/// </summary>
		/// <returns>Managed copy of unmanaged array.</returns>
		public T[] CopyToManagedHeap()
		{
			if (pointer.IsNull)
				return Array.Empty<T>();
			var result = new T[Length];
			WriteTo(result);
			return result;
		}

		/// <summary>
		/// Creates bitwise copy of unmanaged array.
		/// </summary>
		/// <returns>Bitwise copy of unmanaged array.</returns>
		public UnmanagedArray<T> Copy()
		{
			if (pointer.IsNull)
				return this;
			var result = new UnmanagedArray<T>(Length);
			WriteTo(result);
			return result;
		}

		object ICloneable.Clone() => Copy();

        /// <summary>
        /// Computes bitwise equality between two blocks of memory.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
		public bool BitwiseEquals(Pointer<T> other)
			=> pointer.BitwiseEquals(other, Length);

        /// <summary>
        /// Computes bitwise equality between this array and the specified managed array.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
		public bool BitwiseEquals(T[] other)
		{
			if (other.IsNullOrEmpty())
				return pointer.IsNull;
			else if(Length == other.LongLength)
				fixed (T* ptr = other)
					return BitwiseEquals(ptr);
			else
				return false;
		}

        /// <summary>
        /// Bitwise comparison of the memory blocks.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
		public int BitwiseCompare(Pointer<T> other) => pointer.BitwiseCompare(other, Length);

        /// <summary>
        /// Bitwise comparison of the memory blocks.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
		public int BitwiseCompare(T[] other)
		{
			if (other is null)
				throw new ArgumentNullException(nameof(other));
			else if (other.LongLength == 0L)
				return pointer.IsNull ? 0 : 1;
			else if(Length == other.Length)
				fixed (T* ptr = other)
					return BitwiseCompare(ptr);
			else
				return Length.CompareTo(other.Length);
		}

        /// <summary>
        /// Determines whether this unmanaged array points to the same memory block as other unmanaged array.
        /// </summary>
        /// <typeparam name="U">The type of elements in other unmanaged array.</typeparam>
        /// <param name="other">The array to be compared.</param>
        /// <returns><see langword="true"/>, if this unmanaged array points to the same memory block as other unmanaged array; otherwise, <see langword="false"/>.</returns>
		public bool Equals<U>(UnmanagedArray<U> other) where U: unmanaged => pointer.Equals(other.pointer);

        bool IEquatable<UnmanagedArray<T>>.Equals(UnmanagedArray<T> other) => Equals(other);

        /// <summary>
        /// Determines whether this unmanaged array points to the same memory block as other unmanaged array.
        /// </summary>
        /// <param name="other">The object of type <see cref="UnmanagedArray{T}"/>, <see cref="IntPtr"/> or <see cref="UIntPtr"/> to be compared.</param>
        /// <returns><see langword="true"/>, if this unmanaged array points to the same memory block as other unmanaged array; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
		{
			switch(other)
			{
				case IntPtr pointer:
					return this.pointer.Address == pointer;
				case UIntPtr pointer:
					return new UIntPtr(this.pointer) == pointer;
				case UnmanagedArray<T> array:
					return Equals(array);
				default:
					return false;
			}
		}

        /// <summary>
        /// Determines this array contains the same elements as the given array using
        /// custom equality check function.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <param name="comparer">The object implementing equality check logic.</param>
        /// <returns><see langword="true"/>, if this array contains the same elements as the given array; otherwise, <see langword="false"/>.</returns>
		public bool Equals(T[] other, IEqualityComparer<T> comparer)
		{
			if (other is null)
				return pointer.IsNull;
			else if(Length == other.Length)
			{
				for(int i = 0; i < Length; i++)
					if(!comparer.Equals(this[i], other[i]))
						return false;
				return true;
			}
			else
				return false;
		}

        /// <summary>
        /// Determines this array contains the same elements as the given array using
        /// custom equality check function.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <param name="comparer">The object implementing equality check logic.</param>
        /// <returns><see langword="true"/>, if this array contains the same elements as the given array; otherwise, <see langword="false"/>.</returns>
        public bool Equals(UnmanagedArray<T> other, IEqualityComparer<T> comparer)
		{
			if(Length == other.Length)
			{
				for(int i = 0; i < Length; i++)
					if(!comparer.Equals(this[i], other[i]))
						return false;
				return true;
			}
			else
				return false;
		}

        /// <summary>
        /// Performs comparison between each two elements from this and given array.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <param name="comparer">The custom comparison logic.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
		public int Compare(T[] other, IComparer<T> comparer)
		{
			if (other is null)
				throw new ArgumentNullException(nameof(other));
			else if(Length == other.Length)
			{
				var cmp = 0;
				for(int i = 0; i < Length; i++)
					cmp += comparer.Compare(this[i], other[i]);
				return cmp;
			}
			else
				return Length.CompareTo(other.Length);
		}

        /// <summary>
        /// Performs comparison between each two elements from this and given array.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <param name="comparer">The custom comparison logic.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
		public int Compare(UnmanagedArray<T> other, IComparer<T> comparer)
		{
			if(Length == other.Length)
			{
				var cmp = 0;
				for(int i = 0; i < Length; i++)
					cmp += comparer.Compare(this[i], other[i]);
				return cmp;
			}
			else
				return Length.CompareTo(other.Length);
		}

        /// <summary>
        /// Gets enumerator over elements in this array.
        /// </summary>
        /// <returns>The enumerator over elements in this array.</returns>
        public Pointer<T>.Enumerator GetEnumerator() => pointer.GetEnumerator(Length);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Computes the hash code of the unmanaged array address, not of the array content.
        /// </summary>
        /// <returns>The hash code of the unmanaged array.</returns>
        public override int GetHashCode() => pointer.GetHashCode();

        /// <summary>
        /// Returns hexadecimal representation of the unmanaged array address.
        /// </summary>
        /// <returns>The hexadecimal representation of the unmanaged array address.</returns>
		public override string ToString() => new IntPtr(pointer).ToString("X");

        /// <summary>
        /// Obtains a pointer to the unmanaged array.
        /// </summary>
        /// <param name="array">The array.</param>
		public static implicit operator Pointer<T>(UnmanagedArray<T> array) => array.pointer;

        /// <summary>
        /// Obtains span to the unmanaged array.
        /// </summary>
        /// <param name="array">The unmanaged array.</param>
		public static implicit operator Span<T>(UnmanagedArray<T> array)
        {
            //TODO: should be fixed if Span will support long data type
            //for length parameter
            if (array.pointer.IsNull)
                return default;
            else if (array.Length <= int.MaxValue)
                return new Span<T>(array.pointer, (int)array.Length);
            else
                return new Span<T>(array.pointer, int.MaxValue);
        }

        /// <summary>
        /// Provides untyped access to the unmanaged memory.
        /// </summary>
        /// <param name="array">The unmanaged array.</param>
        public static implicit operator UnmanagedMemory(UnmanagedArray<T> array) => new UnmanagedMemory(array.Address, Pointer<T>.Size);

        /// <summary>
        /// Determines whether two unmanaged arrays point to the same memory block.
        /// </summary>
        /// <param name="first">The first unmanaged array reference to be compared.</param>
        /// <param name="second">The second unmanaged array reference to be compared.</param>
        /// <returns><see langword="true"/>, if both unmanaged arrays point to the same memory block.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(UnmanagedArray<T> first, UnmanagedArray<T> second)
			=> first.pointer == second.pointer;

        /// <summary>
        /// Determines whether two unmanaged arrays point to the different memory blocks.
        /// </summary>
        /// <param name="first">The first unmanaged array reference to be compared.</param>
        /// <param name="second">The second unmanaged array reference to be compared.</param>
        /// <returns><see langword="true"/>, if both unmanaged arrays point to the different memory blocks.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(UnmanagedArray<T> first, UnmanagedArray<T> second)
			=> first.pointer != second.pointer;

        /// <summary>
        /// Determines whether two unmanaged arrays point to the same memory block.
        /// </summary>
        /// <param name="first">The first unmanaged array reference to be compared.</param>
        /// <param name="second">The second unmanaged array reference to be compared.</param>
        /// <returns><see langword="true"/>, if both unmanaged arrays point to the same memory block.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(UnmanagedArray<T> first, Pointer<T> second)
            => first.pointer == second;

        /// <summary>
        /// Determines whether two unmanaged arrays point to the different memory blocks.
        /// </summary>
        /// <param name="first">The first unmanaged array reference to be compared.</param>
        /// <param name="second">The second unmanaged array reference to be compared.</param>
        /// <returns><see langword="true"/>, if both unmanaged arrays point to the different memory blocks.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(UnmanagedArray<T> first, Pointer<T> second)
            => first.pointer != second;

        /// <summary>
        /// Obtains a pointer to the array element.
        /// </summary>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="elementIndex">The element index.</param>
        /// <returns>The pointer to the array element.</returns>
		public static Pointer<T> operator+(UnmanagedArray<T> array, int elementIndex)
			=> array.ElementAt(elementIndex);

		/// <summary>
		/// Releases unmanaged memory associated with the array.
		/// </summary>
		public void Dispose()
		{
			UnmanagedMemory.Release(pointer.Address);
            GC.RemoveMemoryPressure(Size);
			this = default;
		}
	}
}