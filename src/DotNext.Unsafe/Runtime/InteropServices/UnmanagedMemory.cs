using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents unmanaged structured memory located outside of managed heap.
    /// </summary>
    /// <remarks>
    /// Allocated memory is not controlled by Garbage Collector.
	/// Therefore, it's developer responsibility to release unmanaged memory using <see cref="IDisposable.Dispose"/> call.
    /// </remarks>
    /// <typeparam name="T">Type to be allocated in the unmanaged heap.</typeparam>
    public unsafe struct UnmanagedMemory<T>: IUnmanagedMemory<T>, IStrongBox, IEquatable<UnmanagedMemory<T>>
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
			private Handle(UnmanagedMemory<T> buffer, bool ownsHandle)
				: base(buffer, ownsHandle)
			{
			}

            /// <summary>
            /// Allocates a new unmanaged memory and associate it
            /// with handle.
            /// </summary>
            /// <remarks>
            /// The handle instantiated with this constructor has ownership over unmanaged memory.
            /// Unmanaged memory will be released when Garbage Collector reclaims instance of this handle
            /// or <see cref="Dispose()"/> will be called directly.
            /// </remarks>
            public Handle()
				: this(Alloc(), true)
			{
			}

            /// <summary>
            /// Allocates a new unmanaged memory and associate it with handle.
            /// </summary>
            /// <remarks>
            /// The handle instantiated with this constructor has ownership over unmanaged memory.
            /// Unmanaged memory will be released when Garbage Collector reclaims instance of this handle
            /// or <see cref="Dispose()"/> will be called directly.
            /// </remarks>
            /// <param name="value">A value to be placed into unmanaged memory.</param>
            public Handle(T value)
				: this(Box(value), true)
			{
			}

            /// <summary>
            /// Initializes a new handle for the given unmanaged memory.
            /// </summary>
            /// <remarks>
            /// The handle instantiated with this constructor doesn't have ownership over unmanaged memory.
            /// </remarks>
            /// <param name="buffer"></param>
			public Handle(UnmanagedMemory<T> buffer)
				: this(buffer, false)
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
			/// Converts handle into unmanaged buffer structure.
			/// </summary>
			/// <param name="handle">Handle to convert.</param>
			/// <exception cref="ObjectDisposedException">Handle is closed.</exception>
			public static implicit operator UnmanagedMemory<T>(Handle handle)
			{
				if (handle is null)
					return default;
				else if (handle.IsClosed)
					throw new ObjectDisposedException(handle.GetType().Name, ExceptionMessages.HandleClosed);
				else
					return new UnmanagedMemory<T>(handle.handle);
			}
		}

        private Pointer<T> pointer;

        private UnmanagedMemory(Pointer<T> pointer)
            => this.pointer = pointer;
        
        private UnmanagedMemory(IntPtr pointer)
            : this(new Pointer<T>(pointer))
        {
        }

        /// <summary>
        /// Obtains typed pointer to the unmanaged memory.
        /// </summary>
        /// <typeparam name="U">The type of the pointer.</typeparam>
        /// <returns>The typed pointer.</returns>
        public Pointer<U> ToPointer<U>() where U : unmanaged => pointer.As<U>();

        Pointer<T> IUnmanagedMemory<T>.Pointer => pointer;

        long IUnmanagedMemory.Size => Pointer<T>.Size;

        /// <summary>
        /// Gets address of the unmanaged memory.
        /// </summary>
        public IntPtr Address => pointer.Address;

        Span<T> IUnmanagedMemory<T>.Span => this;

        /// <summary>
        /// Gets or sets value stored in unmanaged memory.
        /// </summary>
        public T Value
		{
            get => pointer.Value;
            set => pointer.Value = value;
		}

		object IStrongBox.Value
		{
			get => pointer.IsNull ? null : (object)Value;
			set
			{
				if (value is T typedVal)
					Value = typedVal;
				else
					throw new ArgumentException(ExceptionMessages.ExpectedType(typeof(T)), nameof(value));
			}
		}

        private static UnmanagedMemory<T> AllocUnitialized() => new UnmanagedMemory<T>(Marshal.AllocHGlobal(Pointer<T>.Size));

        /// <summary>
        /// Boxes unmanaged type into unmanaged heap.
        /// </summary>
        /// <param name="value">A value to be placed into unmanaged memory.</param>
        /// <returns>Embedded reference to the allocated unmanaged memory.</returns>
        public unsafe static UnmanagedMemory<T> Box(T value)
        {
            //allocate unmanaged memory
            var result = AllocUnitialized();
            result.Value = value;
            return result;
        }

        /// <summary>
        /// Allocates unmanaged type in the unmanaged heap.
        /// </summary>
        /// <returns>Embedded reference to the allocated unmanaged memory.</returns>
        public static UnmanagedMemory<T> Alloc()
        {
            var result = AllocUnitialized();
            result.Clear();
            return result;
        }

        /// <summary>
        /// Copies the value located at the memory block identified by the given pointer
        /// into the memory identified by this instance.
        /// </summary>
        /// <remarks>
        /// If size of type <typeparamref name="U"/> is greater than <typeparamref name="T"/>
        /// then not all bits will be copied. In this case, the copied bits depend on underlying
        /// hardware architecture and endianess of bytes in memory.
        /// </remarks>
        /// <typeparam name="U">The type of the value located at source memory block.</typeparam>
        /// <param name="source">The source memory block.</param>
        public void ReadFrom<U>(Pointer<U> source)
            where U: unmanaged
            => new UnmanagedMemory<U>(source).WriteTo(pointer);

        /// <summary>
        /// Copies the value located at the memory block identified by this instance to
        /// another location in the memory.
        /// </summary>
        /// <remarks>
        /// If size of type <typeparamref name="T"/> is greater than <typeparamref name="U"/>
        /// then not all bits will be copied. In this case, the copied bits depend on underlying
        /// hardware architecture and endianess of bytes in memory.
        /// </remarks>
        /// <typeparam name="U">The type indicating size of the destination memory block.</typeparam>
        /// <param name="destination">The destination memory block.</param>
        public void WriteTo<U>(Pointer<U> destination)
            where U: unmanaged
            => pointer.As<byte>().WriteTo(destination.As<byte>(), Math.Min(Pointer<T>.Size, Pointer<U>.Size));

        /// <summary>
        /// Copies the value located at the memory block identified by this instance to
        /// another located in the memory represented by given unmanaged pointer.
        /// </summary>
        /// <param name="destination">The managed pointer which points to the destination memory block.</param>
        public void WriteTo(ref T destination)
            => destination = Value;

        /// <summary>
        /// Copies the value located at the memory block identified by this instance to another location in the memory.
        /// </summary>
        /// <remarks>
        /// If size of type <typeparamref name="T"/> is greater than <typeparamref name="U"/>
        /// then not all bits will be copied. In this case, the copied bits depend on underlying
        /// hardware architecture and endianess of bytes in memory.
        /// </remarks>
        /// <typeparam name="U">The type indicating size of the destination memory block.</typeparam>
        /// <param name="destination">The destination memory block.</param>
        public void WriteTo<U>(UnmanagedMemory<U> destination)
            where U: unmanaged
            => WriteTo(destination.pointer);

        /// <summary>
        /// Creates a copy of value in the managed heap.
        /// </summary>
        /// <returns>A boxed copy in the managed heap.</returns>
        public StrongBox<T> CopyToManagedHeap() => new StrongBox<T>(Value);

		/// <summary>
		/// Creates bitwise copy of unmanaged buffer.
		/// </summary>
		/// <returns>Bitwise copy of unmanaged buffer.</returns>
        public UnmanagedMemory<T> Copy()
            => pointer.IsNull ? this : Box(Value);

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
		public UnmanagedMemory<U> As<U>() 
            where U: unmanaged
            => new UnmanagedMemory<U>(pointer.As<U>());

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
        /// Obtains typed pointer to the memory block identified by this instance.
        /// </summary>
        /// <param name="memory">The memory block reference.</param>
        public static implicit operator Pointer<T>(UnmanagedMemory<T> memory)
            => memory.pointer;

        /// <summary>
        /// Obtains span to the unmanaged memory.
        /// </summary>
        /// <param name="memory">The memory block reference.</param>
        public static implicit operator Span<T>(UnmanagedMemory<T> memory)
            => memory.pointer.IsNull ? default : new Span<T>(memory.pointer, 1);

        /// <summary>
        /// Extracts value from the unmanaged memory.
        /// </summary>
        /// <param name="memory">The memory block reference.</param>
        public static implicit operator T(UnmanagedMemory<T> memory) => memory.Value;

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
        public void Dispose()
        {
            FreeMem(pointer.Address);
            this = default;
        }

        /// <summary>
        /// Indicates that this pointer represents the same memory location as other pointer.
        /// </summary>
        /// <typeparam name="U">The type of the another pointer.</typeparam>
        /// <param name="other">The pointer to be compared.</param>
        /// <returns><see langword="true"/>, if this pointer represents the same memory location as other pointer; otherwise, <see langword="false"/>.</returns>
        public bool Equals<U>(UnmanagedMemory<U> other)
            where U: unmanaged
            => pointer.Equals(other.pointer);

        bool IEquatable<UnmanagedMemory<T>>.Equals(UnmanagedMemory<T> other) => Equals(other);

        /// <summary>
        /// Computes hash code of the pointer itself (i.e. address), not of the memory content.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => pointer.GetHashCode();

        /// <summary>
        /// Indicates that this pointer represents the same memory location as other pointer.
        /// </summary>
        /// <param name="other">The object of type <see cref="UnmanagedMemory{T}"/>, <see cref="IntPtr"/> or <see cref="UIntPtr"/> to be compared.</param>
        /// <returns><see langword="true"/>, if this pointer represents the same memory location as other pointer; otherwise, <see langword="false"/>.</returns>
		public override bool Equals(object other)
        {
            switch(other)
            {
                case IntPtr pointer:
                    return this.pointer.Address == pointer;
                case UIntPtr pointer:
                    return new UIntPtr(this.pointer) == pointer;
                case UnmanagedMemory<T> box:
                    return Equals(box);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns address of this memory in hexadecimal format.
        /// </summary>
        /// <returns>The addres of this memory.</returns>
		public override string ToString() => new IntPtr(pointer).ToString("X");

        /// <summary>
        /// Computes bitwise equality between two blocks of memory.
        /// </summary>
        /// <param name="other">The pointer identifies block of memory to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        public bool BitwiseEquals(Pointer<T> other) => pointer.BitwiseEquals(other, 1);

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <param name="other">The pointer identifies block of memory to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        public int BitwiseCompare(Pointer<T> other) => pointer.BitwiseCompare(other, 1);

        /// <summary>
        /// Determines whether the value stored in the memory identified by this pointer is equal to the given value.
        /// </summary>
        /// <param name="other">The value to be compared.</param>
        /// <param name="comparer">The object implementing comparison algorithm.</param>
        /// <returns><see langword="true"/>, if the value stored in the memory identified by this pointer is equal to the given value; otherwise, <see langword="false"/>.</returns>
        public bool Equals(T other, IEqualityComparer<T> comparer) => pointer.Equals(other, comparer);

        /// <summary>
        /// Computes hash code of the value stored in the memory identified by this pointer.
        /// </summary>
        /// <param name="comparer">The object implementing custom hash function.</param>
        /// <returns>The hash code of the value stored in the memory identified by this pointer.</returns>
        public int GetHashCode(IEqualityComparer<T> comparer) => pointer.GetHashCode(comparer);

        /// <summary>
        /// Indicates that the first pointer represents the same memory location as the second pointer.
        /// </summary>
        /// <param name="first">The first pointer to be compared.</param>
        /// <param name="second">The second pointer to be compared.</param>
        /// <returns><see langword="true"/>, if the first pointer represents the same memory location as the second pointer; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(UnmanagedMemory<T> first, UnmanagedMemory<T> second) => first.pointer == second.pointer;

        /// <summary>
        /// Indicates that the first pointer represents the different memory location as the second pointer.
        /// </summary>
        /// <param name="first">The first pointer to be compared.</param>
        /// <param name="second">The second pointer to be compared.</param>
        /// <returns><see langword="true"/>, if the first pointer represents the different memory location as the second pointer; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static bool operator !=(UnmanagedMemory<T> first, UnmanagedMemory<T> second) => first.pointer != second.pointer;

        /// <summary>
        /// Indicates that the first pointer represents the same memory location as the second pointer.
        /// </summary>
        /// <param name="first">The first pointer to be compared.</param>
        /// <param name="second">The second pointer to be compared.</param>
        /// <returns><see langword="true"/>, if the first pointer represents the same memory location as the second pointer; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static bool operator ==(UnmanagedMemory<T> first, Pointer<T> second) => first.pointer == second;

        /// <summary>
        /// Indicates that the first pointer represents the different memory location as the second pointer.
        /// </summary>
        /// <param name="first">The first pointer to be compared.</param>
        /// <param name="second">The second pointer to be compared.</param>
        /// <returns><see langword="true"/>, if the first pointer represents the different memory location as the second pointer; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(UnmanagedMemory<T> first, Pointer<T> second) => first.pointer != second;
    }
}