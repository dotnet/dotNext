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
    /// <remarks>
    /// This type is aimed to be compatible with memory allocated using <c>stackalloc</c> operator.
    /// If stack allocation threshold is reached (e.g. <see cref="StackallocThreshold"/>) then it's possible to use pooled memory from
    /// arbitrary <see cref="MemoryPool{T}"/> or <see cref="ArrayPool{T}.Shared"/>. Arbitrary
    /// <see cref="ArrayPool{T}"/> is not supported because default <see cref="ArrayPool{T}.Shared"/>
    /// is optimized for per-CPU core allocation which is perfectly for situation when the same
    /// thread is responsible for renting and releasing array. Otherwise, it's recommended to
    /// use <see cref="ArrayRental{T}"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// const int stackallocThreshold = 20;
    /// var memory = size &lt;=stackallocThreshold ? new MemoryRental&lt;byte&gt;(stackalloc byte[stackallocThreshold], size) : new MemoryRental&lt;byte&gt;(size);
    /// </code>
    /// </example>
    /// <typeparam name="T">The type of the elements in the rented memory.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct MemoryRental<T>
    {
        private const int StackallocThresholdInBytes = 512;

        /// <summary>
        /// Global recommended number of elements that can be allocated on the stack.
        /// </summary>
        /// <remarks>
        /// This property is for internal purposes only and should not be referenced
        /// directly in your code.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [CLSCompliant(false)]
        public static int StackallocThreshold
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.SizeOf<T>() > StackallocThresholdInBytes ? 1 : StackallocThresholdInBytes / Unsafe.SizeOf<T>();
        }

        private readonly object? owner;
        private readonly Span<T> memory;

        /// <summary>
        /// Rents the memory referenced by the span.
        /// </summary>
        /// <param name="span">The span that references the memory to rent.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryRental(Span<T> span)
        {
            memory = span;
            owner = null;
        }

        /// <summary>
        /// Rents the memory referenced by the span.
        /// </summary>
        /// <param name="span">The span that references the memory to rent.</param>
        /// <param name="length">The actual length of the data.</param>
        public MemoryRental(Span<T> span, int length)
            : this(span.Slice(0, length))
        {
        }

        /// <summary>
        /// Rents the memory from the pool.
        /// </summary>
        /// <param name="pool">The memory pool.</param>
        /// <param name="minBufferSize">The minimum size of the memory to rent.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pool"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="minBufferSize"/> is less than or equal to zero.</exception>
        public MemoryRental(MemoryPool<T> pool, int minBufferSize)
        {
            if (pool is null)
                throw new ArgumentNullException(nameof(pool));
            if (minBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(minBufferSize));
            var owner = pool.Rent(minBufferSize);
            memory = owner.Memory.Span.Slice(0, minBufferSize);
            this.owner = owner;
        }

        /// <summary>
        /// Rents the memory from the pool.
        /// </summary>
        /// <param name="pool">The memory pool.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pool"/> is <see langword="null"/>.</exception>
        public MemoryRental(MemoryPool<T> pool)
        {
            if (pool is null)
                throw new ArgumentNullException(nameof(pool));
            var owner = pool.Rent();
            memory = owner.Memory.Span;
            this.owner = owner;
        }

        /// <summary>
        /// Rents the memory from <see cref="ArrayPool{T}.Shared"/>.
        /// </summary>
        /// <param name="minBufferSize">The minimum size of the memory to rent.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="minBufferSize"/> is less than or equal to zero.</exception>
        public MemoryRental(int minBufferSize)
        {
            if (minBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(minBufferSize));

            var owner = ArrayPool<T>.Shared.Rent(minBufferSize);
            memory = owner.AsSpan(0, minBufferSize);
            this.owner = owner;
        }

        /// <summary>
        /// Gets the rented memory.
        /// </summary>
        public Span<T> Span => memory;

        /// <summary>
        /// Gets a value indicating that this object
        /// doesn't reference rented memory.
        /// </summary>
        public bool IsEmpty => memory.IsEmpty;

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
        public int Length => memory.Length;

        /// <summary>
        /// Gets the memory element by its index.
        /// </summary>
        /// <param name="index">The index of the memory element.</param>
        /// <returns>The managed pointer to the memory element.</returns>
        public ref T this[int index] => ref memory[index];

        /// <summary>
        /// Obtains managed pointer to the first element of the rented array.
        /// </summary>
        /// <returns>The managed pointer to the first element of the rented array.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetPinnableReference() => ref memory.GetPinnableReference();

        /// <summary>
        /// Gets textual representation of the rented memory.
        /// </summary>
        /// <returns>The textual representation of the rented memory.</returns>
        public override string ToString() => memory.ToString();

        /// <summary>
        /// Returns the memory back to the pool.
        /// </summary>
        public void Dispose()
        {
            switch (owner)
            {
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
                case T[] array:
                    ArrayPool<T>.Shared.Return(array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    break;
            }
        }
    }
}
