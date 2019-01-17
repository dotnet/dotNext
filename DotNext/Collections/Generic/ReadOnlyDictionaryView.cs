using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DotNext.Collections.Generic
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

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public bool Equals(ReadOnlyDictionaryView<K, V> other) => ReferenceEquals(source, other.source);

		public override int GetHashCode() => RuntimeHelpers.GetHashCode(source);

		public override bool Equals(object other)
			=> other is ReadOnlyDictionaryView<K, V> view ? Equals(view) : Equals(source, other);

		public static bool operator ==(ReadOnlyDictionaryView<K, V> first, ReadOnlyDictionaryView<K, V> second)
			=> first.Equals(second);

		public static bool operator !=(ReadOnlyDictionaryView<K, V> first, ReadOnlyDictionaryView<K, V> second)
			=> !first.Equals(second);
	}

	public readonly struct ReadOnlyDictionaryView<K, I, O> : IReadOnlyDictionary<K, O>, IEquatable<ReadOnlyDictionaryView<K, I, O>>
	{
		private readonly IReadOnlyDictionary<K, I> source;
		private readonly Converter<I, O> mapper;

		public ReadOnlyDictionaryView(IReadOnlyDictionary<K, I> dictionary, Converter<I, O> mapper)
		{
			source = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
			this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
		}

		public O this[K key] => mapper(source[key]);

		public IEnumerable<K> Keys => source.Keys;

		public IEnumerable<O> Values => source.Values.Select(mapper.ConvertDelegate<Func<I, O>>());

		public int Count => source.Count;

		public bool ContainsKey(K key) => source.ContainsKey(key);

		public IEnumerator<KeyValuePair<K, O>> GetEnumerator()
		{
			var mapper = this.mapper;
			return source
				.Select(entry => new KeyValuePair<K, O>(entry.Key, mapper(entry.Value)))
				.GetEnumerator();
		}

		public bool TryGetValue(K key, out O value)
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

		public bool Equals(ReadOnlyDictionaryView<K, I, O> other)
			=> ReferenceEquals(source, other.source) && Equals(mapper, other.mapper);

		public override int GetHashCode()
			=> source is null || mapper is null ? 0 : RuntimeHelpers.GetHashCode(source) ^ mapper.GetHashCode();

		public override bool Equals(object other)
			=> other is ReadOnlyDictionaryView<K, I, O> view ? Equals(view) : Equals(source, other);
	}
}
