using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents array obtained from array pool.
    /// </summary>
    /// <typeparam name="T">Type of array elements.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ArrayRental<T> : IMemoryOwner<T>, IConvertible<Memory<T>>, IConvertible<ArraySegment<T>>, IConvertible<MemoryOwner<T>>
    {
        private readonly ArrayPool<T>? pool;
        private readonly T[] array;
        private readonly bool clearArray;   // TODO: Remove this field in the next major version

        /// <summary>
        /// Obtains a new array from array pool.
        /// </summary>
        /// <param name="pool">Array pool.</param>
        /// <param name="minimumLength">The minimum length of the array.</param>
        /// <param name="clearArray">Indicates whether the contents of the array should be cleared after calling of <see cref="Dispose()"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pool"/> is <see langword="null"/>.</exception>
        public ArrayRental(ArrayPool<T> pool, int minimumLength, bool clearArray)
        {
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
            array = pool.Rent(minimumLength);
            this.clearArray = clearArray;
            Length = minimumLength;
        }

        /// <summary>
        /// Obtains a new array from array pool.
        /// </summary>
        /// <param name="pool">Array pool.</param>
        /// <param name="minimumLength">The minimum length of the array.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pool"/> is <see langword="null"/>.</exception>
        public ArrayRental(ArrayPool<T> pool, int minimumLength)
            : this(pool, minimumLength, RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
        }

        /// <summary>
        /// Obtains a new array from <see cref="ArrayPool{T}.Shared"/>.
        /// </summary>
        /// <param name="minimumLength">The minimum length of the array.</param>
        /// <param name="clearArray">Indicates whether the contents of the array should be cleared after calling of <see cref="Dispose()"/>.</param>
        public ArrayRental(int minimumLength, bool clearArray)
            : this(ArrayPool<T>.Shared, minimumLength, clearArray)
        {
        }

        /// <summary>
        /// Obtains a new array from <see cref="ArrayPool{T}.Shared"/>.
        /// </summary>
        /// <param name="minimumLength">The minimum length of the array.</param>
        public ArrayRental(int minimumLength)
            : this(minimumLength, RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
        }

        /// <summary>
        /// Rents the array.
        /// </summary>
        /// <param name="array">The array to rent.</param>
        /// <param name="length">The length of the rented segment.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is greater than the length of <paramref name="array"/>.</exception>
        public ArrayRental(T[] array, int length)
        {
            this.array = array ?? throw new ArgumentNullException(nameof(array));
            Length = length <= array.Length ? length : throw new ArgumentOutOfRangeException(nameof(length));
            clearArray = false;
            pool = null;
        }

        /// <summary>
        /// Rents the array.
        /// </summary>
        /// <param name="array">The array to rent.</param>
        public ArrayRental(T[] array)
            : this(array, array.Length)
        {
        }

        /// <summary>
        /// Gets length of the rented array.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets value indicating that this object is empty.
        /// </summary>
        public bool IsEmpty => array is null || Length == 0;

        /// <summary>
        /// Gets the memory associated with the rented array.
        /// </summary>
        public Memory<T> Memory => array is null ? default : new Memory<T>(array, 0, Length);

        /// <inheritdoc/>
        Memory<T> IConvertible<Memory<T>>.Convert() => Memory;

        /// <summary>
        /// Gets the span of array elements.
        /// </summary>
        public Span<T> Span => array is null ? default : new Span<T>(array, 0, Length);

        /// <summary>
        /// Gets the rented array.
        /// </summary>
        public ArraySegment<T> Segment => array is null ? ArraySegment<T>.Empty : new ArraySegment<T>(array, 0, Length);

        /// <inheritdoc/>
        ArraySegment<T> IConvertible<ArraySegment<T>>.Convert() => Segment;

        /// <summary>
        /// Converts this instance to <see cref="MemoryOwner{T}"/>.
        /// </summary>
        public MemoryOwner<T> Owner => array is null ? default : new MemoryOwner<T>(pool, array, Length);

        /// <inheritdoc/>
        MemoryOwner<T> IConvertible<MemoryOwner<T>>.Convert() => Owner;

        /// <summary>
        /// Gets the array element by its index.
        /// </summary>
        /// <param name="index">The index of the array element.</param>
        /// <returns>The managed pointer to the array element.</returns>
        public ref T this[long index] => ref array[index];

        /// <summary>
        /// Obtains managed pointer to the first element of the rented array.
        /// </summary>
        /// <returns>The managed pointer to the first element of the rented array.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref T GetPinnableReference() => ref array[0];

        /// <summary>
        /// Sets all elements of the rented array to default value of type <typeparamref name="T"/>.
        /// </summary>
        public void Clear()
        {
            if (array != null)
                Array.Clear(array, 0, Length);
        }

        /// <summary>
        /// Gets textual representation of the rented memory.
        /// </summary>
        /// <returns>The textual representation of the rented memory.</returns>
        public override string ToString() => Memory.ToString();

        /// <summary>
        /// Converts rented array to the memory owner.
        /// </summary>
        /// <param name="array">The rented array.</param>
        /// <returns>The array owner.</returns>
        public static implicit operator MemoryOwner<T>(in ArrayRental<T> array) => array.Owner;

        /// <summary>
        /// Returns the array back to the pool.
        /// </summary>
        public void Dispose() => pool?.Return(array, clearArray);
    }
}