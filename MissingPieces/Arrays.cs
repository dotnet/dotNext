using System;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace MissingPieces
{
	public static class Arrays
	{
		public static T[] Insert<T>(this T[] array, T element, long index)
		{
			if(index < 0 || index > array.Length)
				throw new IndexOutOfRangeException($"Invalid index {index}");
			else if(array.LongLength == 0L)
				return new[]{ element };
			else 
			{
				var result = new T[array.LongLength + 1];
				Array.Copy(array, 0, result, 0, Math.Min(index + 1, array.LongLength));
				Array.Copy(array, index, result, index + 1, array.LongLength - index);
				result[index] = element;
				return result;
			}
		}

        public static O[] Map<I, O>(this I[] input, Func<I, O> mapper)
        {
            var output = New<O>(input.LongLength);
            for(var i = 0L; i < input.LongLength; i++)
                output[i] = mapper(input[i]);
            return output;
        }
		public static O[] Map<I, O>(this I[] input, Func<long, I, O> mapper)
		{
			var output = New<O>(input.LongLength);
            for(var i = 0L; i < input.LongLength; i++)
                output[i] = mapper(i, input[i]);
            return output;
		}

		public static T[] New<T>(long length)
            => length == 0L ? Array.Empty<T>() : new T[length];

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

		public static bool SequenceEqual<T>(this T[] first, T[] second)
			where T : IEquatable<T>
			=> first is null ? second is null : new ReadOnlySpan<T>(first).SequenceEqual(new ReadOnlySpan<T>(second));

		public static unsafe bool BitwiseEquals<T>(this T[] first, T[] second)
			where T: struct
		{
			if(first is null)
				return second is null;
			else if(first.LongLength == 0L)
				return second.LongLength == 0L;
			else if(first.LongLength != second.LongLength)
				return false;
			var size = Unsafe.SizeOf<T>() * first.Length;
			return new ReadOnlySpan<byte>(Unsafe.AsPointer(ref first[0]), size).SequenceEqual(new ReadOnlySpan<byte>(Unsafe.AsPointer(ref second[0]), size));
		}

		public static bool IsNullOrEmpty(this Array array)
		{
			if(array is null)
				return true;
			for(int dimension = 0; dimension < array.Rank; dimension++)
				if(array.GetLongLength(dimension) > 0L)
					return false;
			return true;
		}

		private static bool CheckIndex<T>(this T[] array, long index) =>  index >= 0 || index < array.LongLength;

		/// <summary>
		/// Gets element at the specified index
		/// without throwing <see cref="IndexOutOfRangeException"/>.
		/// </summary>
		/// <param name="array">An array to read.</param>
		/// <param name="index">Index of the element.</param>
		/// <typeparam name="T">Type of array elements.</typeparam>
		/// <returns>Array element.</returns>
		public static Optional<T> At<T>(this T[] array, long index)
			=> array.CheckIndex(index) ? array[index] : Optional<T>.Empty;

		public static bool At<T>(this T[] array, long index, out T value)
			=> array.At(index).TryGet(out value);
		
		public static long Take<T>(this T[] array, out T first, out T second, long startIndex = 0)
		{
			if(array.At(startIndex, out first))
				startIndex += 1;
			else
				{
					second = default;
					return 0;
				}
			return array.At(startIndex, out second) ? 2L : 1L;
		}

		public static long Take<T>(this T[] array, out T first, out T second, out T third, long startIndex = 0)
		{
			if(array.At(startIndex, out first))
				startIndex += 1;
			else
				{
					second = third = default;
					return 0L;
				}
			
			if(array.At(startIndex, out second))
				startIndex += 1;
			else
				{
					third = default;
					return 1L;
				}
			return array.At(startIndex, out third) ? 3L : 2L;
		}

		public static long Take<T>(this T[] array, out T first, out T second, out T third, out T fourth, long startIndex = 0)
		{
			if(array.At(startIndex, out first))
				startIndex += 1;
			else
				{
					second = third = fourth = default;
					return 0L;
				}
			
			if(array.At(startIndex, out second))
				startIndex += 1;
			else
				{
					fourth = third = default;
					return 1L;
				}
			
			if(array.At(startIndex, out third))
				startIndex += 1;
			else
				{
					fourth = default;
					return 2L;
				}
			return array.At(startIndex, out fourth) ? 4L : 3L;
		}
	}
}
