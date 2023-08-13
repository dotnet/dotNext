using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Specialized;

/// <summary>
/// Represents fast implementation of <see cref="ITypeMap{TValue}"/>
/// that is not thread safe.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public partial class TypeMap<TValue> : ITypeMap<TValue>
{
    [StructLayout(LayoutKind.Auto)]
    internal struct Entry
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

        entries = capacity is 0 ? Array.Empty<Entry>() : new Entry[capacity];
    }

    /// <summary>
    /// Initializes a new map of recommended capacity.
    /// </summary>
    public TypeMap()
        => entries = new Entry[ITypeMap.RecommendedCapacity];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int index)
    {
        if ((uint)index >= (uint)entries.Length)
            Array.Resize(ref entries, ITypeMap.RecommendedCapacity);
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
        => ref GetValueRefOrAddDefault(ITypeMap.GetIndex<TKey>(), out exists);

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
        if (!TryAdd(ITypeMap.GetIndex<TKey>(), value))
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
        => Set(ITypeMap.GetIndex<TKey>(), value);

    private bool Set(int index, TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
    {
        EnsureCapacity(index);
        ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);

        bool result;
        if (result = holder.HasValue)
        {
            oldValue = holder.Value;
        }
        else
        {
            oldValue = default;
            holder.HasValue = true;
        }

        holder.Value = newValue;
        return result;
    }

    /// <summary>
    /// Replaces the existing value with a new value.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="newValue">A new value.</param>
    /// <param name="oldValue">The replaced value.</param>
    /// <returns><see langword="true"/> if value is replaced; <see langword="false"/> if a new value is added without replacement.</returns>
    public bool Set<TKey>(TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
        => Set(ITypeMap.GetIndex<TKey>(), newValue, out oldValue);

    /// <summary>
    /// Replaces the existing value with a new value.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="newValue">A new value.</param>
    /// <returns>The replaced value.</returns>
    [Obsolete("Use Set overload instead")]
    public Optional<TValue> Replace<TKey>(TValue newValue)
        => Set(ITypeMap.GetIndex<TKey>(), newValue, out var oldValue) ? Optional.Some(oldValue!) : Optional.None<TValue>();

    /// <summary>
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey<TKey>()
    {
        return ContainsKey(entries, ITypeMap.GetIndex<TKey>());

        static bool ContainsKey(Entry[] entries, int index)
            => (uint)index < (uint)entries.Length && Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).HasValue;
    }

    private bool Remove(int index)
    {
        if ((uint)index >= (uint)entries.Length)
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
        => Remove(ITypeMap.GetIndex<TKey>());

    private bool Remove(int index, [MaybeNullWhen(false)] out TValue value)
    {
        bool result;

        if ((uint)index < (uint)entries.Length)
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
        => Remove(ITypeMap.GetIndex<TKey>(), out value);

    private bool TryGetValue(int index, [MaybeNullWhen(false)] out TValue value)
    {
        if ((uint)index < (uint)entries.Length)
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
        => TryGetValue(ITypeMap.GetIndex<TKey>(), out value);

    /// <summary>
    /// Removes all elements from this map.
    /// </summary>
    public void Clear() => Array.Clear(entries);
}

/// <summary>
/// Represents fast implementation of <see cref="ITypeMap"/>
/// which is not thread safe.
/// </summary>
public partial class TypeMap : ITypeMap
{
    private object?[] entries;

    /// <summary>
    /// Initializes a new empty set.
    /// </summary>
    /// <param name="capacity">The initial capacity of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
    public TypeMap(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        entries = capacity is 0 ? Array.Empty<object>() : new object?[capacity];
    }

    /// <summary>
    /// Initializes a new empty set.
    /// </summary>
    public TypeMap()
        => entries = new object?[ITypeMap.RecommendedCapacity];

    /// <inheritdoc cref="ITypeMap.Add{T}(T)"/>
    public void Add<T>([DisallowNull] T value)
    {
        if (!TryAdd(ITypeMap.GetIndex<T>(), value))
            throw new GenericArgumentException<T>(ExceptionMessages.KeyAlreadyExists);
    }

    private bool TryAdd<T>(int index, T value)
    {
        EnsureCapacity(index);
        ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
        if (holder is not null)
            return false;

        holder = value;
        return true;
    }

    /// <inheritdoc cref="ITypeMap.Set{T}(T)"/>
    public void Set<T>([DisallowNull] T value)
        => Set(ITypeMap.GetIndex<T>(), value);

    private void Set<T>(int index, T value)
    {
        EnsureCapacity(index);
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index) = value;
    }

    /// <inheritdoc cref="ITypeMap.Set{T}(T, out T)"/>
    public bool Set<T>([DisallowNull] T newValue, [MaybeNullWhen(false)] out T oldValue)
        => Set(ITypeMap.GetIndex<T>(), newValue, out oldValue);

    private bool Set<T>(int index, T newValue, [MaybeNullWhen(false)] out T oldValue)
    {
        EnsureCapacity(index);
        ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);

        bool result;
        oldValue = (result = holder is T)
            ? (T)holder!
            : default;

        holder = newValue;
        return result;
    }

    /// <inheritdoc cref="ITypeMap.Clear"/>
    public void Clear() => Array.Clear(entries);

    /// <inheritdoc cref="IReadOnlyTypeMap.Contains{T}"/>
    public bool Contains<T>()
    {
        return Contains(entries, ITypeMap.GetIndex<T>());

        static bool Contains(object?[] entries, int index)
            => (uint)index < (uint)entries.Length && Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index) is not null;
    }

    private bool Remove(int index)
    {
        bool result;
        if ((uint)index < (uint)entries.Length)
        {
            ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            result = holder is not null;
            holder = null;
        }
        else
        {
            result = false;
        }

        return result;
    }

    /// <inheritdoc cref="ITypeMap.Remove{T}()"/>
    public bool Remove<T>()
        => Remove(ITypeMap.GetIndex<T>());

    private bool Remove<T>(int index, [MaybeNullWhen(false)] out T value)
    {
        if ((uint)index < (uint)entries.Length)
        {
            ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);

            if (holder is T)
            {
                value = (T)holder;
                holder = null;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <inheritdoc cref="ITypeMap.Remove{T}(out T)"/>
    public bool Remove<T>([MaybeNullWhen(false)] out T value)
        => Remove(ITypeMap.GetIndex<T>(), out value);

    private bool TryGetValue<T>(int index, [MaybeNullWhen(false)] out T value)
    {
        if ((uint)index < (uint)entries.Length)
        {
            var holder = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            if (holder is T)
            {
                value = (T)holder;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <inheritdoc cref="IReadOnlyTypeMap.TryGetValue{T}(out T)"/>
    public bool TryGetValue<T>([MaybeNullWhen(false)] out T value)
        => TryGetValue(ITypeMap.GetIndex<T>(), out value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int index)
    {
        if ((uint)index >= (uint)entries.Length)
            Array.Resize(ref entries, ITypeMap.RecommendedCapacity);
    }

    /// <summary>
    /// Gets the value associated with the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="exists"><see langword="true"/> if the association exists; <see langword="false"/> if the association is created.</param>
    /// <returns>The reference to the value associated with the type.</returns>
    public ref T GetValueRefOrAddDefault<T>(out bool exists)
        where T : struct
        => ref GetValueRefOrAddDefault<T>(ITypeMap.GetIndex<T>(), out exists);

    private ref T GetValueRefOrAddDefault<T>(int index, out bool exists)
        where T : struct
    {
        EnsureCapacity(index);
        ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
        if (holder is T)
        {
            exists = true;
        }
        else
        {
            holder = default(T);
            exists = false;
        }

        return ref Unsafe.Unbox<T>(holder);
    }
}