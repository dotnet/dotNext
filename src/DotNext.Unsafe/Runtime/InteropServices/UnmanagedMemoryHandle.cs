using System;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices
{
    public abstract class UnmanagedMemoryHandle: SafeHandle, IUnmanagedMemory, IEquatable<UnmanagedMemoryHandle>
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

        public override bool Equals(object other) => Equals(other as UnmanagedMemoryHandle);

        public override string ToString() => handle.ToString("X");

        public override int GetHashCode() => handle.GetHashCode();

        public static bool operator ==(UnmanagedMemoryHandle first, UnmanagedMemoryHandle second) => !(first is null) && first.Equals(second);

        public static bool operator !=(UnmanagedMemoryHandle first, UnmanagedMemoryHandle second) => first is null ? second is null : first.Equals(second);
    }

    /// <summary>
    /// Represents handle to unmanaged memory.
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