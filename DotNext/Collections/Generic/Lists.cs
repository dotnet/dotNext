using System;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
	/// <summary>
	/// Provides various extensions for <see cref="IList{T}"/> interface.
	/// </summary>
	public static class Lists
	{
		/// <summary>
		/// Converts list into array and perform mapping for each
		/// element.
		/// </summary>
		/// <typeparam name="I">Type of elements in the list.</typeparam>
		/// <typeparam name="O">Type of elements in the output array.</typeparam>
		/// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
		/// <param name="mapper">Element mapping function.</param>
		/// <returns>An array representing converted list.</returns>
		public static O[] ToArray<I, O>(this IList<I> input, Func<I, O> mapper)
		{
			var output = Arrays.New<O>(input.Count);
			for (var i = 0; i < input.Count; i++)
				output[i] = mapper(input[i]);
			return output;
		}

		/// <summary>
		/// Converts list into array and perform mapping for each
		/// element.
		/// </summary>
		/// <typeparam name="I">Type of elements in the list.</typeparam>
		/// <typeparam name="O">Type of elements in the output array.</typeparam>
		/// <param name="input">A list to convert. Cannot be <see langword="null"/>.</param>
		/// <param name="mapper">Index-aware element mapping function.</param>
		/// <returns>An array representing converted list.</returns>
		public static O[] ToArray<I, O>(this IList<I> input, Func<int, I, O> mapper)
		{
			var output = Arrays.New<O>(input.Count);
			for (var i = 0; i < input.Count; i++)
				output[i] = mapper(i, input[i]);
			return output;
		}

		public static ReadOnlyListView<T> AsReadOnly<T>(this IList<T> list) => new ReadOnlyListView<T>(list);

		public static ReadOnlyListView<I, O> Convert<I, O>(this IReadOnlyList<I> list, Converter<I, O> converter) => new ReadOnlyListView<I, O>(list, converter);
	}
}
