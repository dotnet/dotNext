using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;

namespace Cheats.Runtime.InteropServices
{
    using static Threading.Tasks.TaskCheats;

    /// <summary>
    /// Represents unmanaged memory buffer located outside of managed heap.
    /// </summary>
    /// <remarks>
    /// Memory allocated by unmanaged buffer is not controlled by Garbage Collector.
	/// Therefore, it's your responsibility to release unmanaged memory using Dispose call.
    /// </remarks>
    /// <typeparam name="T">Type to be allocated in the unmanaged heap.</typeparam>
    public unsafe readonly struct UnmanagedBuffer<T>: IUnmanagedMemory<T>, IBox<T>, IEquatable<UnmanagedBuffer<T>>
        where T: unmanaged
    {
		/// <summary>
		/// Represents GC-friendly reference to the unmanaged memory.
		/// </summary>
		/// <remarks>
		/// Unmanaged memory allocated using handle can be reclaimed by GC automatically.
		/// </remarks>
		public sealed class Handle : UnmanagedMemoryHandle<T>
		{
			private Handle(UnmanagedBuffer<T> buffer, bool ownsHandle)
				: base(buffer, ownsHandle)
			{
			}

			/// <summary>
			/// Allocates a new unmanaged memory and associate it
			/// with handle.
			/// </summary>
			/// <remarks>
			/// Disposing of the handle created with this constructor
			/// will release unmanaged memory.
			/// </remarks>
			public Handle()
				: this(Alloc(), true)
			{
			}

			/// <summary>
			/// Allocates a new unmanaged memory and associate it
			/// with handle.
			/// </summary>
			/// <remarks>
			/// Disposing of the handle created with this constructor
			/// will release unmanaged memory.
			/// </remarks>
			/// <param name="value">A value to be placed into unmanaged memory.</param>
			public Handle(T value)
				: this(Box(value), true)
			{
			}

			public Handle(UnmanagedBuffer<T> buffer)
				: this(buffer, false)
			{
			}

			public override bool IsInvalid => handle == IntPtr.Zero;

			protected override bool ReleaseHandle() => FreeMem(handle);

			/// <summary>
			/// Converts handle into unmanaged buffer structure.
			/// </summary>
			/// <param name="handle">Handle to convert.</param>
			/// <exception cref="ObjectDisposedException">Handle is closed.</exception>
			public static implicit operator UnmanagedBuffer<T>(Handle handle)
			{
				if (handle is null)
					return default;
				else if (handle.IsClosed)
					throw new ObjectDisposedException(handle.GetType().Name, "Handle is closed");
				else
					return new UnmanagedBuffer<T>(handle.handle);
			}
		}

        /// <summary>
		/// Size (in bytes) of unmanaged memory needed to allocate structure.
		/// </summary>
        public static readonly int Size = Unsafe.SizeOf<T>();

        private readonly T* pointer;

        private UnmanagedBuffer(T* pointer)
            => this.pointer = pointer;
        
        private UnmanagedBuffer(IntPtr pointer)
            : this((T*)pointer)
        {
        }

        private bool IsInvalid => pointer == Memory.NullPtr;

        ulong IUnmanagedMemory<T>.Size => (ulong)Size;

        T* IUnmanagedMemory<T>.Address => pointer;

        ReadOnlySpan<T> IUnmanagedMemory<T>.Span => this;

        private static UnmanagedBuffer<T> AllocUnitialized() => new UnmanagedBuffer<T>(Marshal.AllocHGlobal(Size));

        /// <summary>
        /// Boxes unmanaged type into unmanaged heap.
        /// </summary>
        /// <param name="value">A value to be placed into unmanaged memory.</param>
        /// <returns>Embedded reference to the allocated unmanaged memory.</returns>
        public unsafe static UnmanagedBuffer<T> Box(T value)
        {
            //allocate unmanaged memory
            var result = AllocUnitialized();
            Unsafe.Copy(result.pointer, ref value);
            return result;
        }

        /// <summary>
        /// Allocates unmanaged type in the unmanaged heap.
        /// </summary>
        /// <returns>Embedded reference to the allocated unmanaged memory.</returns>
        public static UnmanagedBuffer<T> Alloc()
        {
            var result = AllocUnitialized();
            result.InitMem(0);
            return result;
        }

        private void InitMem(byte value)
            => Unsafe.InitBlock(pointer, 0, (uint)Size);

		/// <summary>
		/// Sets all bits of allocated memory to zero.
		/// </summary>
		/// <exception cref="NullPointerException">This buffer is not allocated.</exception>
		public void ZeroMem()
        {
            if(IsInvalid)
                throw new NullPointerException();
            InitMem(0);
        }

        [CLSCompliant(false)]
        public void ReadFrom<U>(U* source)
            where U: unmanaged
        {
            var buffer = new UnmanagedBuffer<U>(source);
            buffer.WriteTo(pointer);
        }

        public void ReadFrom(ref T source)
        {
            if(IsInvalid)
                throw new NullPointerException();
            else
                Unsafe.Copy(pointer, ref source);
        }

		public long ReadFrom(byte[] source, long offset, long length)
		{
			if (IsInvalid)
				throw new NullPointerException();
			else if (source is null)
				throw new ArgumentNullException(nameof(source));
			else if (source.LongLength == 0L)
				return 0L;
			else if (length < 0L || length > source.LongLength)
				throw new IndexOutOfRangeException("Source length is invalid");
			length = Math.Min(Size, length);
			fixed (byte* src = &source[offset])
				Memory.Copy(src, pointer, length);
			return length;
		}

		public long ReadFrom(byte[] source) => ReadFrom(source, 0L, source.LongLength);

		ulong IUnmanagedMemory<T>.ReadFrom(byte[] source, long offset, long length) => (ulong)ReadFrom(source, offset, length);

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

		[CLSCompliant(false)]
        public void WriteTo<U>(U* destination)
            where U: unmanaged
        {
            if(IsInvalid)
                throw new NullPointerException();
            else if(destination == Memory.NullPtr)
                throw new ArgumentNullException(nameof(destination));
            else
                Memory.Copy(pointer, destination, Math.Min(Size, UnmanagedBuffer<U>.Size));
        }

        public void WriteTo(ref T destination)
        {
            if(IsInvalid)
                throw new NullPointerException();
            else
                Unsafe.Copy(ref destination, pointer);
        }

        public void WriteTo<U>(UnmanagedBuffer<U> destination)
            where U: unmanaged
            => WriteTo(destination.pointer);

		public long WriteTo(byte[] destination, long offset, long length)
		{
			if (IsInvalid)
				throw new NullPointerException();
			else if (destination is null)
				throw new ArgumentNullException(nameof(destination));
			else if (destination.LongLength == 0L)
				return 0L;
			else if (length < 0 || length > destination.LongLength)
				throw new IndexOutOfRangeException("Destination length is invalid");
			length = Math.Min(Size, length);
			fixed (byte* dest = &destination[offset])
				Memory.Copy(pointer, dest, length);
			return length;
		}

		public long WriteTo(byte[] destination) => WriteTo(destination, 0L, destination.LongLength);


		ulong IUnmanagedMemory<T>.WriteTo(byte[] destination, long offset, long length) => (ulong)WriteTo(destination, offset, length);

        public void WriteTo(Stream destination)
        {
            if(IsInvalid)
                throw new NullPointerException();
            else if(destination is null)
                throw new ArgumentNullException(nameof(destination));
            else
                Memory.WriteToSteam(pointer, Size, destination);
        }

        public Task WriteToAsync(Stream destination)
        {
            if(IsInvalid)
                throw new NullPointerException();
            else if(destination is null)
                throw new ArgumentNullException(nameof(destination));
            else
                return Memory.WriteToSteamAsync(pointer, Size, destination);
        }

        /// <summary>
        /// Unboxes structure from unmanaged heap.
        /// </summary>
        /// <returns>Unboxed type.</returns>
        /// <exception cref="NullReferenceException">Attempt to dereference null pointer.</exception>
        public T Unbox() => pointer == Memory.NullPtr ? throw new NullPointerException() : *pointer;

        /// <summary>
        /// Creates a copy of value in the managed heap.
        /// </summary>
        /// <returns>A boxed copy in the managed heap.</returns>
        public Box<T> CopyToManagedHeap() => new Box<T>(Unbox());

		/// <summary>
		/// Creates bitwise copy of unmanaged buffer.
		/// </summary>
		/// <returns>Bitwise copy of unmanaged buffer.</returns>
        public UnmanagedBuffer<T> Copy()
        {
            if(IsInvalid)
                return this;
            var result = AllocUnitialized();
            Memory.Copy(pointer, result.pointer, Size);
            return result;
        }

        object ICloneable.Clone() => Copy();

		/// <summary>
		/// Reinterprets reference to the unmanaged buffer.
		/// </summary>
		/// <remarks>
		/// Type <typeparamref name="U"/> should be of the same size or less than type <typeparamref name="U"/>.
		/// </remarks>
		/// <typeparam name="U">New buffer type.</typeparam>
		/// <returns>Reinterpreted reference pointing to the same memory as original buffer.</returns>
		/// <exception cref="GenericArgumentException{U}">Target type should be of the same size or less than original type.</exception>
		public UnmanagedBuffer<U> ReinterpretCast<U>() 
            where U: unmanaged
        {
            if(IsInvalid)
                throw new NullPointerException();
            else if(Size > UnmanagedBuffer<U>.Size)
                throw new GenericArgumentException<U>("Target type should be the same size or less than original type");
            else
                return new UnmanagedBuffer<U>(this);
        }

		/// <summary>
		/// Converts unmanaged buffer into managed array.
		/// </summary>
		/// <returns>Copy of unmanaged buffer in the form of managed byte array.</returns>
        public byte[] ToByteArray()
        {
			if (IsInvalid)
				return Array.Empty<byte>();
            var result = new byte[Size];
            fixed(byte* destination = result)
                Memory.Copy(pointer, destination, Size);
            return result;
        }

		/// <summary>
		/// Gets memory byte at the specified offset.
		/// </summary>
		/// <param name="offset">Zero-based byte offset.</param>
		/// <returns>Memory byte.</returns>
		public byte GetByte(int offset) => *this[checked((ulong)offset)];

		/// <summary>
		/// Sets memory byte at the specified offset.
		/// </summary>
		/// <param name="offset">Zero-based byte offset.</param>
		/// <param name="value">Byte value to be written into memory.</param>
		public void SetByte(int offset, byte value) => *this[checked((ulong)offset)] = value;

		/// <summary>
		/// Gets pointer to the memory block.
		/// </summary>
		/// <param name="offset">Zero-based byte offset.</param>
		/// <returns>Byte located at the specified offset in the memory.</returns>
		/// <exception cref="NullPointerException">This buffer is not allocated.</exception>
		/// <exception cref="IndexOutOfRangeException">Invalid offset.</exception>
		[CLSCompliant(false)]
		public byte* this[ulong offset]
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

        public static implicit operator IntPtr(UnmanagedBuffer<T> buffer) => new IntPtr(buffer.pointer);

        [CLSCompliant(false)]
        public static implicit operator UIntPtr(UnmanagedBuffer<T> buffer) => new UIntPtr(buffer.pointer);

        [CLSCompliant(false)]
        public static implicit operator T*(UnmanagedBuffer<T> buffer)
            => buffer.IsInvalid ? throw new NullPointerException() : buffer.pointer;

        public static implicit operator ReadOnlySpan<T>(UnmanagedBuffer<T> buffer)
            => buffer.IsInvalid? throw new NullPointerException() : new ReadOnlySpan<T>(buffer.pointer, 1);

        public static implicit operator T(UnmanagedBuffer<T> heap) => heap.Unbox();

        /// <summary>
        /// Gets unmanaged memory buffer as stream.
        /// </summary>
        /// <returns>Stream to unmanaged memory buffer.</returns>
        public UnmanagedMemoryStream AsStream() => new UnmanagedMemoryStream((byte*)pointer, Size);

        private static bool FreeMem(IntPtr memory)
        {
            if(memory == IntPtr.Zero)
                return false;
            Marshal.FreeHGlobal(memory);
            return true;
        }

        /// <summary>
        /// Releases unmanaged memory associated with the boxed type.
        /// </summary>
        public void Dispose() => FreeMem(this);

        public bool Equals(UnmanagedBuffer<T> other) => pointer == other.pointer;

        public override int GetHashCode() => new IntPtr(pointer).ToInt32();

        public int BitwiseHashCode() => IsInvalid ? 0 : Memory.GetHashCode(pointer, Size);

		public override bool Equals(object other)
        {
            switch(other)
            {
                case IntPtr pointer:
                    return new IntPtr(this.pointer) == pointer;
                case UIntPtr pointer:
                    return new UIntPtr(this.pointer) == pointer;
                case UnmanagedBuffer<T> box:
                    return Equals(box);
                default:
                    return false;
            }
        }

		public override string ToString() => new IntPtr(pointer).ToString("X");

		[CLSCompliant(false)]
        public bool BitwiseEquals(T* other)
        {
            if(pointer == other)
                return true;
            else if(pointer == Memory.NullPtr || other == Memory.NullPtr)
                return false;
            else
                return Memory.Equals(pointer, other, Size);
        }

        public bool BitwiseEquals(UnmanagedBuffer<T> other)
            => BitwiseEquals(other.pointer);

        [CLSCompliant(false)]
        public int BitwiseCompare(T* other)
        {
            if(IsInvalid)
                throw new NullPointerException();
            else if(other == Memory.NullPtr)
                throw new ArgumentNullException(nameof(other));
            else
                return Memory.Compare(pointer, other, Size);
        }

        public int BitwiseCompare(UnmanagedBuffer<T> other)
            => BitwiseCompare(other.pointer);

        public bool Equals(T other, IEqualityComparer<T> comparer)
            => !IsInvalid && comparer.Equals(*pointer, other);

        public int GetHashCode(IEqualityComparer<T> comparer)
            => IsInvalid ? 0 : comparer.GetHashCode(*pointer); 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(UnmanagedBuffer<T> first, UnmanagedBuffer<T> second) => first.pointer == second.pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static bool operator !=(UnmanagedBuffer<T> first, UnmanagedBuffer<T> second) => first.pointer != second.pointer;

        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static bool operator ==(UnmanagedBuffer<T> first, void* second) => first.pointer == second;

        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(UnmanagedBuffer<T> first, void* second) => first.pointer != second;
    }
}