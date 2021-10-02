using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Collections.Specialized;

/// <summary>
/// Represents fast implementation of <see cref="ITypeMap{TValue}"/>
/// that is not thread safe.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public class TypeMap<TValue> : ITypeMap<TValue>
{
    [StructLayout(LayoutKind.Auto)]
    private struct Entry
    {
        internal bool HasValue;
        internal TValue? Value;
    }

    private Entry[] storage;

    /// <summary>
    /// Initializes a new map.
    /// </summary>
    /// <param name="capacity">The initial capacity of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
    public TypeMap(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        storage = capacity == 0 ? Array.Empty<Entry>() : new Entry[capacity];
    }

    /// <summary>
    /// Initializes a new map of recommended capacity.
    /// </summary>
    public TypeMap()
        => storage = new Entry[ITypeMap<TValue>.RecommendedCapacity];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity<TKey>()
    {
        if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
            Array.Resize(ref storage, ITypeMap<TValue>.RecommendedCapacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref Entry Get<TKey>()
    {
        Debug.Assert(ITypeMap<TValue>.GetIndex<TKey>() < storage.Length);

        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(storage), ITypeMap<TValue>.GetIndex<TKey>());
    }

    /// <summary>
    /// Gets the value associated with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="exists"><see langword="true"/> if the association exists; <see langword="false"/> if the association is created.</param>
    /// <returns>The reference to the value associated with the type.</returns>
    public ref TValue? GetValueRefOrAddDefault<TKey>(out bool exists)
    {
        EnsureCapacity<TKey>();
        ref var holder = ref Get<TKey>();
        exists = holder.HasValue;
        holder.HasValue = true;
        return ref holder.Value;
    }

    /// <summary>
    /// Associates a new value with the type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <exception cref="GenericArgumentException">A value associated with <typeparamref name="TKey"/> is already exist.</exception>
    public void Add<TKey>(TValue value)
    {
        EnsureCapacity<TKey>();
        ref var holder = ref Get<TKey>();
        if (holder.HasValue)
            throw new GenericArgumentException<TKey>(ExceptionMessages.KeyAlreadyExists);

        holder.Value = value;
        holder.HasValue = true;
    }

    /// <summary>
    /// Associates the value with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value to set.</param>
    public void Set<TKey>(TValue value)
    {
        EnsureCapacity<TKey>();
        ref var holder = ref Get<TKey>();
        holder.Value = value;
        holder.HasValue = true;
    }

    /// <summary>
    /// Replaces the existing value with a new value.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">A new value.</param>
    /// <returns>The replaced value.</returns>
    public Optional<TValue> Replace<TKey>(TValue value)
    {
        EnsureCapacity<TKey>();
        ref var holder = ref Get<TKey>();
        Optional<TValue> result;

        if (holder.HasValue)
        {
            result = holder.Value;
        }
        else
        {
            result = Optional<TValue>.None;
            holder.HasValue = true;
        }

        holder.Value = value;
        return result;
    }

    /// <summary>
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey<TKey>()
        => ITypeMap<TValue>.GetIndex<TKey>() < storage.Length && Get<TKey>().HasValue;

    /// <summary>
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<TKey>()
    {
        if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
            goto fail;

        ref var holder = ref Get<TKey>();
        if (holder.HasValue)
        {
            holder.HasValue = false;
            holder.Value = default;
            return true;
        }

    fail:
        return false;
    }

    /// <summary>
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value of the removed element.</param>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<TKey>([MaybeNullWhen(false)] out TValue value)
    {
        bool result;

        if (ITypeMap<TValue>.GetIndex<TKey>() < storage.Length)
        {
            ref var holder = ref Get<TKey>();

            value = holder.Value;
            holder.Value = default;

            result = holder.HasValue;
            holder.HasValue = false;
        }
        else
        {
            result = false;
            value = default;
        }

        return result;
    }

    /// <summary>
    /// Attempts to get the value associated with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue<TKey>([MaybeNullWhen(false)] out TValue value)
    {
        bool result;

        if (ITypeMap<TValue>.GetIndex<TKey>() < storage.Length)
        {
            ref var holder = ref Get<TKey>();
            value = holder.Value;
            result = holder.HasValue;
        }
        else
        {
            result = false;
            value = default;
        }

        return result;
    }

    /// <summary>
    /// Removes all elements from this map.
    /// </summary>
    public void Clear() => Array.Clear(storage);
}