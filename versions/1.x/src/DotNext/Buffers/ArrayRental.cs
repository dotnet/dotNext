using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents array obtained from array pool.
    /// </summary>
    /// <typeparam name="T">Type of array elements.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ArrayRental<T> : IDisposable
    {
        private readonly ArrayPool<T> pool;
        private readonly T[] array;
        private readonly bool clearArray;

        /// <summary>
        /// Obtains a new array from array pool.
        /// </summary>
        /// <param name="pool">Array pool.</param>
        /// <param name="minimumLength">The minimum length of the array.</param>
        /// <param name="clearArray">Indicates whether the contents of the array should be cleared after calling of <see cref="Dispose()"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pool"/> is <see langword="null"/>.</exception>
        public ArrayRental(ArrayPool<T> pool, int minimumLength, bool clearArray = false)
        {
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
            array = pool.Rent(minimumLength);
            this.clearArray = clearArray;
            Length = minimumLength;
        }

        /// <summary>
        /// Obtains a new array from <see cref="ArrayPool{T}.Shared"/>.
        /// </summary>
        /// <param name="minimumLength">The minimum length of the array.</param>
        /// <param name="clearArray">Indicates whether the contents of the array should be cleared after calling of <see cref="Dispose()"/>.</param>
        public ArrayRental(int minimumLength, bool clearArray = false)
            : this(ArrayPool<T>.Shared, minimumLength, clearArray)
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

        /// <summary>
        /// Gets the span of array elements.
        /// </summary>
        public Span<T> Span => array is null ? default : new Span<T>(array, 0, Length);

        /// <summary>
        /// Gets the rented array.
        /// </summary>
        public ArraySegment<T> Segment => array is null ? default : new ArraySegment<T>(array, 0, Length);

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
        /// Returns a slice of the rented array.
        /// </summary>
        /// <param name="offset">The zero-based index of the first element in the array.</param>
        /// <param name="count">The number of elements in the range.</param>
        /// <returns>The segment of the rented array.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is greater than <see cref="Length"/>.</exception>
        public ArraySegment<T> Slice(int offset, int count)
            => count <= Length ? new ArraySegment<T>(array, offset, count) : throw new ArgumentOutOfRangeException(nameof(count));

        /// <summary>
        /// Obtains rented array.
        /// </summary>
        /// <remarks>
        /// This operation is potentially unsafe because the length of
        /// the returned array may differs from <see cref="Length"/>.
        /// </remarks>
        /// <param name="rental">Array rental.</param>
        public static explicit operator T[](in ArrayRental<T> rental) => rental.array;

        /// <summary>
        /// Gets textual representation of the rented memory.
        /// </summary>
        /// <returns>The textual representation of the rented memory.</returns>
        public override string ToString() => Memory.ToString();

        /// <summary>
        /// Returns the array back to the pool.
        /// </summary>
        public void Dispose() => pool?.Return(array, clearArray);
    }
}