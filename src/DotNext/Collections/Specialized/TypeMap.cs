using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    private Entry[] entries;

    /// <summary>
    /// Initializes a new map.
    /// </summary>
    /// <param name="capacity">The initial capacity of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
    public TypeMap(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        entries = capacity == 0 ? Array.Empty<Entry>() : new Entry[capacity];
    }

    /// <summary>
    /// Initializes a new map of recommended capacity.
    /// </summary>
    public TypeMap()
        => entries = new Entry[ITypeMap<TValue>.RecommendedCapacity];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int index)
    {
        if (index >= entries.Length)
            Array.Resize(ref entries, ITypeMap<TValue>.RecommendedCapacity);
    }

    private ref TValue? GetValueRefOrAddDefault(int index, out bool exists)
    {
        EnsureCapacity(index);
        ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
        exists = holder.HasValue;
        holder.HasValue = true;
        return ref holder.Value;
    }

    /// <summary>
    /// Gets the value associated with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="exists"><see langword="true"/> if the association exists; <see langword="false"/> if the association is created.</param>
    /// <returns>The reference to the value associated with the type.</returns>
    public ref TValue? GetValueRefOrAddDefault<TKey>(out bool exists)
        => ref GetValueRefOrAddDefault(ITypeMap<TValue>.GetIndex<TKey>(), out exists);

    private bool TryAdd(int index, TValue value)
    {
        EnsureCapacity(index);
        ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
        if (holder.HasValue)
            return false;

        holder.Value = value;
        holder.HasValue = true;
        return true;
    }

    /// <summary>
    /// Associates a new value with the type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <exception cref="GenericArgumentException">A value associated with <typeparamref name="TKey"/> is already exist.</exception>
    public void Add<TKey>(TValue value)
    {
        if (!TryAdd(ITypeMap<TValue>.GetIndex<TKey>(), value))
            throw new GenericArgumentException<TKey>(ExceptionMessages.KeyAlreadyExists);
    }

    private void Set(int index, TValue value)
    {
        EnsureCapacity(index);
        ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
        holder.Value = value;
        holder.HasValue = true;
    }

    /// <summary>
    /// Associates the value with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value to set.</param>
    public void Set<TKey>(TValue value)
        => Set(ITypeMap<TValue>.GetIndex<TKey>(), value);

    private Optional<TValue> Replace(int index, TValue value)
    {
        EnsureCapacity(index);
        ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
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
    /// Replaces the existing value with a new value.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">A new value.</param>
    /// <returns>The replaced value.</returns>
    public Optional<TValue> Replace<TKey>(TValue value)
        => Replace(ITypeMap<TValue>.GetIndex<TKey>(), value);

    /// <summary>
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey<TKey>()
    {
        return ContainsKey(entries, ITypeMap<TValue>.GetIndex<TKey>());

        static bool ContainsKey(Entry[] entries, int index)
            => index < entries.Length && Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).HasValue;
    }

    private bool Remove(int index)
    {
        if (index >= entries.Length)
            goto fail;

        ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
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
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<TKey>()
        => Remove(ITypeMap<TValue>.GetIndex<TKey>());

    private bool Remove(int index, [MaybeNullWhen(false)] out TValue value)
    {
        bool result;

        if (index < entries.Length)
        {
            ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);

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
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value of the removed element.</param>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<TKey>([MaybeNullWhen(false)] out TValue value)
        => Remove(ITypeMap<TValue>.GetIndex<TKey>(), out value);

    private bool TryGetValue(int index, [MaybeNullWhen(false)] out TValue value)
    {
        if (index < entries.Length)
        {
            ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            value = holder.Value;
            return holder.HasValue;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Attempts to get the value associated with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue<TKey>([MaybeNullWhen(false)] out TValue value)
        => TryGetValue(ITypeMap<TValue>.GetIndex<TKey>(), out value);

    /// <summary>
    /// Removes all elements from this map.
    /// </summary>
    public void Clear() => Array.Clear(entries);
}