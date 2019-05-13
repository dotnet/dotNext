using System;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents handle of allocated unmanaged memory.
    /// </summary>
    public abstract class UnmanagedMemoryHandle : SafeHandle, IUnmanagedMemory, IEquatable<UnmanagedMemoryHandle>
    {
        private protected UnmanagedMemoryHandle(IUnmanagedMemory memory, bool ownsHandle)
            : base(IntPtr.Zero, ownsHandle)
        {
            handle = memory.Address;
        }

        private protected ObjectDisposedException HandleClosed() => new ObjectDisposedException(handle.GetType().Name, ExceptionMessages.HandleClosed);

        /// <summary>
        /// Gets a value indicating whether the unmanaged memory is released.
        /// </summary>
		public sealed override bool IsInvalid => handle == IntPtr.Zero;

        private protected abstract UnmanagedMemoryHandle Clone();

        object ICloneable.Clone() => Clone();

        /// <summary>
        /// Gets size of allocated unmanaged memory, in bytes.
        /// </summary>
        public abstract long Size { get; }

        /// <summary>
        /// Obtains typed pointer to the unmanaged memory referenced by this handle.
        /// </summary>
        /// <typeparam name="T">The type of pointer.</typeparam>
        /// <returns>The pointer to the unmanaged memory.</returns>
        public Pointer<T> ToPointer<T>() where T : unmanaged => IsClosed ? throw HandleClosed() : new Pointer<T>(handle);

        /// <summary>
        /// Gets the address of the unmanaged memory.
        /// </summary>
        public IntPtr Address => handle;

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
        /// <returns>The addres of this memory.</returns>
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
        /// <returns><see langword="true"/>, if both handles point to the differemt blocks of unmanaged memory; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(UnmanagedMemoryHandle first, UnmanagedMemoryHandle second) => first is null || !first.Equals(second);
    }

    /// <summary>
    /// Represents handle of allocated unmanaged memory.
    /// </summary>
    /// <typeparam name="T">Type of pointer.</typeparam>
    public abstract class UnmanagedMemoryHandle<T> : UnmanagedMemoryHandle, IUnmanagedMemory<T>
        where T : unmanaged
    {
        private protected UnmanagedMemoryHandle(IUnmanagedMemory<T> memory, bool ownsHandle)
            : base(memory, ownsHandle)
        {
        }

        /// <summary>
        /// Gets unmanaged typed pointer to the allocated unmanaged memory.
        /// </summary>
        public Pointer<T> Pointer => IsClosed ? throw HandleClosed() : new Pointer<T>(handle);

        /// <summary>
        /// Obtains span object pointing to the allocated unmanaged memory.
        /// </summary>
        public abstract Span<T> Span { get; }
    }
}