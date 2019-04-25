using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices
{
    using Reflection;

    /// <summary>
    /// Represents a block of allocated unmanaged memory of the specified size.
    /// </summary>
    public unsafe struct UnmanagedMemory : IUnmanagedMemory, IDisposable, IEquatable<UnmanagedMemory>, IEnumerable<byte>
    {
        /// <summary>
		/// Represents GC-friendly reference to the unmanaged memory.
		/// </summary>
		/// <remarks>
		/// Unmanaged memory allocated using handle can be reclaimed by GC automatically.
		/// </remarks>
        public sealed class Handle : UnmanagedMemoryHandle
        {
            private Handle(UnmanagedMemory memory, bool ownsHandle)
                : base(memory, ownsHandle)
            {
                Size = memory.Size;
            }

            /// <summary>
            /// Allocates a new unmanaged memory of the given size and associate it with handle.
            /// </summary>
            /// <remarks>
            /// The handle instantiated with this constructor has ownership over unmanaged memory.
            /// Unmanaged memory will be released when Garbage Collector reclaims instance of this handle
            /// or <see cref="Dispose()"/> will be called directly.
            /// </remarks>
            /// <param name="size">The number of bytes to be allocated in the unmanaged memory.</param>
            /// <param name="zeroMem">Sets all bytes of allocated memory to zero.</param>
            public Handle(long size, bool zeroMem = true)
                : this(new UnmanagedMemory(size, zeroMem), true)
            {

            }

            /// <summary>
            /// Initializes a new handle for the given unmanaged memory.
            /// </summary>
            /// <remarks>
            /// The handle instantiated with this constructor doesn't have ownership over unmanaged memory.
            /// </remarks>
            /// <param name="memory">Already allocated memory.</param>
            public Handle(UnmanagedMemory memory)
                : this(memory, false)
            {
            }

            /// <summary>
            /// Releases referenced unmanaged memory.
            /// </summary>
            /// <returns><see langword="true"/>, if this handle is valid; otherwise, <see langword="false"/>.</returns>
			protected override bool ReleaseHandle() => Release(handle);

            private protected override UnmanagedMemoryHandle Clone() => new Handle(Conversion<Handle, UnmanagedMemory>.Converter(this).Copy(), true);

            /// <summary>
            /// Gets size of allocated unmanaged memory, in bytes.
            /// </summary>
            public override long Size { get; }

            /// <summary>
			/// Converts handle into unmanaged memory structure.
			/// </summary>
			/// <param name="handle">Handle to convert.</param>
			/// <exception cref="ObjectDisposedException">Handle is closed.</exception>
            public static implicit operator UnmanagedMemory(Handle handle)
            {
                if (handle is null)
                    return default;
                else if (handle.IsClosed)
                    throw handle.HandleClosed();
                else
                    return new UnmanagedMemory(handle.handle, handle.Size);
            }
        }

        private readonly long size;

        /// <summary>
        /// Represents address of the allocated memory.
        /// </summary>
        public readonly IntPtr Address;

        internal UnmanagedMemory(IntPtr address, long size)
        {
            Address = address;
            this.size = size;
        }

        /// <summary>
        /// Allocates a new unmanaged memory.
        /// </summary>
        /// <param name="size">The number of bytes to be allocated.</param>
        /// <param name="zeroMem">Sets all bytes of allocated memory to zero.</param>
        public UnmanagedMemory(long size, bool zeroMem = true)
        {
            Address = Alloc(this.size = size, zeroMem);
            GC.AddMemoryPressure(size);
        }

        /// <summary>
        /// Indicates that the memory block is empty.
        /// </summary>
        public bool IsEmpty
        {
            get => Address == IntPtr.Zero || Size == 0L;
        }

        /// <summary>
		/// Gets or sets byte in the memory at the specified zero-based offset.
		/// </summary>
		/// <param name="offset">Offset of the requested byte.</param>
		/// <returns>The byte value from the memory.</returns>
		/// <exception cref="NullPointerException">This memory is not allocated.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Invalid offset.</exception>
        public byte this[long offset]
        {
            get
            {
                if (Address == IntPtr.Zero)
                    throw new NullPointerException();
                else if (offset < 0L || offset >= Size)
                    throw new ArgumentOutOfRangeException(nameof(offset), offset, ExceptionMessages.InvalidOffsetValue(Size));
                else
                    return *(Address.ToPointer<byte>() + offset);
            }
            set
            {
                if (Address == IntPtr.Zero)
                    throw new NullPointerException();
                else if (offset < 0L || offset >= Size)
                    throw new ArgumentOutOfRangeException(nameof(offset), offset, ExceptionMessages.InvalidOffsetValue(Size));
                else
                    *(Address.ToPointer<byte>() + offset) = value;
            }
        }

        internal static IntPtr Alloc(long size, bool zeroMem)
        {
            var address = Marshal.AllocHGlobal(new IntPtr(size));
            if (zeroMem)
                Memory.ClearBits(address, size);
            return address;
        }

        internal static IntPtr Realloc(IntPtr memory, long newSize) => Marshal.ReAllocHGlobal(memory, new IntPtr(newSize));

        internal static bool Release(IntPtr memory)
        {
            if (memory == IntPtr.Zero)
                return false;
            Marshal.FreeHGlobal(memory);
            return true;
        }

        IntPtr IUnmanagedMemory.Address => Address;

        /// <summary>
        /// Returns pointer to unmanaged memory in the form of managed pointer to type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of managed pointer.</typeparam>
        /// <returns>Managed typed pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AsRef<T>() where T : unmanaged => ref Unsafe.AsRef<T>(Address.ToPointer());

        /// <summary>
        /// Obtains typed unmanaged pointer to the allocated memory.
        /// </summary>
        /// <typeparam name="T">The type of unmanaged pointer.</typeparam>
        /// <returns>The unmanaged pointer.</returns>
        public Pointer<T> ToPointer<T>() where T : unmanaged => new Pointer<T>(Address);

        /// <summary>
        /// Creates bitwise copy of the unmanaged memory.
        /// </summary>
        /// <returns>Bitwise copy of the unmanaged memory.</returns>
        public UnmanagedMemory Copy()
        {
            var result = new UnmanagedMemory(Size);
            Memory.Copy(Address, result.Address, Size);
            return result;
        }

        object ICloneable.Clone() => Copy();

        /// <summary>
        /// Gets or sets number of allocated bytes.
        /// </summary>
        /// <remarks>
        /// If size is changed, the contents of this memory block have been copied to the new block, and this memory block has been freed.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is invalid.</exception>
        public long Size
        {
            get => size;
            set
            {
                if (value <= 0L)
                    throw new ArgumentOutOfRangeException(nameof(value));
                else if (value == size)
                    return;
                else if (IsEmpty)
                    this = new UnmanagedMemory(value);
                else
                    this = new UnmanagedMemory(Realloc(Address, value), value);
            }
        }

        /// <summary>
        /// Determines whether this object points to the same memory block as other object.
        /// </summary>
        /// <param name="other">The unmanaged memory holder to be compared.</param>
        /// <returns><see langword="true"/>, if this object points to the same memory block as other object; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(UnmanagedMemory other) => Address == other.Address;

        /// <summary>
        /// Determines whether this object points to the same memory block as other object.
        /// </summary>
        /// <param name="other">The unmanaged memory holder to be compared.</param>
        /// <returns><see langword="true"/>, if this object points to the same memory block as other object; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
        {
            switch (other)
            {
                case UnmanagedMemory memory:
                    return Equals(memory);
                case IntPtr pointer:
                    return Address == pointer;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines whether two objects point to the same block of unmanaged memory.
        /// </summary>
        /// <param name="first">The first memory pointer to be compared.</param>
        /// <param name="second">The second memory pointer to be compared.</param>
        /// <returns><see langword="true"/>, if two objects point to the same block of unmanaged memory; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(UnmanagedMemory first, UnmanagedMemory second) => first.Address == second.Address;

        /// <summary>
        /// Determines whether two objects point to the different blocks of unmanaged memory.
        /// </summary>
        /// <param name="first">The first memory pointer to be compared.</param>
        /// <param name="second">The second memory pointer to be compared.</param>
        /// <returns><see langword="true"/>, if two objects point to the different blocks of unmanaged memory; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(UnmanagedMemory first, UnmanagedMemory second) => first.Address != second.Address;

        /// <summary>
        /// Gets enumerator over all bytes in the allocated memory.
        /// </summary>
        /// <returns>The enumerator over all bytes in the allocated memory.</returns>
        public Pointer<byte>.Enumerator GetEnumerator() => ToPointer<byte>().GetEnumerator(Size);

        IEnumerator<byte> IEnumerable<byte>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Returns address of this memory in hexadecimal format.
        /// </summary>
        /// <returns>The addres of this memory.</returns>
        public override string ToString() => Address.ToString("X");

        /// <summary>
        /// Obtains hash code of the unmanaged memory address.
        /// </summary>
        /// <returns>The hash code of the unmanaged memory address.</returns>
        public override int GetHashCode() => Address.GetHashCode();

        /// <summary>
        /// Releases allocated unmanaged memory.
        /// </summary>
        public void Dispose()
        {
            Release(Address);
            GC.RemoveMemoryPressure(Size);
            this = default;
        }
    }

    /// <summary>
    /// Represents unmanaged structured memory located outside of managed heap.
    /// </summary>
    /// <remarks>
    /// Allocated memory is not controlled by Garbage Collector.
	/// Therefore, it's developer responsibility to release unmanaged memory using <see cref="IDisposable.Dispose"/> call.
    /// </remarks>
    /// <typeparam name="T">Type to be allocated in the unmanaged heap.</typeparam>
    public unsafe struct UnmanagedMemory<T> : IUnmanagedMemory<T>, IStrongBox, IEquatable<UnmanagedMemory<T>>
        where T : unmanaged
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
            /// <param name="zeroMem">Sets all bytes of allocated memory to zero.</param>
            public Handle(bool zeroMem = true)
                : this(new UnmanagedMemory<T>(zeroMem), true)
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
                : this(new UnmanagedMemory<T>(value), true)
            {
            }

            /// <summary>
            /// Initializes a new handle for the given unmanaged memory.
            /// </summary>
            /// <remarks>
            /// The handle instantiated with this constructor doesn't have ownership over unmanaged memory.
            /// </remarks>
            /// <param name="buffer">Already allocated memory.</param>
			public Handle(UnmanagedMemory<T> buffer)
                : this(buffer, false)
            {
            }

            /// <summary>
            /// Obtains span object pointing to the allocated unmanaged memory.
            /// </summary>
            public override Span<T> Span => new Span<T>((void*)handle, 1);

            /// <summary>
            /// Gets size of allocated unmanaged memory, in bytes.
            /// </summary>
            public override long Size => Pointer<T>.Size;

            private protected override UnmanagedMemoryHandle Clone() => new Handle(Conversion<Handle, UnmanagedMemory<T>>.Converter(this).Value);

            /// <summary>
            /// Releases referenced unmanaged memory.
            /// </summary>
            /// <returns><see langword="true"/>, if this handle is valid; otherwise, <see langword="false"/>.</returns>
			protected override bool ReleaseHandle() => UnmanagedMemory.Release(handle);

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
                    throw handle.HandleClosed();
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
        /// Allocates a new unmanaged memory of size necessary to place type <typeparamref name="T"/> into it.
        /// </summary>
        /// <param name="zeroMem">Sets all bytes of allocated memory to zero.</param>
        public UnmanagedMemory(bool zeroMem) => pointer = new Pointer<T>(UnmanagedMemory.Alloc(Pointer<T>.Size, zeroMem));

        /// <summary>
        /// Allocates a new unmanaged memory and place the given value into it.
        /// </summary>
        /// <param name="value">The value to be placed into unmanaged memory.</param>
        public UnmanagedMemory(T value)
            => pointer = new Pointer<T>(UnmanagedMemory.Alloc(Pointer<T>.Size, false))
            {
                Value = value
            };

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

        /// <summary>
        /// Converts unmanaged pointer into managed pointer.
        /// </summary>
        /// <returns>Managed pointer.</returns>
        /// <exception cref="NullPointerException">This pointer is null.</exception>
        public ref T Ref
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref pointer.Ref;
        }

        Span<T> IUnmanagedMemory<T>.Span => new Span<T>(pointer, 1);

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
            where U : unmanaged
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
            where U : unmanaged
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
            where U : unmanaged
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
            => pointer.IsNull ? this : new UnmanagedMemory<T>(Value);

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
            where U : unmanaged
            => new UnmanagedMemory<U>(pointer.As<U>());

        /// <summary>
        /// Gets or sets byte in the memory at the specified zero-based offset.
        /// </summary>
        /// <param name="offset">Offset of the requested byte.</param>
        /// <returns>The byte value from the memory.</returns>
        /// <exception cref="NullPointerException">This memory is not allocated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Invalid offset.</exception>
        public byte this[long offset]
        {
            get
            {
                if (Address == IntPtr.Zero)
                    throw new NullPointerException();
                else if (offset < 0L || offset >= Pointer<T>.Size)
                    throw new ArgumentOutOfRangeException(nameof(offset), offset, ExceptionMessages.InvalidOffsetValue(Pointer<T>.Size));
                else
                    return *Address.ToPointer<byte>();
            }
            set
            {
                if (Address == IntPtr.Zero)
                    throw new NullPointerException();
                else if (offset < 0L || offset >= Pointer<T>.Size)
                    throw new ArgumentOutOfRangeException(nameof(offset), offset, ExceptionMessages.InvalidOffsetValue(Pointer<T>.Size));
                else
                    *Address.ToPointer<byte>() = value;
            }
        }

        /// <summary>
        /// Obtains typed pointer to the memory block identified by this instance.
        /// </summary>
        /// <param name="memory">The memory block reference.</param>
        public static implicit operator Pointer<T>(UnmanagedMemory<T> memory)
            => memory.pointer;

        /// <summary>
        /// Extracts value from the unmanaged memory.
        /// </summary>
        /// <param name="memory">The memory block reference.</param>
        public static implicit operator T(UnmanagedMemory<T> memory) => memory.Value;

        /// <summary>
        /// Provides unstructured access to the unmanaged memory.
        /// </summary>
        /// <param name="memory">The memory block reference.</param>
        public static implicit operator UnmanagedMemory(UnmanagedMemory<T> memory) => new UnmanagedMemory(memory.Address, Pointer<T>.Size);

        /// <summary>
        /// Releases unmanaged memory associated with the boxed type.
        /// </summary>
        public void Dispose()
        {
            UnmanagedMemory.Release(pointer.Address);
            this = default;
        }

        /// <summary>
        /// Indicates that this pointer represents the same memory location as other pointer.
        /// </summary>
        /// <typeparam name="U">The type of the another pointer.</typeparam>
        /// <param name="other">The pointer to be compared.</param>
        /// <returns><see langword="true"/>, if this pointer represents the same memory location as other pointer; otherwise, <see langword="false"/>.</returns>
        public bool Equals<U>(UnmanagedMemory<U> other)
            where U : unmanaged
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
            switch (other)
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
		public override string ToString() => pointer.ToString();

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