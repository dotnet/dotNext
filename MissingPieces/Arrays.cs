using System;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace MissingPieces
{
	public static class Arrays
	{
        public static O[] Map<I, O>(this I[] input, Func<I, O> mapper)
        {
            var output = New<O>(input.LongLength);
            for(var i = 0L; i < input.LongLength; i++)
                output[i] = mapper(input[i]);
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
	}
}
