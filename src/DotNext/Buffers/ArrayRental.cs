using System;
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