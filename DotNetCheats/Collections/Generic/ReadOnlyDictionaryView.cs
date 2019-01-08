using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Cheats.Collections.Generic
{
	public readonly struct ReadOnlyDictionaryView<K, V>: IReadOnlyDictionary<K, V>, IEquatable<ReadOnlyDictionaryView<K, V>>
	{
		private readonly IDictionary<K, V> source;

		public ReadOnlyDictionaryView(IDictionary<K, V> dictionary)
			=> source = dictionary ?? throw new ArgumentNullException(nameof(dictionary));

		public V this[K key] => source[key];

		public IEnumerable<K> Keys => source.Keys;

		public IEnumerable<V> Values => source.Values;

		public int Count => source.Count;

		public bool ContainsKey(K key) => source.ContainsKey(key);

		public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
			=> source.GetEnumerator();

		public bool TryGetValue(K key, out V value)
			=> source.TryGetValue(key, out value);

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		public bool Equals(ReadOnlyDictionaryView<K, V> other)
			=> ReferenceEquals(source, other.source);

		public override int GetHashCode() => RuntimeHelpers.GetHashCode(source);

		public override bool Equals(object other)
			=> other is ReadOnlyDictionaryView<K, V> view ? Equals(view) : Equals(source, other);

		public static bool operator ==(ReadOnlyDictionaryView<K, V> first, ReadOnlyDictionaryView<K, V> second)
			=> first.Equals(second);

		public static bool operator !=(ReadOnlyDictionaryView<K, V> first, ReadOnlyDictionaryView<K, V> second)
			=> !first.Equals(second);
	}

	public readonly struct ReadOnlyDictionaryView<K, V, T> : IReadOnlyDictionary<K, T>, IEquatable<ReadOnlyDictionaryView<K, V, T>>
	{
		private readonly IReadOnlyDictionary<K, V> source;
		private readonly Converter<V, T> mapper;

		public ReadOnlyDictionaryView(IReadOnlyDictionary<K, V> dictionary, Converter<V, T> mapper)
		{
			source = dictionary is null ? throw new ArgumentNullException(nameof(dictionary)) : dictionary;
			this.mapper = mapper is null ? throw new ArgumentNullException(nameof(mapper)) : mapper;
		}

		public T this[K key] => mapper(source[key]);

		public IEnumerable<K> Keys => source.Keys;

		public IEnumerable<T> Values => source.Values.Select(mapper.ConvertDelegate<Func<V, T>>());

		public int Count => source.Count;

		public bool ContainsKey(K key) => source.ContainsKey(key);

		public IEnumerator<KeyValuePair<K, T>> GetEnumerator()
		{
			var mapper = this.mapper;
			return source
				.Select(entry => new KeyValuePair<K, T>(entry.Key, mapper(entry.Value)))
				.GetEnumerator();
		}

		public bool TryGetValue(K key, out T value)
		{
			if (source.TryGetValue(key, out var sourceVal))
			{
				value = mapper(sourceVal);
				return true;
			}
			else
			{
				value = default;
				return false;
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		public bool Equals(ReadOnlyDictionaryView<K, V, T> other)
			=> ReferenceEquals(source, other.source) && Equals(mapper, other.mapper);

		public override int GetHashCode()
			=> source is null || mapper is null ? 0 : RuntimeHelpers.GetHashCode(source) ^ mapper.GetHashCode();

		public override bool Equals(object other)
			=> other is ReadOnlyDictionaryView<K, V, T> view ? Equals(view) : Equals(source, other);
	}
}
