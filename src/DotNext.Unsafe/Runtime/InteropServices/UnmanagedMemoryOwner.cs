using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DotNext.Runtime.InteropServices
{
    public class UnmanagedMemoryOwner : SafeHandle, ICloneable, IEquatable<UnmanagedMemoryOwner>
    {

    }

    /// <summary>
    /// Represents allocated unmanaged memory.
    /// </summary>
    /// <remarks>
    /// All elements are allocated in unmanaged memory not controlled by Garbage Collector.
    /// However, the unmanaged memory will be released automatically if GC collects
    /// the instance of this type.
    /// </remarks>
    /// <typeparam name="T">The type of elements in the unmanaged memory.</typeparam>
    public sealed class UnmanagedMemoryOwner<T> : SafeHandle, ICloneable, IEquatable<UnmanagedMemoryOwner<T>>, IEnumerable<T>
        where T : unmanaged
    {
        private int length;

        /// <summary>
        /// Allocates the block of unmanaged memory.
        /// </summary>
        /// <param name="length">The number of elements in the unmanaged memory.</param>
        /// <param name="zeroMem">Sets all bytes of allocated memory to zero.</param>
        public UnmanagedMemoryOwner(int length, bool zeroMem = true)
            : base(IntPtr.Zero, true)
        {
            var size = GetSize(length);
            handle = Marshal.AllocHGlobal(new IntPtr(size));
            GC.AddMemoryPressure(size);
            this.length = length;
            if(zeroMem)
                Memory.ClearBits(handle, size);
        }

        /// <summary>
        /// Allocates the block of unmanaged memory which is equal to size of type <typeparamref name="T"/>.
        /// </summary>
        public UnmanagedMemoryOwner()
            : this(1)
        {

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long GetSize(int length) => Math.BigMul(length, Pointer<T>.Size);

        /// <summary>
        /// Creates bitwise copy of the unmanaged memory.
        /// </summary>
        /// <returns>The independent copy of unmanaged memory.</returns>
        public UnmanagedMemoryOwner<T> Copy()
        {
            var copy = new UnmanagedMemoryOwner<T>(length, false);
            Memory.Copy(handle, copy.handle, Size);
            return copy;
        }

        /// <summary>
        /// Creates a copy of unmanaged memory inside of managed heap.
        /// </summary>
        /// <returns>A copy of unmanaged memory in the form of byte array.</returns>
        public byte[] ToByteArray() => IsInvalid ? Array.Empty<byte>() : Pointer.As<byte>().ToByteArray(Size);

        /// <summary>
        /// Copies elements from the unmanaged array into managed heap. 
        /// </summary>
        /// <returns>The array allocated in managed heap containing copied elements from unmanaged memory.</returns>
        public T[] ToArray()
        {
            if (IsInvalid)
                return Array.Empty<T>();
            var result = new T[length];
            WriteTo(result, 0, length);
            return result;
        }

        /// <summary>
        /// Represents unmanaged memory as stream.
        /// </summary>
        /// <returns>The stream of unmanaged memory.</returns>
        public Stream AsStream() => IsInvalid ? Stream.Null : Pointer.AsStream(length);

        /// <summary>
        /// Copies bytes from the memory location to the stream.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        public void WriteTo(Stream destination) => Pointer.WriteTo(destination, length);

        /// <summary>
        /// Copies bytes from the specified source stream into
        /// unmanaged memory.
        /// </summary>
        /// <param name="source">The readable stream.</param>
        /// <returns>The actual number of copied elements.</returns>
        public long ReadFrom(Stream source) => Pointer.ReadFrom(source, length);

        /// <summary>
        /// Copies bytes from the memory location to the stream asynchronously.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <returns>The task instance representing asynchronous state of the copying process.</returns>
        public Task WriteToAsync(Stream destination) => Pointer.As<byte>().WriteToAsync(destination, length);

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this object asynchronously.
        /// </summary>
        /// <param name="source">The source stream.</param>
        public Task<long> ReadFromAsync(Stream source) => Pointer.ReadFromAsync(source, length);

        /// <summary>
        /// Copies elements from the memory location to the managed array.
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <param name="offset">The position in the destination array from which copying begins.</param>
        /// <param name="count">The number of arrays elements to be copied.</param>
        /// <returns>The actual number of copied elements.</returns>
        public long WriteTo(T[] destination, long offset, long count) => Pointer.WriteTo(destination, offset, count);

        /// <summary>
        /// Copies elements from the specified array into
        /// the memory block identified by this object.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="offset">The position in the source array from which copying begins.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <returns>Actual number of copied elements.</returns>
        public long ReadFrom(T[] source, long offset, long count) =>
            Pointer.ReadFrom(source, offset, Math.Min(length, count));

        /// <summary>
        /// Copies elements from the memory location to the managed array.
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <returns>The actual number of copied elements.</returns>
        public long WriteTo(T[] destination)
            => Pointer.WriteTo(destination, 0, destination.LongLength.Min(length));

        /// <summary>
        /// Copies elements from the memory location to the managed array.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <returns>The actual number of copied elements.</returns>
        public long ReadFrom(T[] source)
            => Pointer.ReadFrom(source, 0L, source.LongLength);

        /// <summary>
        /// Copies elements from the current memory location to the specified memory location.
        /// </summary>
        /// <param name="destination">The target memory location.</param>
        public void WriteTo(Pointer<T> destination) => Pointer.WriteTo(destination, length);

        /// <summary>
        /// Copies bytes from the source memory to the memory identified by this object.
        /// </summary>
        /// <param name="source">The pointer to the source unmanaged memory.</param>
        public void ReadFrom(Pointer<T> source) => source.WriteTo(Pointer, length);

        /// <summary>
        /// Copies elements from the current memory location to the specified memory location.
        /// </summary>
        /// <param name="destination">The target memory location.</param>
        /// <returns>The actual number of copied elements.</returns>
        public long WriteTo(UnmanagedMemoryOwner<T> destination)
        {
            var count = Math.Min(length, destination.length);
            Pointer.WriteTo(destination.Pointer, count);
            return count;
        }

        /// <summary>
        /// Computes bitwise equality between two blocks of memory.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        public bool BitwiseEquals(Pointer<T> other) => Pointer.BitwiseEquals(other, length);

        /// <summary>
        /// Computes bitwise equality between this array and the specified managed array.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        public unsafe bool BitwiseEquals(T[] other)
        {
            if (other is null || other.LongLength != length)
                return false;
            fixed (T* ptr = other)
                return BitwiseEquals(ptr);
        }

        /// <summary>
        /// Computes bitwise equality between two blocks of memory.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        public bool BitwiseEquals(UnmanagedMemoryOwner<T> other)
            => !(other is null) && length == other.length && BitwiseEquals(other.Pointer);

        /// <summary>
        /// Bitwise comparison of the memory blocks.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        public int BitwiseCompare(Pointer<T> other) => Pointer.BitwiseCompare(other, length);

        /// <summary>
        /// Bitwise comparison of the memory blocks.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        public unsafe int BitwiseCompare(T[] other)
        {
            if (other is null)
                return 1;
            else if (length != other.LongLength)
                return ((long) length).CompareTo(other.LongLength);
            else
                fixed (T* ptr = other)
                    return BitwiseCompare(ptr);
        }

        /// <summary>
        /// Bitwise comparison of the memory blocks.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        public int BitwiseCompare(UnmanagedMemoryOwner<T> other)
        {
            if (other is null)
                return 1;
            else if (length != other.length)
                return length.CompareTo(other.length);
            else
                return BitwiseCompare(other.Pointer);
        }

        object ICloneable.Clone() => Copy();

        /// <summary>
        /// Gets the number of elements in the unmanaged memory.
        /// </summary>
        public int Length => length;

        /// <summary>
        /// Gets the size of allocated unmanaged memory, in bytes.
        /// </summary>
        public long Size => GetSize(length);

        /// <summary>
        /// Resizes a block of memory represented by this instance.
        /// </summary>
        /// <param name="length">The new number of elements in the unmanaged array.</param>
        public void Reallocate(int length)
        {
            long oldSize = Size, newSize = GetSize(this.length = length);
            handle = Marshal.ReAllocHGlobal(handle, new IntPtr(newSize));
            if(newSize > oldSize)
                GC.AddMemoryPressure(newSize - oldSize);
            else if(newSize < oldSize)
                GC.RemoveMemoryPressure(oldSize - newSize);
        }

        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        public void Clear()
        {
            if (handle == IntPtr.Zero)
                return;
            Memory.ClearBits(handle, Size);
        }

        /// <summary>
        /// Releases unmanaged memory.
        /// </summary>
        /// <returns><see langword="true"/> if unmanaged memory is released successfully; otherwise, <see langword="false"/>.</returns>
        protected override bool ReleaseHandle()
        {
            if(handle == IntPtr.Zero)
                return false;
            Marshal.FreeHGlobal(handle);
            GC.RemoveMemoryPressure(Size);
            handle = IntPtr.Zero;
            length = 0;
            return true;
        }

        /// <summary>
        /// Indicates that this object is no longer valid.
        /// </summary>
        public override bool IsInvalid => handle == IntPtr.Zero;

        /// <summary>
        /// Gets a span from the current instance.
        /// </summary>
        public unsafe Span<T> Span => IsInvalid ? default : new Span<T>(handle.ToPointer(), length);

        /// <summary>
        /// Gets a span of bytes from the current instance.
        /// </summary>
        public unsafe Span<byte> Bytes =>
            IsInvalid ? default : new Span<byte>(handle.ToPointer(), length * Pointer<T>.Size);

        /// <summary>
        /// Gets a pointer to the allocated unmanaged memory.
        /// </summary>
        public Pointer<T> Pointer => IsInvalid ? default : new Pointer<T>(handle);

        /// <summary>
        /// Gets a span from the specified instance.
        /// </summary>
        /// <param name="owner">The owner of allocated unmanaged memory.</param>
        public static implicit operator Span<T>(UnmanagedMemoryOwner<T> owner) => owner is null ? default : owner.Span;

        /// <summary>
        /// Gets a pointer to the allocated unmanaged memory.
        /// </summary>
        /// <param name="owner">The owner of allocated unmanaged memory.</param>
        public static implicit operator Pointer<T>(UnmanagedMemoryOwner<T> owner) => owner is null ? default : owner.Pointer;

        /// <summary>
        /// Determines whether the current object points to the
        /// same memory location as other object.
        /// </summary>
        /// <param name="other">The object to be compared.</param>
        /// <returns><see langword="true"/> if the current object points to the same memory location as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(UnmanagedMemoryOwner<T> other)
            => !(other is null) && handle == other.handle;

        /// <summary>
        /// Determines whether the current object points to the
        /// same memory location as other object.
        /// </summary>
        /// <param name="other">The object to be compared.</param>
        /// <returns><see langword="true"/> if the current object points to the same memory location as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
        {
            switch (other)
            {
                case UnmanagedMemoryOwner<T> owner:
                    return Equals(owner);
                case IntPtr ptr:
                    return handle == ptr;
                case Pointer<T> ptr:
                    return handle == ptr.Address;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Computes hash code of the pointer itself (i.e. address), not of the memory content.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => handle.GetHashCode();

        /// <summary>
        /// Returns address of this memory in hexadecimal format.
        /// </summary>
        /// <returns>The address of this memory.</returns>
        public override string ToString() => handle.ToString("X");

        /// <summary>
        /// Gets enumerator over all elements located in the unmanaged memory.
        /// </summary>
        /// <returns>The enumerator over all elements in the unmanaged memory.</returns>
        public Pointer<T>.Enumerator GetEnumerator() => Pointer.GetEnumerator(length);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Determines whether the two objects point to the same location of unmanaged memory.
        /// </summary>
        /// <param name="first">The first object to be compared.</param>
        /// <param name="second">The second object to be compared.</param>
        /// <returns><see langword="true"/> if <paramref name="first"/> points to the same memory location as <paramref name="second"/>; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(UnmanagedMemoryOwner<T> first, UnmanagedMemoryOwner<T> second)
            => Equals(first, second);

        /// <summary>
        /// Determines whether the two objects point to different locations of unmanaged memory.
        /// </summary>
        /// <param name="first">The first object to be compared.</param>
        /// <param name="second">The second object to be compared.</param>
        /// <returns><see langword="false"/> if <paramref name="first"/> points to the same memory location as <paramref name="second"/>; otherwise, <see langword="true"/>.</returns>
        public static bool operator !=(UnmanagedMemoryOwner<T> first, UnmanagedMemoryOwner<T> second)
            => !Equals(first, second);
    }
}