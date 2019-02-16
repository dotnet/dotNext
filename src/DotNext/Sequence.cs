using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNext
{
	/// <summary>
	/// Various methods to work with classes implementing <see cref="IEnumerable{T}"/> interface.
	/// </summary>
    public static class Sequence
    {
		/// <summary>
		/// Apply specified action to each collection element.
		/// </summary>
		/// <typeparam name="T">Type of elements in the collection.</typeparam>
		/// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
		/// <param name="action">An action to applied for each element.</param>
		public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
		{
			foreach (var item in collection)
				action(item);
		}

		/// <summary>
		/// Obtains first value type in the sequence; or <see langword="null"/>
		/// if sequence is empty.
		/// </summary>
		/// <typeparam name="T">Type of elements in the sequence.</typeparam>
		/// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
		/// <returns>First element in the sequence; or <see langword="null"/> if sequence is empty. </returns>
		public static T? FirstOrNull<T>(this IEnumerable<T> seq)
			where T : struct
		{
			using (var enumerator = seq.GetEnumerator())
				return enumerator.MoveNext() ? enumerator.Current : new T?();
		}

		/// <summary>
		/// Obtains first value in the sequence; or <see cref="Optional{T}.Empty"/>
		/// if sequence is empty.
		/// </summary>
		/// <typeparam name="T">Type of elements in the sequence.</typeparam>
		/// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
		/// <returns>First element in the sequence; or <see cref="Optional{T}.Empty"/> if sequence is empty. </returns>
		public static Optional<T> FirstOrEmpty<T>(this IEnumerable<T> seq)
		{
			using(var enumerator = seq.GetEnumerator())
				return enumerator.MoveNext() ? enumerator.Current : Optional<T>.Empty;
		}

		/// <summary>
		/// Bypasses a specified number of elements in a sequence.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="enumerator">Enumerator to modify. Cannot be <see langword="null"/>.</param>
		/// <param name="count">The number of elements to skip.</param>
		/// <returns><see langword="true"/>, if current element is available; otherwise, <see langword="false"/>.</returns>
		public static bool Skip<T>(this IEnumerator<T> enumerator, int count)
		{
			while (count > 0)
				if (enumerator.MoveNext())
					count--;
				else
					return false;
			return true;
		}

		private static bool ElementAt<T>(this IList<T> list, int index, out T element)
		{
			if(index >= 0 && index < list.Count)
			{
				element = list[index];
				return true;
			}
			else
			{
				element = default;
				return false;
			}
		}

		private static bool ElementAt<T>(this IReadOnlyList<T> list, int index, out T element)
		{
			if (index >= 0 && index < list.Count)
			{
				element = list[index];
				return true;
			}
			else
			{
				element = default;
				return false;
			}
		}

		/// <summary>
		/// Obtains elements at the specified index in the sequence.
		/// </summary>
		/// <remarks>
		/// This method is optimized for types <see cref="IList{T}"/>
		/// and <see cref="IReadOnlyList{T}"/>.
		/// </remarks>
		/// <typeparam name="T">Type of elements in the sequence.</typeparam>
		/// <param name="collection">Source collection.</param>
		/// <param name="index">Index of the element to read.</param>
		/// <param name="element">Obtained element.</param>
		/// <returns><see langword="true"/>, if element is available in the collection and obtained successfully; otherwise, <see langword="false"/>.</returns>
		public static bool ElementAt<T>(this IEnumerable<T> collection, int index, out T element)
		{
			if (collection is IList<T> list)
				return ElementAt(list, index, out element);
			else if (collection is IReadOnlyList<T> readOnlyList)
				return ElementAt(readOnlyList, index, out element);
			else
				using (var enumerator = collection.GetEnumerator())
				{
					enumerator.Skip(index);
					if (enumerator.MoveNext())
					{
						element = enumerator.Current;
						return true;
					}
					else
					{
						element = default;
						return false;
					}
				}
		}

		/// <summary>
		/// Skip <see langword="null"/> values in the collection.
		/// </summary>
		/// <typeparam name="T">Type of elements in the collection.</typeparam>
		/// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
		/// <returns>Modified lazy collection without <see langword="null"/> values.</returns>
		public static IEnumerable<T> SkipNulls<T>(this IEnumerable<T> collection)
            where T: class
            => collection.Where(ObjectExtensions.IsNotNull);

		/// <summary>
		/// Concatenates each element from the collection into single string.
		/// </summary>
		/// <typeparam name="T">Type of array elements.</typeparam>
		/// <param name="collection">Collection to convert. Cannot be <see langword="null"/>.</param>
		/// <param name="delimiter">Delimiter between elements in the final string.</param>
		/// <param name="ifEmpty">A string to be returned if collection has no elements.</param>
		/// <returns>Converted collection into string.</returns>
		public static string ToString<T>(this IEnumerable<T> collection, string delimiter, string ifEmpty = "")
            => string.Join(delimiter, collection).IfNullOrEmpty(ifEmpty);
		
        /// <summary>
        /// Constructs a sequence from the single element.
        /// </summary>
        /// <typeparam name="T">Type of element.</typeparam>
        /// <param name="item">An item to be placed into sequence.</param>
        /// <returns>Sequence of single element.</returns>
        public static IEnumerable<T> Singleton<T>(T item)
            => Collections.Generic.List.Singleton(item);
    }
}