using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents pool of unmanaged memory.
    /// </summary>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    public sealed class UnmanagedMemoryPool<T> : MemoryPool<T>
        where T : unmanaged
    {
        private readonly Action<IUnmanagedMemoryOwner<T>>? removeMemory;
        private readonly int defaultBufferSize;
        private volatile Action? ownerDisposal;

        /// <summary>
        /// Initializes a new pool of unmanaged memory.
        /// </summary>
        /// <param name="maxBufferSize">The maximum allowed number of elements that can be allocated by the pool.</param>
        /// <param name="defaultBufferSize">The default number of elements that can be allocated by the pool.</param>
        /// <param name="trackAllocations"><see langword="true"/> to release allocated unmanaged memory when <see cref="Dispose(bool)"/> is called; otherwise, <see langword="false"/>.</param>
        public UnmanagedMemoryPool(int maxBufferSize, int defaultBufferSize = 32, bool trackAllocations = false)
        {
            if (maxBufferSize < 1)
                throw new ArgumentOutOfRangeException(nameof(maxBufferSize));
            MaxBufferSize = maxBufferSize;
            this.defaultBufferSize = Math.Min(defaultBufferSize, maxBufferSize);
            removeMemory = trackAllocations ? new Action<IUnmanagedMemoryOwner<T>>(RemoveTracking) : null;
        }

        /// <summary>
        /// Gets allocator of unmanaged memory.
        /// </summary>
        /// <param name="zeroMem"><see langword="true"/> to set all bits in the memory to zero; otherwise, <see langword="false"/>.</param>
        /// <returns>The unmanaged memory allocator.</returns>
        public static MemoryAllocator<T> GetAllocator(bool zeroMem)
            => new Func<int, IUnmanagedMemoryOwner<T>>(length => Allocate(length, zeroMem)).ToAllocator();

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void RemoveTracking(IUnmanagedMemoryOwner<T> owner)
            => ownerDisposal -= owner.Dispose;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void AddTracking(IUnmanagedMemoryOwner<T> owner)
            => ownerDisposal += owner.Dispose;

        /// <summary>
        /// Gets the maximum elements that can be allocated by this pool.
        /// </summary>
        public override int MaxBufferSize { get; }

        /// <summary>
        /// Returns unmanaged memory block capable of holding at least <paramref name="length"/> elements of <typeparamref name="T"/>.
        /// </summary>
        /// <param name="length">The length of the continuous block of memory.</param>
        /// <returns>The allocated block of unmanaged memory.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is greater than <see cref="MaxBufferSize"/>.</exception>
        public override IMemoryOwner<T> Rent(int length = -1)
        {
            if (length > MaxBufferSize)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length < 0)
                length = defaultBufferSize;
            var result = new UnmanagedMemoryOwner<T>(length, true, true) { OnDisposed = removeMemory };
            if (removeMemory is not null)
                AddTracking(result);
            return result;
        }

        /// <summary>
        /// Allocates unmanaged memory and returns an object
        /// that controls its lifetime.
        /// </summary>
        /// <param name="length">The number of elements to be allocated in unmanaged memory.</param>
        /// <param name="zeroMem"><see langword="true"/> to set all bits in the memory to zero; otherwise, <see langword="false"/>.</param>
        /// <returns>The object representing allocated unmanaged memory.</returns>
        public static IUnmanagedMemoryOwner<T> Allocate(int length, bool zeroMem = true)
            => new UnmanagedMemoryOwner<T>(length, zeroMem, false);

        /// <summary>
        /// Frees the unmanaged resources used by the memory pool and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Exchange(ref ownerDisposal, null)?.Invoke();
            }
        }
    }
}
