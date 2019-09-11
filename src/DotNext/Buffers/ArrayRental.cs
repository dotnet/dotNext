using System;
using System.ComponentModel;
using System.Buffers;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents array obtained from array pool.
    /// </summary>
    /// <typeparam name="T">Type of array elements.</typeparam>
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
        public ArrayRental(ArrayPool<T> pool, int minimumLength, bool clearArray = false)
        {
            this.pool = pool;
            array = pool.Rent(minimumLength);
            this.clearArray = clearArray;
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
        /// Gets value indicating that this object is empty.
        /// </summary>
        public bool IsEmpty => array is null;

        /// <summary>
        /// Gets memory associated with the rented array.
        /// </summary>
        public Memory<T> Memory => array is null ? default : new Memory<T>(array);

        /// <summary>
        /// Gets array element by its index.
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
        /// Obtains rented array.
        /// </summary>
        /// <param name="rental">Array rental.</param>
        public static implicit operator T[](in ArrayRental<T> rental) => rental.array;

        /// <summary>
        /// Returns array to the pool.
        /// </summary>
        public void Dispose() => pool?.Return(array, clearArray);
    }
}