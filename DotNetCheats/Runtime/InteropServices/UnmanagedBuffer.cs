using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;

namespace Cheats.Runtime.InteropServices
{
    using static Threading.Tasks.TaskConverter;

    /// <summary>
    /// Represents unmanaged memory buffer located outside of managed heap.
    /// </summary>
    /// <remarks>
    /// Memory allocated by unmanaged buffer is not controlled by Garbage Collector.
	/// Therefore, it's your responsibility to release unmanaged memory using Dispose call.
    /// </remarks>
    /// <typeparam name="T">Type to be allocated in the unmanaged heap.</typeparam>
    public unsafe struct UnmanagedBuffer<T>: IUnmanagedMemory<T>, IBox<T>, IEquatable<UnmanagedBuffer<T>>
        where T: unmanaged
    {  
        /// <summary>
        /// Represents GC-friendly reference to the unmanaged memory.
        /// </summary>
		/// <remarks>
		/// Unmanaged memory allocated using handle can be reclaimed by GC automatically.
		/// </remarks>
        public sealed class Handle: UnmanagedMemoryHandle<T>
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
                => handle.IsClosed ? throw new ObjectDisposedException(handle.GetType().Name, "Handle is closed") : new UnmanagedBuffer<T>(handle.handle);
        }

        /// <summary>
		/// Size of unmanaged memory needed to allocate structure.
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

        public int ReadFrom(byte[] source)
        {
            if(IsInvalid)
                throw new NullPointerException();
            var size = Math.Min(Size, source.Length);
            fixed(byte* src = source)
                Memory.Copy(src, pointer, size);
            return size;
        }

        ulong IUnmanagedMemory<T>.ReadFrom(byte[] source) => (ulong)ReadFrom(source);

        public int ReadFrom(Stream source) => (int)Memory.ReadFromStream(source, pointer, Size);

        ulong IUnmanagedMemory<T>.ReadFrom(Stream source) => (ulong)Memory.ReadFromStream(source, pointer, Size);

		Task<ulong> IUnmanagedMemory<T>.ReadFromAsync(Stream source) => Memory.ReadFromStreamAsync(source, pointer, Size).Map(Convert.ToUInt64);

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
            => WriteTo<U>(destination.pointer);

        public int WriteTo(byte[] destination)
        {
            if(IsInvalid)
                throw new NullPointerException();
            else if(destination is null)
                throw new ArgumentNullException(nameof(destination));
            var size = Math.Min(Size, destination.Length);
            fixed(byte* dest = destination)
                Memory.Copy(pointer, dest, size);
            return size;
        }

        ulong IUnmanagedMemory<T>.WriteTo(byte[] destination) => (ulong)WriteTo(destination);

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

        public UnmanagedBuffer<T> Copy()
        {
            if(IsInvalid)
                return this;
            var result = AllocUnitialized();
            Memory.Copy(pointer, result.pointer, Size);
            return result;
        }

        object ICloneable.Clone() => Copy();

        public UnmanagedBuffer<U> Reinterpret<U>() 
            where U: unmanaged
        {
            if(IsInvalid)
                throw new NullPointerException();
            else if(Size < UnmanagedBuffer<U>.Size)
                throw new GenericArgumentException<U>("Target type should be the same size or less");
            else
                return new UnmanagedBuffer<U>(this);
        }

        public byte[] ToByteArray()
        {
            if(IsInvalid)
                throw new NullPointerException();
            var result = new byte[Size];
            fixed(byte* destination = result)
                Memory.Copy(pointer, destination, Size);
            return result;
        }

        private byte* Offset(ulong offset)
        {
            if(IsInvalid)
                throw new NullPointerException();
            else if(offset >= 0 && offset < (ulong)Size) 
                return (byte*)pointer + offset;
            else 
                throw new IndexOutOfRangeException($"Offset should be in range [0, {Size})");
        }

        public byte this[int offset]
        {
            get => this[checked((ulong)offset)];
            set => this[checked((ulong)offset)] = value;
        }

        [CLSCompliant(false)]
        public byte this[ulong offset]
        {
            get => Unsafe.ReadUnaligned<byte>(Offset(offset));
            set => Unsafe.WriteUnaligned(Offset(offset), value);
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

        public int BitwiseHashCode() => pointer == Memory.NullPtr ? 0 : Memory.GetHashCode(pointer, Size);

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
            if(pointer == Memory.NullPtr)
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