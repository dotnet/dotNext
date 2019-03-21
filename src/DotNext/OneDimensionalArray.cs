using System;

namespace DotNext
{
	using Runtime.InteropServices;

	/// <summary>
	/// Provides specialized methods to work with one-dimensional array.
	/// </summary>
	public static class OneDimensionalArray
	{
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
		public static void ForEach<T>(this T[] array, ItemAction<long, T> action)
		{
			for (var i = 0L; i < array.LongLength; i++)
				action(i, ref array[i]);
		}

		/// <summary>
		/// Insert a new element into array and return modified array.
		/// </summary>
		/// <typeparam name="T">Type of array element.</typeparam>
		/// <param name="array">Source array. Cannot be <see langword="null"/>.</param>
		/// <param name="element">The zero-based index at which item should be inserted.</param>
		/// <param name="index">The object to insert. The value can be null for reference types.</param>
		/// <returns>A modified array with inserted element.</returns>
		public static T[] Insert<T>(this T[] array, T element, long index)
		{
			if (index < 0 || index > array.LongLength)
				throw new IndexOutOfRangeException(nameof(index));
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
		/// Converts each array element from one type into another.
		/// </summary>
		/// <typeparam name="I">Type of source array elements.</typeparam>
		/// <typeparam name="O">Type of target array elements.</typeparam>
		/// <param name="input">Input array to be converted. Cannot be <see langword="null"/>.</param>
		/// <param name="mapper">Mapping function. Cannot be <see langword="null"/>.</param>
		/// <returns>Converted array.</returns>
        public static O[] Convert<I, O>(this I[] input, Converter<I, O> mapper) => Array.ConvertAll(input, mapper);

		/// <summary>
		/// Converts each array element from one type into another.
		/// </summary>
		/// <typeparam name="I">Type of source array elements.</typeparam>
		/// <typeparam name="O">Type of target array elements.</typeparam>
		/// <param name="input">Input array to be converted. Cannot be <see langword="null"/>.</param>
		/// <param name="mapper">Index-aware mapping function. Cannot be <see langword="null"/>.</param>
		/// <returns>Converted array.</returns>
		public static O[] Convert<I, O>(this I[] input, Func<long, I, O> mapper)
		{
			var output = New<O>(input.LongLength);
            for(var i = 0L; i < input.LongLength; i++)
                output[i] = mapper(i, input[i]);
            return output;
		}

		/// <summary>
		/// Allocates a new one-dimensional array, or return cached empty array.
		/// </summary>
		/// <typeparam name="T">Type of array elements.</typeparam>
		/// <param name="length">Length of the array.</param>
		/// <returns>Allocated array.</returns>
		public static T[] New<T>(long length)
            => length == 0L ? Array.Empty<T>() : new T[length];

		/// <summary>
		/// Removes the specified number of elements from the beginning of the array.
		/// </summary>
		/// <typeparam name="T">Type of array elements.</typeparam>
		/// <param name="input">Source array.</param>
		/// <param name="count">A number of elements to be removed.</param>
		/// <returns>Modified array.</returns>
		public static T[] RemoveFirst<T>(this T[] input, long count)
		{
			if(count == 0L)
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
			if(startIndex >= input.LongLength || length == 0)
				return Array.Empty<T>();
			else if(startIndex == 0 && length == input.Length)
				return input;
			length = Math.Min(length - startIndex, input.LongLength);
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
			if(count == 0L)
				return input;
			else if (count >= input.LongLength)
				return Array.Empty<T>();
			else
			{
				var result = new T[input.LongLength - count];
				Array.Copy(input, 0, result, 0, result.LongLength);
				return result;
			}
		}

		/// <summary>
		/// Determines whether two arrays contain the same set of elements.
		/// </summary>
		/// <remarks>
		/// This method calls <see cref="IEquatable{T}.Equals(T)"/> for each element type.
		/// </remarks>
		/// <typeparam name="T">Type of array elements.</typeparam>
		/// <param name="first">First array for equality check.</param>
		/// <param name="second">Second array for equality check.</param>
		/// <returns><see langword="true"/>, if both arrays are equal; otherwise, <see langword="false"/>.</returns>
		public static bool SequenceEqual<T>(this T[] first, T[] second)
			where T : IEquatable<T>
			=> first is null ? second is null : new ReadOnlySpan<T>(first).SequenceEqual(new ReadOnlySpan<T>(second));

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
			where T: unmanaged
		{
			if(first is null)
				return second is null;
			else if(first.LongLength != second.LongLength)
				return false;
			else if(first.LongLength == 0)
				return true;
			else
				fixed(T* firstPtr = first, secondPtr = second)
					return Memory.Equals(firstPtr, secondPtr, first.LongLength * ValueType<T>.Size);
		}

        /// <summary>
        /// Computes bitwise hash code for the array content.
        /// </summary>
        /// <typeparam name="T">The type of array elements.</typeparam>
        /// <param name="array">The array to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>The hash code of the array content.</returns>
        public static unsafe int BitwiseHashCode<T>(this T[] array, bool salted = true)
            where T : unmanaged
        {
            if (array.IsNullOrEmpty())
                return 0;
            fixed (T* ptr = array)
                return Memory.GetHashCode(ptr, array.LongLength * ValueType<T>.Size, salted);
        }

        /// <summary>
        /// Computes bitwise hash code for the array content
        /// and custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of array elements.</typeparam>
        /// <param name="array">The array to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>The hash code of the array content.</returns>
        public static unsafe int BitwiseHashCode<T>(this T[] array, int hash, Func<int, int, int> hashFunction, bool salted = true)
            where T : unmanaged
        {
            if (array.IsNullOrEmpty())
                return hash;
            fixed (T* ptr = array)
                return Memory.GetHashCode(ptr, array.LongLength * ValueType<T>.Size, hash, hashFunction, salted);
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
            if (first.LongLength != second.LongLength)
                return first.LongLength.CompareTo(second.LongLength);
            fixed (T* firstPtr = first, secondPtr = second)
                return Memory.Compare(firstPtr, secondPtr, first.LongLength * ValueType<T>.Size);
        }
	}
}
