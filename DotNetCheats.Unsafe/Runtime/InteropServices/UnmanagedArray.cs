using System;
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

		/// <summary>
		/// Size (in bytes) of single element type.
		/// </summary>
		public static readonly int ElementSize = Unsafe.SizeOf<T>();

		private readonly T* pointer;

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
				var size = length * ElementSize;
				pointer = (T*)Marshal.AllocHGlobal(size);
				Unsafe.InitBlock(pointer, 0, (uint)size);
			}
			else
				pointer = (T*)Memory.NullPtr;
		}

		private UnmanagedArray(T* pointer, int length)
		{
			Length = length;
			this.pointer = pointer;
		}

		private UnmanagedArray(IntPtr pointer, int length)
			: this((T*)pointer, length)
		{
		}

		private bool IsInvalid => pointer == Memory.NullPtr;

		/// <summary>
		/// Gets length of this array.
		/// </summary>
		public int Length { get; }

		/// <summary>
		/// Size of allocated memory, in bytes.
		/// </summary>
		public long Size => ElementSize * Length;

		ulong IUnmanagedMemory<T>.Size => (uint)Size;

		T* IUnmanagedMemory<T>.Address => pointer;

		/// <summary>
		/// Zeroes all bits in the allocated memory.
		/// </summary>
		/// <exception cref="NullPointerException">Array is not allocated.</exception>
		public void ZeroMem()
		{
			if (IsInvalid)
				throw new NullPointerException();
			Unsafe.InitBlock(pointer, 0, (uint)Size);
		}

		/// <summary>
		/// Gets or sets array element.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		/// <exception cref="NullPointerException">This array is not allocated.</exception>
		/// <exception cref="IndexOutOfRangeException">Invalid index.</exception>
		[CLSCompliant(false)]
		public T* this[uint index]
		{
			get
			{
				if (IsInvalid)
					throw new NullPointerException();
				else if (index >= 0 && index < Length)
					return pointer + index;
				else
					throw new IndexOutOfRangeException($"Index should be in range [0, {Length})");
			}
		}

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
			get => *this[checked((uint)index)];
			set => *this[checked((uint)index)] = value;
		}
		
		byte* IUnmanagedMemory<T>.this[ulong offset]
		{
			get
			{
				if (IsInvalid)
					throw new NullPointerException();
				else if (offset >= 0 && offset < (ulong)Size)
					return (byte*)pointer + offset;
				else
					throw new IndexOutOfRangeException($"Offset should be in range [0, {Size})");
			}
		}

		public long WriteTo(T[] destination, long offset, long length)
		{
			if (IsInvalid)
				throw new NullPointerException();
			else if (destination is null)
				throw new ArgumentNullException(nameof(destination));
			else if (destination.LongLength == 0L)
				return 0L;
			else if (length < 0 || length > destination.LongLength)
				throw new IndexOutOfRangeException("Destination length is invalid");
			length = Math.Min(length, Length);
			fixed (T* dest = &destination[offset])
				Memory.Copy(pointer, dest, length * ElementSize);
			return length;
		}

		public long WriteTo(T[] destination) => WriteTo(destination, 0, destination.LongLength);

		ulong IUnmanagedMemory<T>.WriteTo(byte[] destination, long offset, long length)
		{
			if (IsInvalid)
				throw new NullPointerException();
			else if (destination is null)
				throw new ArgumentNullException(nameof(destination));
			else if (destination.LongLength == 0L)
				return 0UL;
			else if (length < 0 || length > destination.LongLength)
				throw new IndexOutOfRangeException("Destination length is invalid");
			length = Math.Min(length, Size);
			fixed (byte* dest = &destination[offset])
				Memory.Copy(pointer, dest, length);
			return (ulong)length;
		}

		public void WriteTo(Stream destination)
		{
			if (IsInvalid)
				throw new NullPointerException();
			else if (destination is null)
				throw new ArgumentNullException(nameof(destination));
			else
				Memory.WriteToSteam(pointer, Size, destination);
		}

		public Task WriteToAsync(Stream destination)
		{
			if (IsInvalid)
				throw new NullPointerException();
			else if (destination is null)
				throw new ArgumentNullException(nameof(destination));
			else
				return Memory.WriteToSteamAsync(pointer, Size, destination);
		}

		public long ReadFrom(T[] source, long offset, long length)
		{
			if (IsInvalid)
				throw new NullPointerException();
			else if (source is null)
				throw new ArgumentNullException(nameof(source));
			else if (source.LongLength == 0L)
				return 0L;
			else if (length < 0L || length > source.LongLength)
				throw new IndexOutOfRangeException("Source length is invalid");
			length = Math.Min(length, Length);
			fixed (T* src = &source[offset])
				Memory.Copy(src, pointer, length * ElementSize);
			return length;
		}

		public long ReadFrom(T[] source) => ReadFrom(source, 0L, source.LongLength);

		ulong IUnmanagedMemory<T>.ReadFrom(byte[] source, long offset, long length)
		{
			if (IsInvalid)
				throw new NullPointerException();
			else if (source is null)
				throw new ArgumentNullException(nameof(source));
			else if (source.LongLength == 0L)
				return 0UL;
			else if (length < 0 || length > source.LongLength)
				throw new IndexOutOfRangeException("Source length is invalid");
			length = Math.Min(length, Size);
			fixed (byte* src = &source[offset])
				Memory.Copy(src, pointer, length);
			return (ulong)length;
		}

		public long ReadFrom(Stream source)
		{
			if (IsInvalid)
				throw new NullPointerException();
			else if (source is null)
				throw new ArgumentNullException(nameof(source));
			else
				return Memory.ReadFromStream(source, pointer, Size);
		}

		public Task<long> ReadFromAsync(Stream source)
		{
			if (IsInvalid)
				throw new NullPointerException();
			else if (source is null)
				throw new ArgumentNullException(nameof(source));
			else
				return Memory.ReadFromStreamAsync(source, pointer, Size);
		}

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
		public UnmanagedArray<U> Reinterpret<U>()
			where U : unmanaged
		{
			if (UnmanagedArray<U>.ElementSize > ElementSize)
				throw new GenericArgumentException<U>("Target element type size should be less than or equal to the original element type");
			else if (ElementSize % UnmanagedArray<U>.ElementSize != 0)
				throw new GenericArgumentException<U>("Target element size must be a multiple of the original element size.");
			else
				return new UnmanagedArray<U>((U*)pointer, Length * (ElementSize / UnmanagedArray<U>.ElementSize));
		}

		/// <summary>
		/// Represents unmanaged array as stream.
		/// </summary>
		/// <returns>A stream to unmanaged array.</returns>
		public UnmanagedMemoryStream AsStream()
			=> new UnmanagedMemoryStream((byte*)pointer, Size);

		/// <summary>
		/// Converts this unmanaged array into managed array.
		/// </summary>
		/// <returns>Managed copy of unmanaged array.</returns>
		public T[] CopyToManagedHeap()
		{
			if (IsInvalid)
				return Array.Empty<T>();
			var result = new T[Length];
			fixed (T* destination = result)
				Memory.Copy(pointer, destination, Size);
			return result;
		}

		/// <summary>
		/// Converts unmanaged array into managed array of bytes.
		/// </summary>
		/// <returns>Managed array of bytes as bitwise copy of unmanaged array.</returns>
		public byte[] ToByteArray()
		{
			if (IsInvalid)
				return Array.Empty<byte>();
			var result = new byte[Size];
			fixed (byte* destination = result)
				Memory.Copy(pointer, destination, Size);
			return result;
		}

		/// <summary>
		/// Creates bitwise copy of unmanaged array.
		/// </summary>
		/// <returns>Bitwise copy of unmanaged array.</returns>
		public UnmanagedArray<T> Copy()
		{
			if (IsInvalid)
				return this;
			var result = new UnmanagedArray<T>(Length);
			Memory.Copy(pointer, result.pointer, Size);
			return result;
		}

		object ICloneable.Clone() => Copy();

		ReadOnlySpan<T> IUnmanagedMemory<T>.Span => this;

		[CLSCompliant(false)]
		public bool BitwiseEquals(T* other)
		{
			if (pointer == other)
				return true;
			else if (pointer == Memory.NullPtr || other == Memory.NullPtr)
				return false;
			else
				return Memory.Equals(pointer, other, checked((int)Size));
		}

		public bool BitwiseEquals(in UnmanagedArray<T> other) => BitwiseEquals(other.pointer);

		public bool BitwiseEquals(T[] other)
		{
			if (other.IsNullOrEmpty())
				return IsInvalid;
			else
				fixed (T* ptr = other)
					return BitwiseEquals(ptr);
		}

		public int BitwiseHashCode()
			=> IsInvalid ? 0 : Memory.GetHashCode(pointer, Size);

		[CLSCompliant(false)]
		public int BitwiseCompare(T* other)
		{
			if (IsInvalid)
				throw new NullPointerException();
			else if (other == Memory.NullPtr)
				throw new ArgumentNullException(nameof(other));
			else
				return Memory.Compare(pointer, other, checked((int)Size));
		}

		public int BitwiseCompare(in UnmanagedArray<T> other) => BitwiseCompare(other.pointer);

		public int BitwiseCompare(T[] other)
		{
			if (other is null)
				throw new ArgumentNullException(nameof(other));
			else if (other.LongLength == 0)
				return IsInvalid ? 0 : 1;
			else
				fixed (T* ptr = other)
					return BitwiseCompare(ptr);
		}

		public bool Equals(UnmanagedArray<T> other) => pointer == other.pointer;

		public override bool Equals(object other)
		{
			switch(other)
			{
				case IntPtr pointer:
					return new IntPtr(this.pointer) == pointer;
				case UIntPtr pointer:
					return new UIntPtr(this.pointer) == pointer;
				case UnmanagedArray<T> array:
					return Equals(array);
				default:
					return false;
			}
		}

		public override int GetHashCode() => new IntPtr(pointer).ToInt32();

		public override string ToString() => new IntPtr(pointer).ToString("X");

		[CLSCompliant(false)]
		public static implicit operator T*(in UnmanagedArray<T> array) => array.pointer;

		public static implicit operator ReadOnlySpan<T>(in UnmanagedArray<T> array)
			=> array.pointer == Memory.NullPtr ? new ReadOnlySpan<T>() : new ReadOnlySpan<T>(array.pointer, array.Length);

		public static implicit operator IntPtr(in UnmanagedArray<T> array) => new IntPtr(array.pointer);

		[CLSCompliant(false)]
		public static implicit operator UIntPtr(in UnmanagedArray<T> array) => new UIntPtr(array.pointer);

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
		public void Dispose() => FreeMem(new IntPtr(pointer));
	}
}