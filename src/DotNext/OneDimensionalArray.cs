using System;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext
{
    using Runtime.InteropServices;

    /// <summary>
    /// Provides specialized methods to work with one-dimensional array.
    /// </summary>
    public static class OneDimensionalArray
    {
        private struct RemovalCounter<T> : IConsumer<T>
        {
            internal long Count;

            void IConsumer<T>.Invoke(T item) => Count += 1;
        }

        /// <summary>
        /// Indicates that array is <see langword="null"/> or empty.
        /// </summary>
        /// <typeparam name="T">Type of elements in the array.</typeparam>
        /// <param name="array">The array to check.</param>
        /// <returns><see langword="true"/>, if array is <see langword="null"/> or empty.</returns>
        public static bool IsNullOrEmpty<T>(this T[] array)
            => array is null || array.LongLength == 0L;

        /// <summary>
        /// Applies specific action to each array element.
        /// </summary>
        /// <remarks>
        /// This method support modification of array elements
        /// because each array element is passed by reference into action.
        /// </remarks>
        /// <typeparam name="T">Type of array elements.</typeparam>
        /// <param name="array">An array to iterate.</param>
        /// <param name="action">An action to be applied for each element.</param>
        public static void ForEach<T>(this T[] array, in ValueRefAction<T, long> action)
        {
            for (var i = 0L; i < array.LongLength; i++)
                action.Invoke(ref array[i], i);
        }

        /// <summary>
        /// Applies specific action to each array element.
        /// </summary>
        /// <remarks>
        /// This method support modification of array elements
        /// because each array element is passed by reference into action.
        /// </remarks>
        /// <typeparam name="T">Type of array elements.</typeparam>
        /// <param name="array">An array to iterate.</param>
        /// <param name="action">An action to be applied for each element.</param>
        public static void ForEach<T>(this T[] array, RefAction<T, long> action)
            => ForEach(array, new ValueRefAction<T, long>(action, true));

        /// <summary>
        /// Insert a new element into array and return modified array.
        /// </summary>
        /// <typeparam name="T">Type of array elements.</typeparam>
        /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
        /// <param name="element">The zero-based index at which item should be inserted.</param>
        /// <param name="index">The object to insert. The value can be null for reference types.</param>
        /// <returns>A modified array with inserted element.</returns>
        public static T[] Insert<T>(this T[] array, T element, long index)
        {
            if (index < 0 || index > array.LongLength)
                throw new ArgumentOutOfRangeException(nameof(index));
            else if (array.LongLength == 0L)
                return new[] { element };
            else
            {
                var result = new T[array.LongLength + 1];
                Array.Copy(array, 0, result, 0, Math.Min(index + 1, array.LongLength));
                Array.Copy(array, index, result, index + 1, array.LongLength - index);
                result[index] = element;
                return result;
            }
        }

        /// <summary>
        /// Removes the element at the specified in the array and returns modified array.
        /// </summary>
        /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
        /// <param name="index">The zero-based index of the element to remove.</param>
        /// <typeparam name="T">Type of array elements.</typeparam>
        /// <returns>A modified array with removed element.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is incorrect.</exception>
        public static T[] RemoveAt<T>(this T[] array, long index)
        {
            if (index < 0L || index >= array.LongLength)
                throw new ArgumentOutOfRangeException(nameof(index));
            else if (array.LongLength == 1L)
                return Array.Empty<T>();
            else
            {
                var newStore = new T[array.LongLength - 1L];
                Array.Copy(array, 0L, newStore, 0L, index);
                Array.Copy(array, index + 1L, newStore, index, array.LongLength - index - 1L);
                return newStore;
            }
        }

        private static T[] RemoveAll<T, C>(T[] array, in ValueFunc<T, bool> match, ref C callback)
            where C : struct, IConsumer<T>
        {
            if (array.LongLength == 0L)
                return array;
            var newLength = 0L;
            var tempArray = new T[array.LongLength];
            foreach (var item in array)
                if (match.Invoke(item))
                    callback.Invoke(item);
                else
                    tempArray[newLength++] = item;
            if (array.LongLength - newLength == 0L)
                return array;
            else if (newLength == 0L)
                return Array.Empty<T>();
            else
            {
                array = new T[newLength];
                Array.Copy(tempArray, 0L, array, 0L, newLength);
                return array;
            }
        }

        /// <summary>
        /// Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <param name="count">The number of elements removed from this list.</param>
        /// <returns>A modified array with removed elements.</returns>
        public static T[] RemoveAll<T>(this T[] array, in ValueFunc<T, bool> match, out long count)
        {
            var counter = new RemovalCounter<T>();
            var result = RemoveAll(array, match, ref counter);
            count = counter.Count;
            return result;
        }

        /// <summary>
        /// Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <param name="count">The number of elements removed from this list.</param>
        /// <returns>A modified array with removed elements.</returns>
        public static T[] RemoveAll<T>(this T[] array, Predicate<T> match, out long count)
            => RemoveAll(array, match.AsValueFunc(true), out count);

        /// <summary>
        /// Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <param name="callback">The delegate that is used to accept removed items.</param>
        /// <returns>A modified array with removed elements.</returns>
        public static T[] RemoveAll<T>(this T[] array, in ValueFunc<T, bool> match, in ValueAction<T> callback)
            => RemoveAll(array, match, ref AsRef(callback));

        /// <summary>
        /// Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <param name="callback">The delegate that is used to accept removed items.</param>
        /// <returns>A modified array with removed elements.</returns>
        public static T[] RemoveAll<T>(this T[] array, Predicate<T> match, Action<T> callback)
            => RemoveAll(array, match.AsValueFunc(true), new ValueAction<T>(callback, true));

        internal static T[] New<T>(long length) => length == 0L ? Array.Empty<T>() : new T[length];

        /// <summary>
        /// Removes the specified number of elements from the beginning of the array.
        /// </summary>
        /// <typeparam name="T">Type of array elements.</typeparam>
        /// <param name="input">Source array.</param>
        /// <param name="count">A number of elements to be removed.</param>
        /// <returns>Modified array.</returns>
        public static T[] RemoveFirst<T>(this T[] input, long count)
        {
            if (count == 0L)
                return input;
            else if (count >= input.LongLength)
                return Array.Empty<T>();
            else
            {
                var result = new T[input.LongLength - count];
                Array.Copy(input, count, result, 0, result.LongLength);
                return result;
            }
        }

        /// <summary>
        /// Returns sub-array.
        /// </summary>
        /// <param name="input">Input array. Cannot be <see langword="null"/>.</param>
        /// <param name="startIndex">The index at which to begin this slice.</param>
        /// <param name="length">The desired length for the slice.</param>
        /// <typeparam name="T">Type of array elements.</typeparam>
        /// <returns>A new sliced array.</returns>
        public static T[] Slice<T>(this T[] input, long startIndex, long length)
        {
            if (startIndex >= input.LongLength || length == 0)
                return Array.Empty<T>();
            else if (startIndex == 0 && length == input.Length)
                return input;
            length = Math.Min(input.LongLength - startIndex, length);
            var result = new T[length];
            Array.Copy(input, startIndex, result, 0, length);
            return result;
        }

        /// <summary>
        /// Removes the specified number of elements from the end of the array.
        /// </summary>
        /// <typeparam name="T">Type of array elements.</typeparam>
        /// <param name="input">Source array.</param>
        /// <param name="count">A number of elements to be removed.</param>
        /// <returns>Modified array.</returns>
        public static T[] RemoveLast<T>(this T[] input, long count)
        {
            if (count == 0L)
                return input;
            else if (count >= input.LongLength)
                return Array.Empty<T>();
            else
            {
                var result = new T[input.LongLength - count];
                Array.Copy(input, result, result.LongLength);
                return result;
            }
        }

        /// <summary>
        /// Determines whether two arrays contain the same set of bits.
        /// </summary>
        /// <remarks>
        /// This method performs bitwise equality between each pair of elements.
        /// </remarks>
        /// <typeparam name="T">Type of array elements. Should be unmanaged value type.</typeparam>
        /// <param name="first">First array for equality check.</param>
        /// <param name="second">Second array of equality check.</param>
        /// <returns><see langword="true"/>, if both arrays are equal; otherwise, <see langword="false"/>.</returns>
        public static unsafe bool BitwiseEquals<T>(this T[] first, T[] second)
            where T : unmanaged
        {
            if (first is null || second is null)
                return ReferenceEquals(first, second);
            else if (first.LongLength != second.LongLength)
                return false;
            else if (first.LongLength == 0)
                return true;
            else
                fixed (T* firstPtr = first, secondPtr = second)
                    return Memory.EqualsAligned(new IntPtr(firstPtr), new IntPtr(secondPtr), first.LongLength * sizeof(T));
        }

        /// <summary>
        /// Computes bitwise hash code for the array content.
        /// </summary>
        /// <typeparam name="T">The type of array elements.</typeparam>
        /// <param name="array">The array to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>32-bit hash code of the array content.</returns>
        public static unsafe int BitwiseHashCode<T>(this T[] array, bool salted = true)
            where T : unmanaged
        {
            if (array.IsNullOrEmpty())
                return 0;
            fixed (T* ptr = array)
                return Memory.GetHashCode32Aligned(new IntPtr(ptr), array.LongLength * sizeof(T), salted);
        }

        /// <summary>
        /// Computes bitwise hash code for the array content using custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of array elements.</typeparam>
        /// <param name="array">The array to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>32-bit hash code of the array content.</returns>
        public static unsafe int BitwiseHashCode<T>(this T[] array, int hash, in ValueFunc<int, int, int> hashFunction, bool salted = true)
            where T : unmanaged
        {
            if (array.IsNullOrEmpty())
                return hash;
            fixed (T* ptr = array)
                return Memory.GetHashCode32Aligned(new IntPtr(ptr), array.LongLength * sizeof(T), hash, hashFunction, salted);
        }

        /// <summary>
        /// Computes bitwise hash code for the array content using custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of array elements.</typeparam>
        /// <param name="array">The array to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>32-bit hash code of the array content.</returns>
        public static int BitwiseHashCode<T>(this T[] array, int hash, Func<int, int, int> hashFunction, bool salted = true)
            where T : unmanaged
            => BitwiseHashCode(array, hash, new ValueFunc<int, int, int>(hashFunction, true), salted);

        /// <summary>
        /// Computes bitwise hash code for the array content using custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of array elements.</typeparam>
        /// <param name="array">The array to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>64-bit hash code of the array content.</returns>
        public static unsafe long BitwiseHashCode64<T>(this T[] array, long hash, in ValueFunc<long, long, long> hashFunction, bool salted = true)
            where T : unmanaged
        {
            if (array.IsNullOrEmpty())
                return hash;
            fixed (T* ptr = array)
                return Memory.GetHashCode64Aligned(new IntPtr(ptr), array.LongLength * sizeof(T), hash, hashFunction, salted);
        }

        /// <summary>
        /// Computes bitwise hash code for the array content using custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of array elements.</typeparam>
        /// <param name="array">The array to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>64-bit hash code of the array content.</returns>
        public static long BitwiseHashCode64<T>(this T[] array, long hash, Func<long, long, long> hashFunction, bool salted = true)
            where T : unmanaged
            => BitwiseHashCode64(array, hash, new ValueFunc<long, long, long>(hashFunction, true), salted);

        /// <summary>
        /// Computes bitwise hash code for the array content.
        /// </summary>
        /// <typeparam name="T">The type of array elements.</typeparam>
        /// <param name="array">The array to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>64-bit hash code of the array content.</returns>
        public static unsafe long BitwiseHashCode64<T>(this T[] array, bool salted = true)
            where T : unmanaged
        {
            if (array.IsNullOrEmpty())
                return 0;
            fixed (T* ptr = array)
                return Memory.GetHashCode64Aligned(new IntPtr(ptr), array.LongLength * sizeof(T), salted);
        }

        /// <summary>
		/// Determines whether two arrays contain the same set of elements.
		/// </summary>
		/// <remarks>
		/// This method calls <see cref="object.Equals(object, object)"/> for each element type.
		/// </remarks>
		/// <param name="first">The first array to compare.</param>
		/// <param name="second">The second array to compare.</param>
		/// <returns><see langword="true"/>, if both arrays are equal; otherwise, <see langword="false"/>.</returns>
        public static bool SequenceEqual(this object[] first, object[] second)
        {
            if (ReferenceEquals(first, second))
                return true;
            else if (first is null)
                return second is null;
            else if (second is null || first.LongLength != second.LongLength)
                return false;
            for (var i = 0L; i < first.LongLength; i++)
                if (!Equals(first[i], second[i]))
                    return false;
            return true;
        }

        /// <summary>
        /// Compares content of the two arrays.
        /// </summary>
        /// <typeparam name="T">The type of array elements.</typeparam>
        /// <param name="first">The first array to compare.</param>
        /// <param name="second">The second array to compare.</param>
        /// <returns>Comparison result.</returns>
        public static unsafe int BitwiseCompare<T>(this T[] first, T[] second)
            where T : unmanaged
        {
            if (first is null)
                return second is null ? 0 : -1;
            else if (second is null)
                return 1;
            else if (first.LongLength != second.LongLength)
                return first.LongLength.CompareTo(second.LongLength);
            fixed (T* firstPtr = first, secondPtr = second)
                return Memory.CompareUnaligned(new IntPtr(firstPtr), new IntPtr(secondPtr), first.LongLength * sizeof(T));
        }
    }
}
