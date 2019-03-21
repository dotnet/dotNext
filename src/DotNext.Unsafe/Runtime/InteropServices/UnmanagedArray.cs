using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;

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

        ReadOnlySpan<T> IUnmanagedMemory<T>.Span => this;

        /// <summary>
        /// Gets pointer to array element.
        /// </summary>
        /// <param name="index">Index of the element.</param>
        /// <returns>Pointer to array element.</returns>
        [CLSCompliant(false)]
        public Pointer<T> ElementAt(uint index)
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
			get => this[(long)index];
			set => this[(long)index] = value;
		}

		/// <summary>
		/// Gets or sets array element.
		/// </summary>
		/// <param name="index">Element index.</param>
		/// <returns>Array element.</returns>
		/// <exception cref="NullPointerException">This array is not allocated.</exception>
		/// <exception cref="IndexOutOfRangeException">Invalid index.</exception>
		public T this[long index]
		{
			get => ElementAt(checked((uint)index)).Read(MemoryAccess.Aligned);
			set => ElementAt(checked((uint)index)).Write(MemoryAccess.Aligned, value);
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

		public int WriteTo(in UnmanagedArray<T> destination, int offset, int length)
		{
			if (pointer.IsNull)
				throw new NullPointerException();
			else if (destination.pointer.IsNull)
				throw new ArgumentNullException(nameof(destination));
			else if (length < 0)
				throw new IndexOutOfRangeException();
			else if (destination.Length == 0 || (length + offset) >= destination.Length)
				return 0;
			pointer.WriteTo(destination.pointer + offset, length);
			return length;
		}

		public int WriteTo(UnmanagedArray<T> destination) => WriteTo(destination, 0, destination.Length);

		public long WriteTo(T[] destination, long offset, long length)
			=> pointer.WriteTo(destination, offset, Math.Min(length, Length));

		public long WriteTo(T[] destination) => WriteTo(destination, 0, destination.LongLength);

		public long ReadFrom(T[] source, long offset, long length)
			=> pointer.ReadFrom(source, offset, Math.Min(length, Length));

		public long ReadFrom(T[] source) => ReadFrom(source, 0L, source.LongLength);

		public int ReadFrom(UnmanagedArray<T> source, int offset, int length)
			=> source.WriteTo(this, offset, length);

		public int ReadFrom(UnmanagedArray<T> source) => ReadFrom(source, 0, source.Length);

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

		public bool BitwiseEquals(Pointer<T> other)
			=> pointer.BitwiseEquals(other, Length);

		public bool BitwiseEquals(T[] other)
		{
			if (other.IsNullOrEmpty())
				return pointer.IsNull;
			else if(Length == other.Length)
				fixed (T* ptr = other)
					return BitwiseEquals(ptr);
			else
				return false;
		}

		public int BitwiseCompare(Pointer<T> other) => pointer.BitwiseCompare(other, Length);

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

		public bool Equals<U>(UnmanagedArray<U> other) 
			where U: unmanaged
			=> pointer.Equals(other.pointer);

        bool IEquatable<UnmanagedArray<T>>.Equals(UnmanagedArray<T> other) => Equals(other);

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

		public bool Equals(in UnmanagedArray<T> other, IEqualityComparer<T> comparer)
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

		public int Compare(in UnmanagedArray<T> other, IComparer<T> comparer)
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

        public Pointer<T>.Enumerator GetEnumerator() => pointer.GetEnumerator(Length);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override int GetHashCode() => pointer.GetHashCode();

		public override string ToString() => new IntPtr(pointer).ToString("X");

		public static implicit operator Pointer<T>(in UnmanagedArray<T> array) => array.pointer;

		public static implicit operator ReadOnlySpan<T>(in UnmanagedArray<T> array)
			=> array.pointer == Memory.NullPtr ? new ReadOnlySpan<T>() : new ReadOnlySpan<T>(array.pointer, array.Length);

		public static bool operator ==(in UnmanagedArray<T> first, in UnmanagedArray<T> second)
			=> first.pointer == second.pointer;

		public static bool operator !=(in UnmanagedArray<T> first, in UnmanagedArray<T> second)
			=> first.pointer != second.pointer;

		public static Pointer<T> operator+(in UnmanagedArray<T> array, int elementIndex)
			=> array.ElementAt(checked((uint)elementIndex));

		public static Pointer<T> operator+(in UnmanagedArray<T> array, long elementIndex)
			=> array.ElementAt(checked((uint)elementIndex));

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
		public void Dispose() => FreeMem(pointer.Address);
	}
}