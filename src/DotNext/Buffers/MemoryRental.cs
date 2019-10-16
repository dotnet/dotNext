using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents the memory obtained from the pool or allocated
    /// on the stack or heap.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the rented memory.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct MemoryRental<T>
    {
        /// <summary>
        /// The rented memory.
        /// </summary>
        [SuppressMessage("Design", "CA1051", Justification = "To avoid generation of temporary variables by the users of this type")]
        public readonly Span<T> Span;

        private readonly IMemoryOwner<T> owner;

        /// <summary>
        /// Rents the memory referenced by the span.
        /// </summary>
        /// <param name="span">The span that references the memory to rent.</param>
        public MemoryRental(Span<T> span)
        {
            Span = span;
            owner = null;
        }

        /// <summary>
        /// Rents the memory from the pool.
        /// </summary>
        /// <param name="pool">The memory pool.</param>
        /// <param name="minBufferSize">The minimum size of the memory to rent.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pool"/> is <see langword="null"/>.</exception>
        public MemoryRental(MemoryPool<T> pool, int minBufferSize)
        {
            if (pool is null)
                throw new ArgumentNullException(nameof(pool));
            owner = pool.Rent(minBufferSize);
            Span = owner.Memory.Span.Slice(0, minBufferSize);
        }

        /// <summary>
        /// Rents the memory from <see cref="MemoryPool{T}.Shared"/>.
        /// </summary>
        /// <param name="minBufferSize">The minimum size of the memory to rent.</param>
        public MemoryRental(int minBufferSize)
            : this(MemoryPool<T>.Shared, minBufferSize)
        {
        }

        /// <summary>
        /// Gets a value indicating that this object
        /// doesn't reference rented memory.
        /// </summary>
        public bool IsEmpty => Span.IsEmpty;

        /// <summary>
        /// Converts the reference to the already allocated memory
        /// into the rental object.
        /// </summary>
        /// <param name="span">The allocated memory to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator MemoryRental<T>(Span<T> span)
            => new MemoryRental<T>(span);

        /// <summary>
        /// Gets length of the rented memory.
        /// </summary>
        public int Length => Span.Length;

        /// <summary>
        /// Gets the memory element by its index.
        /// </summary>
        /// <param name="index">The index of the memory element.</param>
        /// <returns>The managed pointer to the memory element.</returns>
        public ref T this[int index] => ref Span[index];

        /// <summary>
        /// Obtains managed pointer to the first element of the rented array.
        /// </summary>
        /// <returns>The managed pointer to the first element of the rented array.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetPinnableReference() => ref Span.GetPinnableReference();

        /// <summary>
        /// Releases the rented memory.
        /// </summary>
        public void Dispose() => owner?.Dispose();
    }
}
