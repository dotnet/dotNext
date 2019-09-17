using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Represents various extensions for types <see cref="Dictionary{TKey, TValue}"/>
    /// and <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    public static class Dictionary
    {
        private static class Indexer<D, K, V>
            where D : class, IEnumerable<KeyValuePair<K, V>>
        {
            internal static readonly Func<D, K, V> Getter;
            internal static readonly Action<D, K, V> Setter;

            static Indexer()
            {
                foreach (var member in typeof(D).GetDefaultMembers())
                    if (member is PropertyInfo indexer)
                    {
                        Getter = indexer.GetMethod.CreateDelegate<Func<D, K, V>>();
                        Setter = indexer.SetMethod?.CreateDelegate<Action<D, K, V>>();
                        return;
                    }
                Debug.Fail(ExceptionMessages.UnreachableCodeDetected);
            }
        }

        /// <summary>
        /// Provides strongly-typed access to dictionary indexer.
        /// </summary>
        /// <typeparam name="K">Type of keys in the dictionary.</typeparam>
        /// <typeparam name="V">Type of values in the dictionary.</typeparam>
		public static class Indexer<K, V>
        {
            /// <summary>
            /// Represents read-only dictionary indexer.
            /// </summary>
			public static Func<IReadOnlyDictionary<K, V>, K, V> ReadOnly => Indexer<IReadOnlyDictionary<K, V>, K, V>.Getter;

            /// <summary>
            /// Represents dictionary value getter.
            /// </summary>
			public static Func<IDictionary<K, V>, K, V> Getter => Indexer<IDictionary<K, V>, K, V>.Getter;

            /// <summary>
            /// Represents dictionary value setter.
            /// </summary>
			public static Action<IDictionary<K, V>, K, V> Setter => Indexer<IDictionary<K, V>, K, V>.Setter;
        }

        /// <summary>
        /// Returns <see cref="IReadOnlyDictionary{TKey, TValue}.get_Item"/> as
        /// delegate attached to the dictionary instance.
        /// </summary>
        /// <typeparam name="K">Type of dictionary keys.</typeparam>
        /// <typeparam name="V">Type of dictionary values.</typeparam>
        /// <param name="dictionary">Read-only dictionary instance.</param>
        /// <returns>A delegate representing dictionary indexer.</returns>
        public static Func<K, V> IndexerGetter<K, V>(this IReadOnlyDictionary<K, V> dictionary)
            => Indexer<K, V>.ReadOnly.Bind(dictionary);

        /// <summary>
        /// Returns <see cref="IDictionary{TKey, TValue}.get_Item"/> as
        /// delegate attached to the dictionary instance.
        /// </summary>
        /// <typeparam name="K">Type of dictionary keys.</typeparam>
        /// <typeparam name="V">Type of dictionary values.</typeparam>
        /// <param name="dictionary">Mutable dictionary instance.</param>
        /// <returns>A delegate representing dictionary indexer.</returns>
        public static Func<K, V> IndexerGetter<K, V>(this IDictionary<K, V> dictionary)
            => Indexer<K, V>.Getter.Bind(dictionary);

        /// <summary>
        /// Returns <see cref="IDictionary{TKey, TValue}.set_Item"/> as
        /// delegate attached to the dictionary instance.
        /// </summary>
        /// <typeparam name="K">Type of dictionary keys.</typeparam>
        /// <typeparam name="V">Type of dictionary values.</typeparam>
        /// <param name="dictionary">Mutable dictionary instance.</param>
        /// <returns>A delegate representing dictionary indexer.</returns>
        public static Action<K, V> IndexerSetter<K, V>(this IDictionary<K, V> dictionary)
            => Indexer<K, V>.Setter.Bind(dictionary);

        /// <summary>
        /// Deconstruct key/value pair.
        /// </summary>
        /// <typeparam name="K">Type of key.</typeparam>
        /// <typeparam name="V">Type of value.</typeparam>
        /// <param name="pair">A pair to decompose.</param>
        /// <param name="key">Deconstructed key.</param>
        /// <param name="value">Deconstructed value.</param>
        public static void Deconstruct<K, V>(this KeyValuePair<K, V> pair, out K key, out V value)
        {
            key = pair.Key;
            value = pair.Value;
        }

        /// <summary>
        /// Adds a key-value pair to the dictionary if the key does not exist.
        /// </summary>
        /// <typeparam name="K">The key type of the dictionary.</typeparam>
        /// <typeparam name="V">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">The source dictionary.</param>
        /// <param name="key">The key of the key-value pair.</param>
        /// <returns>
        /// The corresponding value in the dictionary if <paramref name="key"/> already exists, 
        /// or a new instance of <typeparamref name="V"/>.
        /// </returns>
        public static V GetOrAdd<K, V>(this Dictionary<K, V> dictionary, K key)
            where V : new()
        {
            if (!dictionary.TryGetValue(key, out var value))
                dictionary.Add(key, value = new V());
            return value;
        }

        /// <summary>
        /// Adds a key-value pair to the dictionary if the key does not exist.
        /// </summary>
        /// <typeparam name="K">The key type of the dictionary.</typeparam>
        /// <typeparam name="V">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">The source dictionary.</param>
        /// <param name="key">The key of the key-value pair.</param>
        /// <param name="value">The value of the key-value pair.</param>
        /// <returns>
        /// The corresponding value in the dictionary if <paramref name="key"/> already exists, 
        /// or <paramref name="value"/>.
        /// </returns>
        public static V GetOrAdd<K, V>(this Dictionary<K, V> dictionary, K key, V value)
        {
            if (dictionary.TryGetValue(key, out var temp))
                value = temp;
            else
                dictionary.Add(key, value);
            return value;
        }

        /// <summary>
        /// Generates a value and adds the key-value pair to the dictionary if the key does not
        /// exist.
        /// </summary>
        /// <typeparam name="K">The key type of the dictionary.</typeparam>
        /// <typeparam name="V">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">The source dictionary.</param>
        /// <param name="key">The key of the key-value pair.</param>
        /// <param name="valueFactory">
        /// The function used to generate the value from the key.
        /// </param>
        /// <returns>
        /// The corresponding value in the dictionary if <paramref name="key"/> already exists, 
        /// or the value generated by <paramref name="valueFactory"/>.
        /// </returns>
        public static V GetOrAdd<K, V>(this Dictionary<K, V> dictionary, K key, in ValueFunc<K, V> valueFactory)
        {
            if (dictionary.TryGetValue(key, out var value))
                return value;
            else
            {
                value = valueFactory.Invoke(key);
                dictionary.Add(key, value);
                return value;
            }
        }

        /// <summary>
        /// Generates a value and adds the key-value pair to the dictionary if the key does not
        /// exist.
        /// </summary>
        /// <typeparam name="K">The key type of the dictionary.</typeparam>
        /// <typeparam name="V">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">The source dictionary.</param>
        /// <param name="key">The key of the key-value pair.</param>
        /// <param name="valueFactory">
        /// The function used to generate the value from the key.
        /// </param>
        /// <returns>
        /// The corresponding value in the dictionary if <paramref name="key"/> already exists, 
        /// or the value generated by <paramref name="valueFactory"/>.
        /// </returns>
        public static V GetOrAdd<K, V>(this Dictionary<K, V> dictionary, K key, Func<K, V> valueFactory)
            => GetOrAdd(dictionary, key, new ValueFunc<K, V>(valueFactory, true));

        /// <summary>
        /// Applies specific action to each dictionary
        /// </summary>
        /// <typeparam name="K">The key type of the dictionary.</typeparam>
        /// <typeparam name="V">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="action">The action to be applied for each key/value pair.</param>
		public static void ForEach<K, V>(this IDictionary<K, V> dictionary, in ValueAction<K, V> action)
        {
            foreach (var (key, value) in dictionary)
                action.Invoke(key, value);
        }

        /// <summary>
        /// Applies specific action to each dictionary
        /// </summary>
        /// <typeparam name="K">The key type of the dictionary.</typeparam>
        /// <typeparam name="V">The value type of the dictionary.</typeparam>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="action">The action to be applied for each key/value pair.</param>
        public static void ForEach<K, V>(this IDictionary<K, V> dictionary, Action<K, V> action)
            => ForEach(dictionary, new ValueAction<K, V>(action, true));

        /// <summary>
        /// Gets dictionary value by key if it exists or
        /// invoke <paramref name="defaultValue"/> and
        /// return its result as a default value.
        /// </summary>
        /// <typeparam name="K">Type of dictionary keys.</typeparam>
        /// <typeparam name="V">Type of dictionary values.</typeparam>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="key">A key associated with the value.</param>
        /// <param name="defaultValue">A delegate to be invoked if key doesn't exist in the dictionary.</param>
        /// <returns>The value associated with the key or returned by the delegate.</returns>
        public static V GetOrInvoke<K, V>(this IDictionary<K, V> dictionary, K key, in ValueFunc<V> defaultValue)
            => dictionary.TryGetValue(key, out var value) ? value : defaultValue.Invoke();

        /// <summary>
        /// Gets dictionary value by key if it exists or
        /// invoke <paramref name="defaultValue"/> and
        /// return its result as a default value.
        /// </summary>
        /// <typeparam name="K">Type of dictionary keys.</typeparam>
        /// <typeparam name="V">Type of dictionary values.</typeparam>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="key">A key associated with the value.</param>
        /// <param name="defaultValue">A delegate to be invoked if key doesn't exist in the dictionary.</param>
        /// <returns>The value associated with the key or returned by the delegate.</returns>
		public static V GetOrInvoke<K, V>(this IDictionary<K, V> dictionary, K key, Func<V> defaultValue)
            => GetOrInvoke(dictionary, key, new ValueFunc<V>(defaultValue, true));

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="key">The key whose value to get.</param>
        /// <typeparam name="K">Type of dictionary keys.</typeparam>
        /// <typeparam name="V">Type of dictionary values.</typeparam>
        /// <returns>The optional value associated with the key.</returns>
        public static Optional<V> TryGetValue<K, V>(this IDictionary<K, V> dictionary, K key)
            => dictionary.TryGetValue(key, out var value) ? new Optional<V>(value) : Optional<V>.Empty;

        /// <summary>
        /// Removes the value with the specified key and return the removed value.
        /// </summary>
        /// <param name="dictionary">A dictionary to modify.</param>
        /// <param name="key">The key of the element to remove.</param>
        /// <typeparam name="K">Type of dictionary keys.</typeparam>
        /// <typeparam name="V">Type of dictionary values.</typeparam>
        /// <returns>The removed value.</returns>
        public static Optional<V> TryRemove<K, V>(this IDictionary<K, V> dictionary, K key)
            => dictionary.TryGetValue(key, out var value) && dictionary.Remove(key) ? new Optional<V>(value) : Optional<V>.Empty;

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="dictionary">A dictionary to read from.</param>
        /// <param name="key">The key whose value to get.</param>
        /// <typeparam name="K">Type of dictionary keys.</typeparam>
        /// <typeparam name="V">Type of dictionary values.</typeparam>
        /// <returns>The optional value associated with the key.</returns>        
        public static Optional<V> TryGetValue<K, V>(IReadOnlyDictionary<K, V> dictionary, K key)
            => dictionary.TryGetValue(key, out var value) ? new Optional<V>(value) : Optional<V>.Empty;

        /// <summary>
        /// Applies lazy conversion for each dictionary value.
        /// </summary>
        /// <typeparam name="K">Type of keys.</typeparam>
        /// <typeparam name="V">Type of values.</typeparam>
        /// <typeparam name="T">Type of mapped values.</typeparam>
        /// <param name="dictionary">A dictionary to be mapped.</param>
        /// <param name="mapper">Mapping function.</param>
        /// <returns>Read-only view of the dictionary where each value is converted in lazy manner.</returns>
        public static ReadOnlyDictionaryView<K, V, T> ConvertValues<K, V, T>(this IReadOnlyDictionary<K, V> dictionary, in ValueFunc<V, T> mapper)
            => new ReadOnlyDictionaryView<K, V, T>(dictionary, mapper);

        /// <summary>
        /// Applies lazy conversion for each dictionary value.
        /// </summary>
        /// <typeparam name="K">Type of keys.</typeparam>
        /// <typeparam name="V">Type of values.</typeparam>
        /// <typeparam name="T">Type of mapped values.</typeparam>
        /// <param name="dictionary">A dictionary to be mapped.</param>
        /// <param name="mapper">Mapping function.</param>
        /// <returns>Read-only view of the dictionary where each value is converted in lazy manner.</returns>
        public static ReadOnlyDictionaryView<K, V, T> ConvertValues<K, V, T>(this IReadOnlyDictionary<K, V> dictionary, Converter<V, T> mapper)
            => ConvertValues(dictionary, mapper.AsValueFunc(true));
    }
}
