using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices
{
	using Threading.Tasks;

	/// <summary>
	/// Represents typed array allocated in the unmanaged heap.
	/// </summary>
	/// <remarks>
    /// Allocated memory is not controlled by Garbage Collector.
	/// Therefore, it's developer responsibility to release unmanaged memory using <see cref="IDisposable.Dispose"/> call.
    /// </remarks>
	/// <typeparam name="T">Array element type.</typeparam>
	public unsafe struct UnmanagedArray<T> : IUnmanagedMemory<T>, IEquatable<UnmanagedArray<T>>, IEnumerable<T>
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
			private readonly int length;

			private Handle(UnmanagedArray<T> array, bool ownsHandle)
				: base(array, ownsHandle)
			{
				length = array.Length;
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
			public Handle(int length)
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

            /// <summary>
            /// Gets a value indicating whether the unmanaged memory is released.
            /// </summary>
			public override bool IsInvalid => handle == IntPtr.Zero;

            /// <summary>
            /// Releases referenced unmanaged memory.
            /// </summary>
            /// <returns><see langword="true"/>, if this handle is valid; otherwise, <see langword="false"/>.</returns>
			protected override bool ReleaseHandle() => FreeMem(handle);

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
					throw new ObjectDisposedException(handle.GetType().Name, ExceptionMessages.HandleClosed);
				else
					return new UnmanagedArray<T>(handle.handle, handle.length);
			}
		}

		private readonly Pointer<T> pointer;

		/// <summary>
		/// Allocates a new array in the unmanaged memory of the specified length.
		/// </summary>
		/// <param name="length">Array length. Cannot be less or equal than zero.</param>
		/// <exception cref="ArgumentOutOfRangeException">Invalid length.</exception>
		public UnmanagedArray(int length)
		{
			if (length < 0)
				throw new ArgumentOutOfRangeException(ExceptionMessages.ArrayNegativeLength);
			else if ((Length = length) > 0L)
			{
				var size = length * Pointer<T>.Size;
				pointer = new Pointer<T>(Marshal.AllocHGlobal(size));
				pointer.Clear(length);
			}
			else
				pointer = Pointer<T>.Null;
		}

		private UnmanagedArray(Pointer<T> pointer, int length)
		{
			Length = length;
			this.pointer = pointer;
		}

		private UnmanagedArray(IntPtr pointer, int length)
			: this((T*)pointer, length)
		{
		}

		/// <summary>
		/// Gets length of this array.
		/// </summary>
		public int Length { get; }

		/// <summary>
		/// Size of allocated memory, in bytes.
		/// </summary>
		public long Size => Pointer<T>.Size * Length;

        Pointer<T> IUnmanagedMemory<T>.Pointer => pointer;

        IntPtr IUnmanagedMemory.Address => pointer.Address;

        Span<T> IUnmanagedMemory<T>.Span => this;

        /// <summary>
        /// Gets pointer to array element.
        /// </summary>
        /// <param name="index">Index of the element.</param>
        /// <returns>Pointer to array element.</returns>
        public Pointer<T> ElementAt(int index)
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
		public T this[int index]
		{
            get => ElementAt(index).Read(MemoryAccess.Aligned);
            set => ElementAt(index).Write(MemoryAccess.Aligned, value);
		}

        /// <summary>
        /// Obtains typed pointer to the unmanaged memory.
        /// </summary>
        /// <typeparam name="U">The type of the pointer.</typeparam>
        /// <returns>The typed pointer.</returns>
        public Pointer<U> ToPointer<U>() where U : unmanaged => pointer.As<U>();

        /// <summary>
		/// Gets pointer to the memory block.
		/// </summary>
		/// <param name="offset">Zero-based byte offset.</param>
		/// <returns>Byte located at the specified offset in the memory.</returns>
		/// <exception cref="NullPointerException">This buffer is not allocated.</exception>
		/// <exception cref="IndexOutOfRangeException">Invalid offset.</exception>    
        public Pointer<byte> ToPointer(long offset) => offset >= 0 && offset < Pointer<T>.Size ?
                pointer.As<byte>() + offset :
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidOffsetValue(Pointer<T>.Size));

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
			else if (destination.Length == 0 || (count + offset) >= destination.Length)
				return 0;
			pointer.WriteTo(destination.pointer + offset, count);
			return count;
		}

        /// <summary>
        /// Copies elements from this array to the destination array,
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <returns>The actual number of copied elements.</returns>
		public long WriteTo(UnmanagedArray<T> destination) => WriteTo(destination, 0, destination.Length);

        /// <summary>
        /// Copies elements from this array to the managed array. 
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <param name="offset">The position in the destination array from which copying begins.</param>
        /// <param name="count">The number of elements to be copied.</param>
        /// <returns>Actual number of copied elements.</returns>
		public long WriteTo(T[] destination, long offset, long count)
			=> pointer.WriteTo(destination, offset, Math.Min(count, Length));

        /// <summary>
        /// Copies elements from this array to the managed array. 
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <returns>Actual number of copied elements.</returns>
		public long WriteTo(T[] destination) => WriteTo(destination, 0, destination.LongLength);

        /// <summary>
        /// Copies elements from given managed array to the this array. 
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="offset">The position in the source array from which copying begins.</param>
        /// <param name="count">The number of elements to be copied.</param>
        /// <returns>Actual number of copied elements.</returns>
		public long ReadFrom(T[] source, long offset, long count)
			=> pointer.ReadFrom(source, offset, Math.Min(count, Length));

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
			=> array.pointer == Memory.NullPtr ? default : new Span<T>(array.pointer, array.Length);

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

		private static bool FreeMem(IntPtr memory)
		{
			if (memory == IntPtr.Zero)
				return false;
			Marshal.FreeHGlobal(memory);
			return true;
		}

		/// <summary>
		/// Releases unmanaged memory associated with the array.
		/// </summary>
		public void Dispose()
		{
			FreeMem(pointer.Address);
			this = default;
		}
	}
}