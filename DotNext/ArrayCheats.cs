using System;

namespace Cheats
{
	using Runtime.InteropServices;

	/// <summary>
	/// Various extensions for arrays.
	/// </summary>
	public static class ArrayCheats
	{
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
			if (index < 0 || index > array.Length)
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
		/// Allocates a new array, or return cached empty array.
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
					return Memory.Equals(firstPtr, secondPtr, first.Length * ValueType<T>.Size);
		}
	}
}
