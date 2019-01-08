using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;

namespace Cheats.Runtime.InteropServices
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
	public unsafe struct UnmanagedArray<T> : IUnmanagedMemory<T>, IEquatable<UnmanagedArray<T>>
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
			/// Initializes a new unmanaged array and associate
			/// it with the handle.
			/// </summary>
			/// <param name="length">Array length.</param>
			public Handle(int length)
				: this(new UnmanagedArray<T>(length), true)
			{

			}

			public Handle(UnmanagedArray<T> array)
				: this(array, false)
			{
			}

			public override bool IsInvalid => handle == IntPtr.Zero;

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
					throw new ObjectDisposedException(handle.GetType().Name, "Handle is closed");
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
				throw new ArgumentOutOfRangeException("Length of the array should not be less than zero");
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

		ulong IUnmanagedMemory<T>.Size => (ulong)Size;

		T* IUnmanagedMemory<T>.Address => pointer;

		/// <summary>
		/// Zeroes all bits in the allocated memory.
		/// </summary>
		/// <exception cref="NullPointerException">Array is not allocated.</exception>
		public void Clear() => pointer.Clear(Length);

		/// <summary>
		/// Gets or sets array element.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		/// <exception cref="NullPointerException">This array is not allocated.</exception>
		/// <exception cref="IndexOutOfRangeException">Invalid index.</exception>
		[CLSCompliant(false)]
		public Pointer<T> this[uint index] => index >= 0 && index < Length ?
			pointer + index :
			throw new IndexOutOfRangeException($"Index should be in range [0, {Length})");

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
			get => this[checked((uint)index)].ReadUnaligned();
			set => this[checked((uint)index)].WriteUnaligned(value);
		}
		
		byte* IUnmanagedMemory<T>.this[ulong offset] => offset >= 0 && offset < (ulong)Pointer<T>.Size ? 
                pointer.As<byte>() + offset : 
                throw new IndexOutOfRangeException($"Offset should be in range [0, {Pointer<T>.Size})");

		public int WriteTo(in UnmanagedArray<T> destination, int offset, int length)
		{
			if (pointer.IsNull)
				throw new NullPointerException();
			else if (destination.pointer.IsNull)
				throw new ArgumentNullException(nameof(destination));
			else if (length < 0)
				throw new IndexOutOfRangeException("Destination length is invalid");
			else if (destination.Length == 0 || (length + offset) >= destination.Length)
				return 0;
			pointer.WriteTo(destination.pointer + offset, length);
			return length;
		}

		public int WriteTo(in UnmanagedArray<T> destination) => WriteTo(destination, 0, destination.Length);

		public long WriteTo(T[] destination, long offset, long length)
			=> pointer.WriteTo(destination, offset, Math.Min(length, Length));

		public long WriteTo(T[] destination) => WriteTo(destination, 0, destination.LongLength);

		ulong IUnmanagedMemory<T>.WriteTo(byte[] destination, long offset, long length)
			=> (ulong)pointer.As<byte>().WriteTo(destination, offset, Math.Min(Size, length));

		public void WriteTo(Stream destination)
			=> pointer.WriteTo(destination, Length);

		public Task WriteToAsync(Stream destination)
			=> pointer.WriteToAsync(destination, Length);

		public long ReadFrom(T[] source, long offset, long length)
			=> pointer.ReadFrom(source, offset, Math.Min(length, Length));

		public long ReadFrom(T[] source) => ReadFrom(source, 0L, source.LongLength);

		public int ReadFrom(in UnmanagedArray<T> source, int offset, int length)
			=> source.WriteTo(this, offset, length);

		public int ReadFrom(in UnmanagedArray<T> source) => ReadFrom(source, 0, source.Length);

		ulong IUnmanagedMemory<T>.ReadFrom(byte[] source, long offset, long length)
			=> (ulong)pointer.As<byte>().ReadFrom(source, offset, Math.Min(Size, length));

		public long ReadFrom(Stream source)
			=> pointer.ReadFrom(source, Length);

		public Task<long> ReadFromAsync(Stream source)
			=> pointer.ReadFromAsync(source, Length);

		ulong IUnmanagedMemory<T>.ReadFrom(Stream source) => (ulong)ReadFrom(source);

		Task<ulong> IUnmanagedMemory<T>.ReadFromAsync(Stream source) => ReadFromAsync(source).Map(Convert.ToUInt64);

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
				new UnmanagedArray<U>(pointer.As<U>(), Length * (Pointer<T>.Size / Pointer<U>.Size)):
				throw new GenericArgumentException<U>("Target element size must be a multiple of the original element size.");

		/// <summary>
		/// Represents unmanaged array as stream.
		/// </summary>
		/// <returns>A stream to unmanaged array.</returns>
		public UnmanagedMemoryStream AsStream()
			=> pointer.AsStream(Length);

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
		/// Converts unmanaged array into managed array of bytes.
		/// </summary>
		/// <returns>Managed array of bytes as bitwise copy of unmanaged array.</returns>
		public byte[] ToByteArray()
			=> pointer.ToByteArray(Length);

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

		ReadOnlySpan<T> IUnmanagedMemory<T>.Span => this;

		public bool BitwiseEquals(Pointer<T> other)
			=> pointer.BitwiseEquals(other, Length);

		public bool BitwiseEquals(in UnmanagedArray<T> other) => BitwiseEquals(other.pointer);

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

		public int BitwiseHashCode(bool salted)
			=> pointer.BitwiseHashCode(Length, salted);

		public int BitwiseCompare(Pointer<T> other) => pointer.BitwiseCompare(other, Length);

		public int BitwiseCompare(in UnmanagedArray<T> other) => BitwiseCompare(other.pointer);

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

		public override int GetHashCode() => new IntPtr(pointer).ToInt32();


		public override string ToString() => new IntPtr(pointer).ToString("X");

		public static implicit operator Pointer<T>(in UnmanagedArray<T> array) => array.pointer;

		public static implicit operator ReadOnlySpan<T>(in UnmanagedArray<T> array)
			=> array.pointer == Memory.NullPtr ? new ReadOnlySpan<T>() : new ReadOnlySpan<T>(array.pointer, array.Length);

		public static bool operator ==(in UnmanagedArray<T> first, in UnmanagedArray<T> second)
			=> first.pointer == second.pointer;

		public static bool operator !=(in UnmanagedArray<T> first, in UnmanagedArray<T> second)
			=> first.pointer != second.pointer;

		[CLSCompliant(false)]
		public static bool operator ==(in UnmanagedArray<T> first, T* second)
			=> first.pointer == second;

		[CLSCompliant(false)]
		public static bool operator !=(in UnmanagedArray<T> first, T* second)
			=> first.pointer != second;

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