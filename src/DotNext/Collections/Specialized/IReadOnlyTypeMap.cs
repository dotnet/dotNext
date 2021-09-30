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