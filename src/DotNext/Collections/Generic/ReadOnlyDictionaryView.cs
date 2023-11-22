using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic;

/// <summary>
/// Represents lazily converted read-only dictionary.
/// </summary>
/// <typeparam name="TKey">Type of dictionary keys.</typeparam>
/// <typeparam name="TInput">Type of values in the source dictionary.</typeparam>
/// <typeparam name="TOutput">Type of values in the converted dictionary.</typeparam>
/// <remarks>
/// Initializes a new lazily converted view.
/// </remarks>
/// <param name="dictionary">Read-only dictionary to convert.</param>
/// <param name="mapper">Value converter.</param>
[StructLayout(LayoutKind.Auto)]
public readonly struct ReadOnlyDictionaryView<TKey, TInput, TOutput>(IReadOnlyDictionary<TKey, TInput> dictionary, Func<TInput, TOutput> mapper) : IReadOnlyDictionary<TKey, TOutput>, IEquatable<ReadOnlyDictionaryView<TKey, TInput, TOutput>>
{
    private readonly IReadOnlyDictionary<TKey, TInput>? source = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
    private readonly Func<TInput, TOutput> mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

    /// <summary>
    /// Initializes a new lazily converted view.
    /// </summary>
    /// <param name="dictionary">Read-only dictionary to convert.</param>
    /// <param name="mapper">Value converter.</param>
    public ReadOnlyDictionaryView(IReadOnlyDictionary<TKey, TInput> dictionary, Converter<TInput, TOutput> mapper)
        : this(dictionary, Unsafe.As<Func<TInput, TOutput>>(mapper))
    {
    }

    /// <summary>
    /// Gets value associated with the key and convert it.
    /// </summary>
    /// <param name="key">The key of the element to get.</param>
    /// <returns>The converted value associated with the key.</returns>
    /// <exception cref="KeyNotFoundException">The requested key doesn't exist.</exception>
    public TOutput this[TKey key]
    {
        get
        {
            if (source is null)
                throw new KeyNotFoundException();

            return mapper.Invoke(source[key]);
        }
    }

    /// <summary>
    /// All dictionary keys.
    /// </summary>
    public IEnumerable<TKey> Keys => source?.Keys ?? Enumerable.Empty<TKey>();

    /// <summary>
    /// All converted dictionary values.
    /// </summary>
    public IEnumerable<TOutput> Values
        => source is null || mapper is null ? Enumerable.Empty<TOutput>() : source.Values.Select(mapper);

    /// <summary>
    /// Count of key/value pairs.
    /// </summary>
    public int Count => source?.Count ?? 0;

    /// <summary>
    /// Determines whether the wrapped dictionary contains an element
    /// with the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the dictionary.</param>
    /// <returns><see langword="true"/> if the key exists in the wrapped dictionary; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey(TKey key) => source?.ContainsKey(key) ?? false;

    private static IEnumerator<KeyValuePair<TKey, TOutput>> GetEnumerator(IReadOnlyDictionary<TKey, TInput> source, Func<TInput, TOutput> mapper)
    {
        foreach (var (key, value) in source)
            yield return new KeyValuePair<TKey, TOutput>(key, mapper.Invoke(value));
    }

    /// <summary>
    /// Returns enumerator over key/value pairs in the wrapped dictionary
    /// and performs conversion for each value in the pair.
    /// </summary>
    /// <returns>The enumerator over key/value pairs.</returns>
    public IEnumerator<KeyValuePair<TKey, TOutput>> GetEnumerator()
        => source is null or { Count: 0 } || mapper is null ? Enumerable.Empty<KeyValuePair<TKey, TOutput>>().GetEnumerator() : GetEnumerator(source, mapper);

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
        if (source is not null && source.TryGetValue(key, out var sourceVal))
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

    private bool Equals(in ReadOnlyDictionaryView<TKey, TInput, TOutput> other)
        => ReferenceEquals(source, other.source) && mapper == other.mapper;

    /// <summary>
    /// Determines whether two converted dictionaries are same.
    /// </summary>
    /// <param name="other">Other dictionary to compare.</param>
    /// <returns><see langword="true"/> if this view wraps the same source dictionary and contains the same converter as other view; otherwise, <see langword="false"/>.</returns>
    public bool Equals(ReadOnlyDictionaryView<TKey, TInput, TOutput> other)
        => Equals(in other);

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
        => other is ReadOnlyDictionaryView<TKey, TInput, TOutput> view ? Equals(in view) : Equals(source, other);

    /// <summary>
    /// Determines whether two views are same.
    /// </summary>
    /// <param name="first">The first dictionary to compare.</param>
    /// <param name="second">The second dictionary to compare.</param>
    /// <returns><see langword="true"/> if the first view wraps the same source dictionary and contains the same converter as the second view; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(in ReadOnlyDictionaryView<TKey, TInput, TOutput> first, in ReadOnlyDictionaryView<TKey, TInput, TOutput> second)
        => first.Equals(in second);

    /// <summary>
    /// Determines whether two views are not same.
    /// </summary>
    /// <param name="first">The first dictionary to compare.</param>
    /// <param name="second">The second collection to compare.</param>
    /// <returns><see langword="true"/> if the first view wraps the different source dictionary and contains the different converter as the second view; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(in ReadOnlyDictionaryView<TKey, TInput, TOutput> first, in ReadOnlyDictionaryView<TKey, TInput, TOutput> second)
        => !first.Equals(in second);
}