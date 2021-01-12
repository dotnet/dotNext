using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents managed wrapper of the unmanaged memory.
    /// </summary>
    /// <remarks>
    /// The allocated unmanaged memory is not controlled by GC. However, the unmanaged memory 
    /// will be released automatically if GC collects the instance of this type.
    /// </remarks>
    public abstract class UnmanagedMemoryHandle : SafeHandle, ICloneable, IEquatable<UnmanagedMemoryHandle>, IUnmanagedMemory
    {
        private protected UnmanagedMemoryHandle(long size, bool zeroMem)
            : base(default, true)
        {
            handle = Marshal.AllocHGlobal(new IntPtr(size));
            GC.AddMemoryPressure(size);
            if (zeroMem)
                Memory.ClearBits(handle, size);
        }

        private protected ObjectDisposedException HandleClosed() => new ObjectDisposedException(handle.GetType().Name, ExceptionMessages.HandleClosed);

        /// <summary>
        /// Indicates that this object is no longer valid.
        /// </summary>
		public sealed override bool IsInvalid => handle == default;

        private protected abstract UnmanagedMemoryHandle Clone();

        object ICloneable.Clone() => Clone();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static long SizeOf<T>(int length)
            where T : unmanaged
            => Math.BigMul(length, sizeof(T));

        /// <summary>
        /// Gets size of allocated unmanaged memory, in bytes.
        /// </summary>
        public abstract long Size { get; }

        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public void Clear()
        {
            if (IsClosed)
                throw HandleClosed();
            Memory.ClearBits(handle, Size);
        }

        /// <summary>
        /// Releases unmanaged memory.
        /// </summary>
        /// <returns><see langword="true"/> if unmanaged memory is released successfully; otherwise, <see langword="false"/>.</returns>
        protected sealed override bool ReleaseHandle()
        {
            if (IsInvalid)
                return false;
            Marshal.FreeHGlobal(handle);
            GC.RemoveMemoryPressure(Size);
            handle = default;
            return true;
        }

        /// <summary>
        /// Gets a pointer to the allocated unmanaged memory.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public Pointer<byte> Pointer
        {
            get
            {
                if (IsClosed)
                    throw HandleClosed();
                else if (IsInvalid)
                    return default;
                else
                    return new Pointer<byte>(handle);
            }
        }

        /// <summary>
        /// Gets a span of bytes from the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public unsafe Span<byte> Bytes
        {
            get
            {
                if (IsClosed)
                    throw HandleClosed();
                else if (IsInvalid)
                    return default;
                else
                    return new Span<byte>(handle.ToPointer(), checked((int)Size));
            }
        }

        /// <summary>
        /// Represents unmanaged memory as stream.
        /// </summary>
        /// <returns>The stream of unmanaged memory.</returns>
        public Stream AsStream() => Pointer.AsStream(Size);

        /// <summary>
        /// Creates a copy of unmanaged memory inside of managed heap.
        /// </summary>
        /// <returns>A copy of unmanaged memory in the form of byte array.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public byte[] ToByteArray() => Pointer.ToByteArray(Size);

        /// <summary>
        /// Copies bytes from the memory location to the stream.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public void WriteTo(Stream destination) => Pointer.WriteTo(destination, Size);

        /// <summary>
        /// Copies bytes from the specified source stream into
        /// unmanaged memory.
        /// </summary>
        /// <param name="source">The readable stream.</param>
        /// <returns>The actual number of copied elements.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public long ReadFrom(Stream source) => Pointer.ReadFrom(source, Size);

        /// <summary>
        /// Copies bytes from the memory location to the stream asynchronously.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <returns>The task instance representing asynchronous state of the copying process.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public Task WriteToAsync(Stream destination) => Pointer.WriteToAsync(destination, Size);

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this object asynchronously.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public Task<long> ReadFromAsync(Stream source) => Pointer.ReadFromAsync(source, Size);

        /// <summary>
        /// Copies elements from the current memory location to the specified memory location.
        /// </summary>
        /// <param name="destination">The target memory location.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public void WriteTo(Pointer<byte> destination) => Pointer.WriteTo(destination, Size);

        /// <summary>
        /// Copies bytes from the source memory to the memory identified by this object.
        /// </summary>
        /// <param name="source">The pointer to the source unmanaged memory.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public void ReadFrom(Pointer<byte> source) => source.WriteTo(Pointer, Size);

        /// <summary>
        /// Copies bytes from the current memory location to the specified memory location.
        /// </summary>
        /// <param name="destination">The target memory location.</param>
        /// <returns>The actual number of copied bytes.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public long WriteTo(UnmanagedMemoryHandle destination)
        {
            var count = Math.Min(Size, destination.Size);
            Pointer.WriteTo(destination.Pointer, count);
            return count;
        }

        /// <summary>
        /// Computes bitwise equality between two blocks of memory.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public bool BitwiseEquals(UnmanagedMemoryHandle other)
            => !(other is null) && Size == other.Size && Pointer.BitwiseEquals(other.Pointer, Size);

        /// <summary>
        /// Bitwise comparison of the memory blocks.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public int BitwiseCompare(UnmanagedMemoryHandle other)
        {
            if (other is null)
                return 1;
            else if (Size != other.Size)
                return Size.CompareTo(other.Size);
            else
                return Pointer.BitwiseCompare(other.Pointer, Size);
        }

        /// <summary>
        /// Determines whether the given handle points to the same unmanaged memory as this handle.
        /// </summary>
        /// <param name="other">The handle to be compared.</param>
        /// <returns><see langword="true"/>, if the given handle points to the same unmanaged memory as this handle; otherwise, <see langword="false"/>.</returns>
        public bool Equals(UnmanagedMemoryHandle other) => !(other is null) && handle == other.handle;

        /// <summary>
        /// Determines whether the given handle points to the same unmanaged memory as this handle.
        /// </summary>
        /// <param name="other">The handle to be compared.</param>
        /// <returns><see langword="true"/>, if the given handle points to the same unmanaged memory as this handle; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => Equals(other as UnmanagedMemoryHandle);

        /// <summary>
        /// Returns address of this memory in hexadecimal format.
        /// </summary>
        /// <returns>The address of this memory.</returns>
        public override string ToString() => handle.ToString("X");

        /// <summary>
        /// Returns hash code of the memory address.
        /// </summary>
        /// <returns>The hash code of the memory address.</returns>
        public override int GetHashCode() => handle.GetHashCode();

        /// <summary>
        /// Determines whether two handles point to the same unmanaged memory.
        /// </summary>
        /// <param name="first">The first unmanaged memory handle.</param>
        /// <param name="second">The second unmanaged memory handle.</param>
        /// <returns><see langword="true"/>, if both handles point to the same unmanaged memory; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(UnmanagedMemoryHandle first, UnmanagedMemoryHandle second) => !(first is null) && first.Equals(second);

        /// <summary>
        /// Determines whether two handles point to the the different blocks of unmanaged memory.
        /// </summary>
        /// <param name="first">The first unmanaged memory handle.</param>
        /// <param name="second">The second unmanaged memory handle.</param>
        /// <returns><see langword="true"/>, if both handles point to the different blocks of unmanaged memory; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(UnmanagedMemoryHandle first, UnmanagedMemoryHandle second) => first is null || !first.Equals(second);
    }
}