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

        private readonly Pointer<T> pointer;

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

        IntPtr IUnmanagedMemory.Address => pointer.Address;

        ReadOnlySpan<T> IUnmanagedMemory<T>.Span => this;

        /// <summary>
        /// Gets or sets value stored in unmanaged memory.
        /// </summary>
        public T Value
		{
			get => pointer.Read(MemoryAccess.Aligned);
			set => pointer.Write(MemoryAccess.Aligned, value);
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

        public void ReadFrom<U>(Pointer<U> source)
            where U: unmanaged
            => new UnmanagedMemory<U>(source).WriteTo(pointer);

        public void WriteTo<U>(Pointer<U> destination)
            where U: unmanaged
            => pointer.As<byte>().WriteTo(destination.As<byte>(), Math.Min(Pointer<T>.Size, Pointer<U>.Size));

        public void WriteTo(ref T destination)
            => destination = Value;

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

        public static implicit operator Pointer<T>(UnmanagedMemory<T> buffer)
            => buffer.pointer;

        public static implicit operator ReadOnlySpan<T>(UnmanagedMemory<T> buffer)
            => buffer.pointer.IsNull ? new ReadOnlySpan<T>() : new ReadOnlySpan<T>(buffer.pointer, 1);

        public static implicit operator T(UnmanagedMemory<T> heap) => heap.Value;

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
        public void Dispose() => FreeMem(pointer.Address);

        public bool Equals<U>(UnmanagedMemory<U> other)
            where U: unmanaged
            => pointer.Equals(other.pointer);

        bool IEquatable<UnmanagedMemory<T>>.Equals(UnmanagedMemory<T> other) => Equals(other);

        public override int GetHashCode() => pointer.GetHashCode();

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

        public bool BitwiseEquals(Pointer<T> other) => pointer.BitwiseEquals(other, 1);

        public int BitwiseCompare(Pointer<T> other) => pointer.BitwiseCompare(other, 1);

        public bool Equals(T other, IEqualityComparer<T> comparer)
            => pointer.Equals(other, comparer);

        public int GetHashCode(IEqualityComparer<T> comparer)
            => pointer.GetHashCode(comparer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(UnmanagedMemory<T> first, UnmanagedMemory<T> second) => first.pointer == second.pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static bool operator !=(UnmanagedMemory<T> first, UnmanagedMemory<T> second) => first.pointer != second.pointer;

        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static bool operator ==(UnmanagedMemory<T> first, Pointer<T> second) => first.pointer == second;

        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(UnmanagedMemory<T> first, Pointer<T> second) => first.pointer != second;
    }
}