using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Represents lazily converted read-only dictionary.
    /// </summary>
    /// <typeparam name="TKey">Type of dictionary keys.</typeparam>
    /// <typeparam name="TInput">Type of values in the source dictionary.</typeparam>
    /// <typeparam name="TOutput">Type of values in the converted dictionary.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ReadOnlyDictionaryView<TKey, TInput, TOutput> : IReadOnlyDictionary<TKey, TOutput>, IEquatable<ReadOnlyDictionaryView<TKey, TInput, TOutput>>
    {
        private readonly IReadOnlyDictionary<TKey, TInput> source;
        private readonly ValueFunc<TInput, TOutput> mapper;

        /// <summary>
        /// Initializes a new lazily converted view.
        /// </summary>
        /// <param name="dictionary">Read-only dictionary to convert.</param>
        /// <param name="mapper">Value converter.</param>
        public ReadOnlyDictionaryView(IReadOnlyDictionary<TKey, TInput> dictionary, in ValueFunc<TInput, TOutput> mapper)
        {
            source = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
            this.mapper = mapper;
        }

        /// <summary>
        /// Gets value associated with the key and convert it.
        /// </summary>
        /// <param name="key">The key of the element to get.</param>
        /// <returns>The converted value associated with the key.</returns>
        public TOutput this[TKey key] => mapper.Invoke(source[key]);

        /// <summary>
        /// All dictionary keys.
        /// </summary>
        public IEnumerable<TKey> Keys => source.Keys;

        /// <summary>
        /// All converted dictionary values.
        /// </summary>
        public IEnumerable<TOutput> Values => source.Values.Select(mapper.ToDelegate());

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
        public bool ContainsKey(TKey key) => source.ContainsKey(key);

        /// <summary>
        /// Returns enumerator over key/value pairs in the wrapped dictionary
        /// and performs conversion for each value in the pair.
        /// </summary>
        /// <returns>The enumerator over key/value pairs.</returns>
        public IEnumerator<KeyValuePair<TKey, TOutput>> GetEnumerator()
        {
            foreach (var (key, value) in source)
                yield return new KeyValuePair<TKey, TOutput>(key, mapper.Invoke(value));
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
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TOutput value)
        {
            if (source.TryGetValue(key, out var sourceVal))
            {
                value = mapper.Invoke(sourceVal);
                return true;
            }

            value = default;
            return false;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        /// <summary>
        /// Determines whether two converted dictionaries are same.
        /// </summary>
        /// <param name="other">Other dictionary to compare.</param>
        /// <returns><see langword="true"/> if this view wraps the same source dictionary and contains the same converter as other view; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ReadOnlyDictionaryView<TKey, TInput, TOutput> other)
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
        public override bool Equals(object? other)
            => other is ReadOnlyDictionaryView<TKey, TInput, TOutput> view ? Equals(view) : Equals(source, other);

        /// <summary>
        /// Determines whether two views are same.
        /// </summary>
        /// <param name="first">The first dictionary to compare.</param>
        /// <param name="second">The second dictionary to compare.</param>
        /// <returns><see langword="true"/> if the first view wraps the same source dictionary and contains the same converter as the second view; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ReadOnlyDictionaryView<TKey, TInput, TOutput> first, ReadOnlyDictionaryView<TKey, TInput, TOutput> second)
            => first.Equals(second);

        /// <summary>
        /// Determines whether two views are not same.
        /// </summary>
        /// <param name="first">The first dictionary to compare.</param>
        /// <param name="second">The second collection to compare.</param>
        /// <returns><see langword="true"/> if the first view wraps the different source dictionary and contains the different converter as the second view; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ReadOnlyDictionaryView<TKey, TInput, TOutput> first, ReadOnlyDictionaryView<TKey, TInput, TOutput> second)
            => !first.Equals(second);
    }
}
