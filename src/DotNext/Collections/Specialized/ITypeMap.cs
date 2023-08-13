using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Collections.Specialized;

/// <summary>
/// Represents specialized dictionary where the each key is represented by generic
/// parameter.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public interface ITypeMap<TValue> : IReadOnlyTypeMap<TValue>
{
    /// <summary>
    /// Adds a new value with the type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <exception cref="GenericArgumentException">A value associated with <typeparamref name="TKey"/> already exists.</exception>
    void Add<TKey>(TValue value);

    /// <summary>
    /// Adds or overwrites the value with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value to set.</param>
    void Set<TKey>(TValue value);

    /// <summary>
    /// Replaces the existing value with a new value.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="newValue">A new value.</param>
    /// <param name="oldValue">The replaced value.</param>
    /// <returns><see langword="true"/> if value is replaced; <see langword="false"/> if a new value is added without replacement.</returns>
    bool Set<TKey>(TValue newValue, [MaybeNullWhen(false)] out TValue oldValue);

    /// <summary>
    /// Replaces the existing value with a new value.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="newValue">A new value.</param>
    /// <returns>The replaced value.</returns>
    [Obsolete("Use Set overload instead")]
    Optional<TValue> Replace<TKey>(TValue newValue)
        => Set<TKey>(newValue, out var oldValue) ? Optional.Some(oldValue!) : Optional.None<TValue>();

    /// <summary>
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    bool Remove<TKey>();

    /// <summary>
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value of the removed element.</param>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    bool Remove<TKey>([MaybeNullWhen(false)] out TValue value);

    /// <summary>
    /// Removes all elements from this map.
    /// </summary>
    void Clear();
}

/// <summary>
/// Represents specialized set that store values of different types efficiently.
/// </summary>
public interface ITypeMap : IReadOnlyTypeMap
{
    private const int DefaultInitialCapacity = 16;
    private static volatile int typeLastIndex = -1;

    private static class TypeSlot<T>
    {
        internal static readonly int Index = Interlocked.Increment(ref typeLastIndex);
    }

    /// <summary>
    /// Gets zero-based index of the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns>The index of the type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetIndex<TKey>() => TypeSlot<TKey>.Index;

    /// <summary>
    /// Gets the recommended initial capacity of the internal array.
    /// </summary>
    internal static int RecommendedCapacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var capacity = typeLastIndex + 1;

            if (capacity < DefaultInitialCapacity)
            {
                capacity = DefaultInitialCapacity;
            }
            else
            {
                capacity <<= 1;
                if ((uint)capacity > (uint)Array.MaxLength)
                    capacity = Array.MaxLength;
            }

            return capacity;
        }
    }

    /// <summary>
    /// Adds a new value associated with type <typeparamref name="T"/> to this set.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be added.</param>
    /// <exception cref="GenericArgumentException">A value associated with <typeparamref name="T"/> already exists.</exception>
    void Add<T>([DisallowNull] T value);

    /// <summary>
    /// Adds or overwrites the value associated with type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to set.</param>
    void Set<T>([DisallowNull] T value);

    /// <summary>
    /// Replaces the value associated with type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="newValue">The value to set.</param>
    /// <param name="oldValue">The replaced value.</param>
    /// <returns><see langword="true"/> if value is replaced; <see langword="false"/> if a new value is added without replacement.</returns>
    bool Set<T>([DisallowNull] T newValue, [MaybeNullWhen(false)] out T oldValue);

    /// <summary>
    /// Attempts to remove the value of type <typeparamref name="T"/> from this set.
    /// </summary>
    /// <typeparam name="T">The type which value should be removed.</typeparam>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    bool Remove<T>();

    /// <summary>
    /// Attempts to remove the value of type <typeparamref name="T"/> from this set.
    /// </summary>
    /// <typeparam name="T">The type which value should be removed.</typeparam>
    /// <param name="value">Removed value.</param>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    bool Remove<T>([MaybeNullWhen(false)] out T value);

    /// <summary>
    /// Removes all elements from this set.
    /// </summary>
    void Clear();
}