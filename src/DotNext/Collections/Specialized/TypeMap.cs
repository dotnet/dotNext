using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Specialized;

using Runtime;
using Runtime.CompilerServices;

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
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        entries = capacity is 0 ? [] : new Entry[capacity];
    }

    /// <summary>
    /// Initializes a new map of recommended capacity.
    /// </summary>
    public TypeMap()
        => entries = new Entry[ITypeMap.RecommendedCapacity];

    private ref Entry this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Debug.Assert((uint)index < (uint)entries.Length);
            
            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref Entry EnsureSlotAllocated<TKey>()
        where TKey : allows ref struct
    {
        var index = TypeSlot<TKey>.Index;
        if ((uint)index >= (uint)entries.Length)
            Array.Resize(ref entries, index + 1);

        return ref this[index];
    }

    /// <summary>
    /// Gets the value associated with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="exists"><see langword="true"/> if the association exists; <see langword="false"/> if the association is created.</param>
    /// <returns>The reference to the value associated with the type.</returns>
    public ref TValue? GetValueRefOrAddDefault<TKey>(out bool exists)
    {
        ref var holder = ref EnsureSlotAllocated<TKey>();
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
        where TKey : allows ref struct
    {
        ref var holder = ref EnsureSlotAllocated<TKey>();
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
        where TKey : allows ref struct
    {
        ref var holder = ref EnsureSlotAllocated<TKey>();
        holder.Value = value;
        holder.HasValue = true;
    }

    /// <summary>
    /// Replaces the existing value with a new value.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="newValue">A new value.</param>
    /// <param name="oldValue">The replaced value.</param>
    /// <returns><see langword="true"/> if value is replaced; <see langword="false"/> if a new value is added without replacement.</returns>
    public bool Set<TKey>(TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
        where TKey : allows ref struct
    {
        ref var holder = ref EnsureSlotAllocated<TKey>();

        var result = holder.HasValue;
        if (result)
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
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey<TKey>()
        where TKey : allows ref struct
    {
        var index = TypeSlot<TKey>.Index;
        return (uint)index < (uint)entries.Length && this[index].HasValue;
    }

    /// <summary>
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<TKey>()
        where TKey : allows ref struct
    {
        var index = TypeSlot<TKey>.Index;
        if ((uint)index >= (uint)entries.Length)
            goto fail;

        ref var holder = ref this[index];
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
        where TKey : allows ref struct
    {
        var index = TypeSlot<TKey>.Index;
        bool result;

        if ((uint)index < (uint)entries.Length)
        {
            ref var holder = ref this[index];

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
        where TKey : allows ref struct
    {
        var index = TypeSlot<TKey>.Index;
        if ((uint)index < (uint)entries.Length)
        {
            ref var holder = ref this[index];
            value = holder.Value;
            return holder.HasValue;
        }

        value = default;
        return false;
    }

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
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        entries = capacity is 0 ? [] : new object?[capacity];
    }

    /// <summary>
    /// Initializes a new empty set.
    /// </summary>
    public TypeMap()
        => entries = new object?[ITypeMap.RecommendedCapacity];

    private ref object? this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Debug.Assert((uint)index < (uint)entries.Length);

            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
        }
    }

    /// <inheritdoc cref="ITypeMap.Add{T}(T)"/>
    public void Add<T>([DisallowNull] T value)
    {
        ref var holder = ref this[EnsureCapacity<T>()];
        if (holder is not null)
            throw new GenericArgumentException<T>(ExceptionMessages.KeyAlreadyExists);

        holder = value;
    }

    /// <inheritdoc cref="ITypeMap.Set{T}(T)"/>
    public void Set<T>([DisallowNull] T value)
        => this[EnsureCapacity<T>()] = value;

    /// <inheritdoc cref="ITypeMap.Set{T}(T, out T)"/>
    public bool Set<T>([DisallowNull] T newValue, [NotNullWhen(true)] out T? oldValue)
    {
        ref var holder = ref this[EnsureCapacity<T>()];

        var currentValue = holder;
        var result = currentValue is T;
        oldValue = result
            ? currentValue!.UnboxAny<T>()
            : default;

        holder = newValue;
        return result;
    }

    /// <inheritdoc cref="ITypeMap.Clear"/>
    public void Clear() => Array.Clear(entries);

    /// <inheritdoc cref="IReadOnlyTypeMap.Contains{T}"/>
    public bool Contains<T>()
    {
        var index = TypeSlot<T>.Index;
        return (uint)index < (uint)entries.Length && this[index] is T;
    }

    /// <inheritdoc cref="ITypeMap.Remove{T}()"/>
    public bool Remove<T>()
    {
        var index = TypeSlot<T>.Index;
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

    /// <inheritdoc cref="ITypeMap.Remove{T}(out T)"/>
    public bool Remove<T>([NotNullWhen(true)] out T? value)
    {
        var index = TypeSlot<T>.Index;
        if ((uint)index < (uint)entries.Length)
        {
            ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            var currentValue = holder;

            if (currentValue is T)
            {
                value = currentValue.UnboxAny<T>();
                Debug.Assert(value is not null);
                
                holder = null;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <inheritdoc cref="IReadOnlyTypeMap.TryGetValue{T}(out T)"/>
    public bool TryGetValue<T>([NotNullWhen(true)] out T? value)
    {
        var index = TypeSlot<T>.Index;
        if ((uint)index < (uint)entries.Length)
        {
            var holder = this[index];
            if (holder is T)
            {
                value = holder.UnboxAny<T>();
                Debug.Assert(value is not null);
                
                return true;
            }
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int EnsureCapacity<T>()
    {
        var index = TypeSlot<T>.Index;
        if ((uint)index >= (uint)entries.Length)
            Array.Resize(ref entries, index + 1);

        return index;
    }

    /// <summary>
    /// Gets the value associated with the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="exists"><see langword="true"/> if the association exists; <see langword="false"/> if the association is created.</param>
    /// <returns>The reference to the value associated with the type.</returns>
    public ref T GetValueRefOrAddDefault<T>(out bool exists)
        where T : struct
    {
        ref var holder = ref this[EnsureCapacity<T>()];
        if (holder is T)
        {
            exists = true;
        }
        else
        {
            holder = default(T);
            exists = false;
        }

        return ref BoxedValue<T>.UnsafeUnbox(holder);
    }
}