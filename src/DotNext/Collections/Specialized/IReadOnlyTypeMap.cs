using System.Diagnostics.CodeAnalysis;

namespace DotNext.Collections.Specialized;

/// <summary>
/// Represents read-only view of the dictionary where
/// the values are associated with the types.
/// </summary>
/// <typeparam name="TValue">The type of the values in the map.</typeparam>
public interface IReadOnlyTypeMap<TValue>
{
    /// <summary>
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    bool ContainsKey<TKey>();

    /// <summary>
    /// Attempts to get the value associated with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    bool TryGetValue<TKey>([MaybeNullWhen(false)] out TValue value);
}

/// <summary>
/// Represents read-only view of a set of typed values.
/// </summary>
public interface IReadOnlyTypeMap
{
    /// <summary>
    /// Determines whether the set has the value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns><see langword="true"/> if this set has a value of type <typeparamref name="T"/>; otherwise, <see langword="false"/>.</returns>
    bool Contains<T>();

    /// <summary>
    /// Attempts to get the value of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value stored in the set.</param>
    /// <returns><see langword="true"/> if the value of type <typeparamref name="T"/> exists in this set; otherwise, <see langword="false"/>.</returns>
    bool TryGetValue<T>([NotNullWhen(true)] out T? value);
}