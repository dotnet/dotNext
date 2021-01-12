using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Represents lazily converted read-only dictionary.
    /// </summary>
    /// <typeparam name="K">Type of dictionary keys.</typeparam>
    /// <typeparam name="I">Type of values in the source dictionary.</typeparam>
    /// <typeparam name="O">Type of values in the converted dictionary.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ReadOnlyDictionaryView<K, I, O> : IReadOnlyDictionary<K, O>, IEquatable<ReadOnlyDictionaryView<K, I, O>>
    {
        private readonly IReadOnlyDictionary<K, I> source;
        private readonly ValueFunc<I, O> mapper;

        /// <summary>
        /// Initializes a new lazily converted view.
        /// </summary>
        /// <param name="dictionary">Read-only dictionary to convert.</param>
        /// <param name="mapper">Value converter.</param>
        public ReadOnlyDictionaryView(IReadOnlyDictionary<K, I> dictionary, in ValueFunc<I, O> mapper)
        {
            source = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
            this.mapper = mapper;
        }

        /// <summary>
        /// Gets value associated with the key and convert it.
        /// </summary>
        /// <param name="key">The key of the element to get.</param>
        /// <returns>The converted value associated with the key.</returns>
		public O this[K key] => mapper.Invoke(source[key]);

        /// <summary>
        /// All dictionary keys.
        /// </summary>
		public IEnumerable<K> Keys => source.Keys;

        /// <summary>
        /// All converted dictionary values.
        /// </summary>
        public IEnumerable<O> Values => source.Values.Select(mapper.ToDelegate());

        /// <summary>
        /// Count of key/value pairs.
        /// </summary>
		public int Count => source.Count;

        /// <summary>
        /// Determines whether the wrapped dictionary contains an element
        /// with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the dictionary.</param>
        /// <returns><see langword="true"/> if the key exists in the wrapped dictionary; otherwise, <see langword="false"/>.</returns>
        public bool ContainsKey(K key) => source.ContainsKey(key);

        /// <summary>
        /// Returns enumerator over key/value pairs in the wrapped dictionary
        /// and performs conversion for each value in the pair.
        /// </summary>
        /// <returns>The enumerator over key/value pairs.</returns>
		public IEnumerator<KeyValuePair<K, O>> GetEnumerator()
        {
            foreach (var (key, value) in source)
                yield return new KeyValuePair<K, O>(key, mapper.Invoke(value));
        }

        /// <summary>
        /// Returns the converted value associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="value">The converted value associated with the specified key, if the
        /// key is found; otherwise, the <see langword="default"/> value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns><see langword="true"/>, if the dictionary contains the specified key; otherwise, <see langword="false"/>.</returns>
        public bool TryGetValue(K key, out O value)
        {
            if (source.TryGetValue(key, out var sourceVal))
            {
                value = mapper.Invoke(sourceVal);
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

        /// <summary>
        /// Determines whether two converted dictionaries are same.
        /// </summary>
        /// <param name="other">Other dictionary to compare.</param>
        /// <returns><see langword="true"/> if this view wraps the same source dictionary and contains the same converter as other view; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ReadOnlyDictionaryView<K, I, O> other)
            => ReferenceEquals(source, other.source) && mapper == other.mapper;

        /// <summary>
        /// Returns hash code for the this view.
        /// </summary>
        /// <returns>The hash code of this view.</returns>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(source) ^ mapper.GetHashCode();

        /// <summary>
        /// Determines whether two converted dictionaries are same.
        /// </summary>
        /// <param name="other">Other dictionary to compare.</param>
        /// <returns><see langword="true"/> if this view wraps the same source dictionary and contains the same converter as other view; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
            => other is ReadOnlyDictionaryView<K, I, O> view ? Equals(view) : Equals(source, other);

        /// <summary>
        /// Determines whether two views are same.
        /// </summary>
        /// <param name="first">The first dictionary to compare.</param>
        /// <param name="second">The second dictionary to compare.</param>
        /// <returns><see langword="true"/> if the first view wraps the same source dictionary and contains the same converter as the second view; otherwise, <see langword="false"/>.</returns>
		public static bool operator ==(ReadOnlyDictionaryView<K, I, O> first, ReadOnlyDictionaryView<K, I, O> second)
            => first.Equals(second);

        /// <summary>
        /// Determines whether two views are not same.
        /// </summary>
        /// <param name="first">The first dictionary to compare.</param>
        /// <param name="second">The second collection to compare.</param>
        /// <returns><see langword="true"/> if the first view wraps the different source dictionary and contains the different converter as the second view; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ReadOnlyDictionaryView<K, I, O> first, ReadOnlyDictionaryView<K, I, O> second)
            => !first.Equals(second);
    }
}
