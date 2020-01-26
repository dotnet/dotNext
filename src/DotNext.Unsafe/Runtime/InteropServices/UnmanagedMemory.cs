using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents unstructured unmanaged memory.
    /// </summary>
    public sealed class UnmanagedMemory : UnmanagedMemoryHandle
    {
        private long size;

        /// <summary>
        /// Allocates the block of unmanaged memory.
        /// </summary>
        /// <param name="size">The size of unmanaged memory to be allocated, in bytes.</param>
        /// <param name="zeroMem">Sets all bytes of allocated memory to zero.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is less than 1.</exception>
        /// <exception cref="OutOfMemoryException">There is insufficient memory to satisfy the request.</exception>
        public UnmanagedMemory(long size, bool zeroMem = true) : base(size, zeroMem) => this.size = size;

        /// <summary>
        /// Gets size of allocated unmanaged memory, in bytes.
        /// </summary>
        public override long Size => size;

        /// <summary>
        /// Resizes a block of memory represented by this instance.
        /// </summary>
        /// <param name="size">The new number of bytes in the unmanaged array.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public void Reallocate(long size)
        {
            if (IsClosed)
                throw HandleClosed();
            if (IsInvalid)
                return;
            handle = Marshal.ReAllocHGlobal(handle, new IntPtr(size));
            var diff = size - this.size;
            if (diff > 0L)
                GC.AddMemoryPressure(diff);
            else if (diff < 0L)
                GC.RemoveMemoryPressure(Math.Abs(diff));
            this.size = size;
        }

        /// <summary>
        /// Creates bitwise copy of the unmanaged memory.
        /// </summary>
        /// <returns>The independent copy of unmanaged memory.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public UnmanagedMemory Copy()
        {
            if (IsClosed)
                throw HandleClosed();
            if (IsInvalid)
                return this;
            var copy = new UnmanagedMemory(Size, false);
            Memory.Copy(handle, copy.handle, Size);
            return copy;
        }

        /// <summary>
        /// Copies bytes from the memory location to the managed array.
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <param name="offset">The position in the destination array from which copying begins.</param>
        /// <param name="count">The number of bytes to be copied.</param>
        /// <returns>The actual number of copied bytes.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public long WriteTo(byte[] destination, long offset, long count) => Pointer.WriteTo(destination, offset, count);

        /// <summary>
        /// Copies bytes from the specified array into
        /// the memory block identified by this object.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="offset">The position in the source array from which copying begins.</param>
        /// <param name="count">The number of bytes to be copied.</param>
        /// <returns>Actual number of copied bytes.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public long ReadFrom(byte[] source, long offset, long count) =>
            Pointer.ReadFrom(source, offset, Math.Min(Size, count));

        /// <summary>
        /// Copies bytes from the memory location to the managed array.
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <returns>The actual number of copied bytes.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public long WriteTo(byte[] destination)
            => Pointer.WriteTo(destination, 0, destination.LongLength.Min(Size));

        /// <summary>
        /// Copies bytes from the memory location to the managed array.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <returns>The actual number of copied bytes.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public long ReadFrom(byte[] source)
            => Pointer.ReadFrom(source, 0L, source.LongLength);

        /// <summary>
        /// Gets a span from the specified instance.
        /// </summary>
        /// <param name="owner">The owner of allocated unmanaged memory.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public static implicit operator Span<byte>(UnmanagedMemory owner) => owner is null ? default : owner.Bytes;

        /// <summary>
        /// Gets a pointer to the allocated unmanaged memory.
        /// </summary>
        /// <param name="owner">The owner of allocated unmanaged memory.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public static implicit operator Pointer<byte>(UnmanagedMemory owner) => owner is null ? default : owner.Pointer;

        private protected override UnmanagedMemoryHandle Clone() => Copy();
    }

    /// <summary>
    /// Represents array-like unmanaged memory.
    /// </summary>
    /// <remarks>
    /// All elements are allocated in unmanaged memory not controlled by Garbage Collector.
    /// However, the unmanaged memory will be released automatically if GC collects
    /// the instance of this type.
    /// </remarks>
    /// <typeparam name="T">The type of elements in the unmanaged memory.</typeparam>
    public sealed class UnmanagedMemory<T> : UnmanagedMemoryHandle, IUnmanagedArray<T>
        where T : unmanaged
    {
        private int length;

        /// <summary>
        /// Allocates the block of unmanaged memory.
        /// </summary>
        /// <param name="length">The number of elements in the unmanaged memory.</param>
        /// <param name="zeroMem">Sets all bytes of allocated memory to zero.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than 1.</exception>
        /// <exception cref="OutOfMemoryException">There is insufficient memory to satisfy the request.</exception>
        public UnmanagedMemory(int length, bool zeroMem = true) : base(SizeOf<T>(length), zeroMem) => this.length = length;

        /// <summary>
        /// Allocates the block of unmanaged memory which is equal to size of type <typeparamref name="T"/>.
        /// </summary>
        /// <exception cref="OutOfMemoryException">There is insufficient memory to satisfy the request.</exception>
        public UnmanagedMemory()
            : this(1)
        {

        }

        /// <summary>
        /// Allocates a new unmanaged memory and place the given value into it.
        /// </summary>
        /// <param name="value">The value to be placed into unmanaged memory.</param>
        /// <returns>The object representing allocated unmanaged memory.</returns>
        /// <exception cref="OutOfMemoryException">There is insufficient memory to satisfy the request.</exception>
        public static UnmanagedMemory<T> Box(T value)
        {
            var memory = new UnmanagedMemory<T>(1, false);
            memory.Pointer.Value = value;
            return memory;
        }

        /// <summary>
        /// Creates bitwise copy of the unmanaged memory.
        /// </summary>
        /// <returns>The independent copy of unmanaged memory.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public UnmanagedMemory<T> Copy()
        {
            if (IsClosed)
                throw HandleClosed();
            if (IsInvalid)
                return this;
            var copy = new UnmanagedMemory<T>(length, false);
            Memory.Copy(handle, copy.handle, Size);
            return copy;
        }

        private protected override UnmanagedMemoryHandle Clone() => Copy();

        /// <summary>
        /// Copies elements from the unmanaged array into managed heap. 
        /// </summary>
        /// <returns>The array allocated in managed heap containing copied elements from unmanaged memory.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public T[] ToArray()
        {
            if (IsClosed)
                throw HandleClosed();
            if (IsInvalid)
                return Array.Empty<T>();
            var result = new T[length];
            WriteTo(result, 0, length);
            return result;
        }

        /// <summary>
        /// Copies elements from the memory location to the managed array.
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <param name="offset">The position in the destination array from which copying begins.</param>
        /// <param name="count">The number of array elements to be copied.</param>
        /// <returns>The actual number of copied elements.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public long WriteTo(T[] destination, long offset, long count) => Pointer.WriteTo(destination, offset, count);

        /// <summary>
        /// Copies elements from the specified array into
        /// the memory block identified by this object.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="offset">The position in the source array from which copying begins.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <returns>Actual number of copied elements.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public long ReadFrom(T[] source, long offset, long count) =>
            Pointer.ReadFrom(source, offset, Math.Min(length, count));

        /// <summary>
        /// Copies elements from the memory location to the managed array.
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <returns>The actual number of copied elements.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public long WriteTo(T[] destination)
            => Pointer.WriteTo(destination, 0, destination.LongLength.Min(length));

        /// <summary>
        /// Copies elements from the memory location to the managed array.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <returns>The actual number of copied elements.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public long ReadFrom(T[] source)
            => Pointer.ReadFrom(source, 0L, source.LongLength);

        /// <summary>
        /// Copies elements from the current memory location to the specified memory location.
        /// </summary>
        /// <param name="destination">The target memory location.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public void WriteTo(Pointer<T> destination) => Pointer.WriteTo(destination, length);

        /// <summary>
        /// Copies bytes from the source memory to the memory identified by this object.
        /// </summary>
        /// <param name="source">The pointer to the source unmanaged memory.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public void ReadFrom(Pointer<T> source) => source.WriteTo(Pointer, length);

        /// <summary>
        /// Copies elements from the current memory location to the specified memory location.
        /// </summary>
        /// <param name="destination">The target memory location.</param>
        /// <returns>The actual number of copied elements.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public long WriteTo(UnmanagedMemory<T> destination)
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
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public bool BitwiseEquals(Pointer<T> other) => Pointer.BitwiseEquals(other, length);

        /// <summary>
        /// Computes bitwise equality between this array and the specified managed array.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public unsafe bool BitwiseEquals(T[] other)
        {
            if (other is null || other.LongLength != length)
                return false;
            fixed (T* ptr = other)
                return BitwiseEquals(ptr);
        }

        /// <summary>
        /// Bitwise comparison of the memory blocks.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public int BitwiseCompare(Pointer<T> other) => Pointer.BitwiseCompare(other, length);

        /// <summary>
        /// Bitwise comparison of the memory blocks.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public unsafe int BitwiseCompare(T[] other)
        {
            if (other is null)
                return 1;
            else if (length != other.LongLength)
                return ((long)length).CompareTo(other.LongLength);
            else
                fixed (T* ptr = other)
                    return BitwiseCompare(ptr);
        }

        /// <summary>
        /// Gets the number of elements in the unmanaged memory.
        /// </summary>
        public int Length => length;

        /// <summary>
        /// Gets the size of allocated unmanaged memory, in bytes.
        /// </summary>
        public override long Size => SizeOf<T>(length);

        /// <summary>
        /// Resizes a block of memory represented by this instance.
        /// </summary>
        /// <param name="length">The new number of elements in the unmanaged array.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public void Reallocate(int length)
        {
            if (IsClosed)
                throw HandleClosed();
            if (IsInvalid)
                return;
            long oldSize = Size, newSize = SizeOf<T>(this.length = length);
            handle = Marshal.ReAllocHGlobal(handle, new IntPtr(newSize));
            var diff = newSize - oldSize;
            if (diff > 0L)
                GC.AddMemoryPressure(diff);
            else if (diff < 0L)
                GC.RemoveMemoryPressure(Math.Abs(diff));
        }

        /// <summary>
        /// Gets a span from the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public unsafe Span<T> Span
        {
            get
            {
                if (IsClosed)
                    throw HandleClosed();
                else if (IsInvalid)
                    return default;
                else
                    return new Span<T>(handle.ToPointer(), length);
            }
        }

        /// <summary>
        /// Gets a pointer to the allocated unmanaged memory.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public new Pointer<T> Pointer
        {
            get
            {
                if (IsClosed)
                    throw HandleClosed();
                else if (IsInvalid)
                    return default;
                else
                    return new Pointer<T>(handle);
            }
        }

        /// <summary>
        /// Gets a span from the specified instance.
        /// </summary>
        /// <param name="owner">The owner of allocated unmanaged memory.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public static implicit operator Span<T>(UnmanagedMemory<T> owner) => owner is null ? default : owner.Span;

        /// <summary>
        /// Gets a pointer to the allocated unmanaged memory.
        /// </summary>
        /// <param name="owner">The owner of allocated unmanaged memory.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public static implicit operator Pointer<T>(UnmanagedMemory<T> owner) => owner is null ? default : owner.Pointer;

        /// <summary>
        /// Gets enumerator over all elements located in the unmanaged memory.
        /// </summary>
        /// <returns>The enumerator over all elements in the unmanaged memory.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public Pointer<T>.Enumerator GetEnumerator() => Pointer.GetEnumerator(length);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}