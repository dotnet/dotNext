using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    using Runtime.InteropServices;

    /// <summary>
    /// Represents unmanaged memory access that allows
    /// to obtain <see cref="Memory{T}"/> pointing to the
    /// unmanaged memory.
    /// </summary>
    /// <typeparam name="T">The type of elements to store in memory.</typeparam>
    internal sealed class UnmanagedMemoryManager<T> : MemoryManager<T>, IUnmanagedMemoryOwner<T>
        where T : unmanaged
    {
        private IntPtr address;
        internal Action<IUnmanagedMemoryOwner<T>> OnDisposed;

        internal UnmanagedMemoryManager(int length, bool zeroMem)
        {
            var size = UnmanagedMemoryHandle.SizeOf<T>(length);
            address = Marshal.AllocHGlobal(new IntPtr(size));
            GC.AddMemoryPressure(size);
            Length = length;
            if (zeroMem)
                Runtime.InteropServices.Memory.ClearBits(address, size);
        }

        /// <summary>
        /// Gets length of the elements allocated in unmanaged memory.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the size of allocated memory, in bytes.
        /// </summary>
        public long Size => UnmanagedMemoryHandle.SizeOf<T>(Length);

        Pointer<byte> IUnmanagedMemory.Pointer => new Pointer<byte>(address);

        /// <summary>
        /// Gets a span of bytes from the current instance.
        /// </summary>
        public unsafe Span<byte> Bytes => address == default ? default : new Span<byte>(address.ToPointer(), checked((int)Size));

        /// <summary>
        /// Gets a pointer to the allocated unmanaged memory.
        /// </summary>
        public Pointer<T> Pointer => new Pointer<T>(address);

        Span<T> IUnmanagedArray<T>.Span => GetSpan();

        /// <summary>
        /// Represents unmanaged memory as stream.
        /// </summary>
        /// <returns>The stream of unmanaged memory.</returns>
        public unsafe Stream AsStream() => Pointer.AsStream(Size);

        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public void Clear()
        {
            if (address == default)
                throw new ObjectDisposedException(GetType().Name);
            Runtime.InteropServices.Memory.ClearBits(address, Size);
        }

        /// <summary>
        /// Returns a memory span that wraps the underlying unmanaged memory.
        /// </summary>
        /// <returns>A memory span that wraps the underlying unmanaged memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Span<T> GetSpan()
            => Pointer.ToSpan(Length);

        /// <summary>
        /// Returns a handle to the memory that has been pinned and whose address can be taken.
        /// </summary>
        /// <param name="elementIndex">The offset to the element in unmanaged memory.</param>
        /// <returns>A handle to the memory that has been pinned.</returns>
        /// /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        public unsafe override MemoryHandle Pin(int elementIndex = 0)
        {
            if (address == default)
                throw new ObjectDisposedException(GetType().Name);
            return new MemoryHandle(address.ToPointer<T>() + elementIndex);
        }

        /// <summary>
        /// This method does nothing.
        /// </summary>
        public override void Unpin()
        {
            //no need to pin/unpin unmanaged memory
        }

        /// <summary>
        /// Gets enumerator over all elements located in the unmanaged memory.
        /// </summary>
        /// <returns>The enumerator over all elements in the unmanaged memory.</returns>
        public Pointer<T>.Enumerator GetEnumerator() => Pointer.GetEnumerator(Length);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Releases unmanaged memory that was allocated by this object.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release all resources; <see langword="false"/> to release unmanaged memory only.</param>
        protected override void Dispose(bool disposing)
        {
            if (address != default)
            {
                Marshal.FreeHGlobal(address);
                GC.RemoveMemoryPressure(Size);
                OnDisposed?.Invoke(this);
            }
            address = default;
            OnDisposed = null;
        }
    }
}