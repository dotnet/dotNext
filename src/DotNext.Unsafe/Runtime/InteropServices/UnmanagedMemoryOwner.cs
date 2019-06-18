using System;
using System.Runtime.CompilerServices;
using System.IO;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents allocated unmanaged memory.
    /// </summary>
    /// <remarks>
    /// All elements are allocated in unmanaged memory not controlled by Garbage Collector.
    /// However, the unmanaged memory will be released automatically if GC collects
    /// the instance of this type.
    /// </remarks>
    /// <typeparam name="T">The type of elements in the unmanaged memory.</typeparam>
    public sealed class UnmanagedMemoryOwner<T> : SafeHandle, ICloneable
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
        public byte[] ToByteArray<M>() => IsInvalid ? Array.Empty<byte>() : Pointer.As<byte>().ToByteArray(Size);

        /// <summary>
        /// Represents unmanaged memory as stream.
        /// </summary>
        /// <returns>The stream of unmanaged memory.</returns>
        public Stream AsStream() => IsInvalid ? Stream.Null : Pointer.As<byte>().AsStream(Size);

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
            var oldSize = Size;
            this.length = length;
            handle = Marshal.ReAllocHGlobal(handle, new IntPtr(Size));
            GC.RemoveMemoryPressure(oldSize);
            GC.AddMemoryPressure(Size);
        }

        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        public void Clear()
        {
            if(handle == IntPtr.Zero)
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
        /// Gets a pointer to the allocated unmanaged memory.
        /// </summary>
        public Pointer<T> Pointer => IsInvalid ? default : new Pointer<T>(handle);

        public static implicit operator Span<T>(UnmanagedMemoryOwner<T> owner) => owner is null ? default : owner.Span;

        public static implicit operator Pointer<T>(UnmanagedMemoryOwner<T> owner) => owner is null ? default : owner.Pointer;
    }
}