using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Specialized;

using Threading;

/// <summary>
/// Represents thread-safe implementation of <see cref="ITypeMap{TValue}"/> interface.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public partial class ConcurrentTypeMap<TValue> : ITypeMap<TValue>
{
    private const int EmptyValueState = 0;
    private const int LockedState = 1;
    private const int HasValueState = 2;

    internal sealed class Entry
    {
        private int state; // volatile
        internal TValue? Value;

        internal int AcquireLock()
        {
            int currentState;
            for (var spinner = new SpinWait(); ; spinner.SpinOnce())
            {
                currentState = state.VolatileRead();

                if (currentState is not LockedState && state.CompareAndSet(currentState, LockedState))
                    return currentState;
            }
        }

        internal void ReleaseLock(int newState) => state.VolatileWrite(newState);

        internal bool HasValue
        {
            get
            {
                int currentState;

                for (var spinner = new SpinWait(); ; spinner.SpinOnce())
                {
                    currentState = state.VolatileRead();

                    if (currentState is LockedState)
                        continue;

                    return currentState is HasValueState;
                }
            }
        }

        internal bool TryAcquireLock(int expectedState)
        {
            int currentState;

            for (var spinner = new SpinWait(); ; spinner.SpinOnce())
            {
                currentState = state.VolatileRead();

                if (currentState is LockedState)
                    continue;

                if (currentState != expectedState)
                    return false;

                if (state.CompareAndSet(currentState, LockedState))
                    return true;
            }
        }
    }

    // Assuming that the map will not contain hunders or thousands for entries.
    // If so, we can keep the lock for each entry instead of buckets as in ConcurrentDictionaryMap.
    // As a result, we don't need the concurrency level. Also, we can modify different entries concurrently
    // and perform resizing in parallel with read/write of individual entry
    private volatile Entry[] entries;

    /// <summary>
    /// Initializes a new map.
    /// </summary>
    /// <param name="capacity">The initial capacity of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
    public ConcurrentTypeMap(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        var entries = capacity is 0 ? Array.Empty<Entry>() : new Entry[capacity];
        entries.AsSpan().Initialize();
        this.entries = entries;
    }

    /// <summary>
    /// Initializes a new map of recommended capacity.
    /// </summary>
    public ConcurrentTypeMap()
    {
        var entries = new Entry[ITypeMap<TValue>.RecommendedCapacity];
        entries.AsSpan().Initialize();
        this.entries = entries;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void Resize(Entry[] entries)
    {
        // make sure nobody resized the table while we were waiting for the first lock
        if (!ReferenceEquals(entries, this.entries))
            return;

        // do resize
        var firstUnitialized = entries.Length;
        Array.Resize(ref entries, ITypeMap<TValue>.RecommendedCapacity);

        // initializes the rest of the array
        for (var i = firstUnitialized; i < entries.Length; i++)
            entries[i] = new();

        // commit resized storage
        this.entries = entries;
    }

    /// <inheritdoc />
    void ITypeMap<TValue>.Add<TKey>(TValue value)
    {
        if (!TryAdd<TKey>(value))
            throw new GenericArgumentException<TKey>(ExceptionMessages.KeyAlreadyExists);
    }

    private bool TryAdd(int index, TValue value)
    {
        for (Entry[] entries; ;)
        {
            entries = this.entries;

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);

            bool added;
            if (added = entry.TryAcquireLock(EmptyValueState))
            {
                entry.Value = value;
                entry.ReleaseLock(HasValueState);
            }

            return added;
        }
    }

    /// <summary>
    /// Attempts to associate a value with the type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <returns><see langword="true"/> if the value is added; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd<TKey>(TValue value)
        => TryAdd(ITypeMap<TValue>.GetIndex<TKey>(), value);

    private void Set(int index, TValue value)
    {
        for (Entry[] entries; ;)
        {
            entries = this.entries;

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            entry.AcquireLock();
            entry.Value = value;
            entry.ReleaseLock(HasValueState);
            break;
        }
    }

    /// <summary>
    /// Associates the value with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value to set.</param>
    public void Set<TKey>(TValue value)
        => Set(ITypeMap<TValue>.GetIndex<TKey>(), value);

    /// <summary>
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey<TKey>()
    {
        return ContainsKey(entries, ITypeMap<TValue>.GetIndex<TKey>());

        static bool ContainsKey(Entry[] entries, int index)
            => (uint)index < (uint)entries.Length && Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).HasValue;
    }

    private TValue GetOrAdd(int index, TValue value, out bool added)
    {
        for (Entry[] entries; ;)
        {
            entries = this.entries;

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);

            if (added = entry.AcquireLock() is EmptyValueState)
            {
                entry.Value = value;
            }
            else
            {
                value = entry.Value!;
            }

            entry.ReleaseLock(HasValueState);
            return value;
        }
    }

    /// <summary>
    /// Adds a value to the map if the key does not already exist.
    /// Returns the new value, or the existing value if the key already exists.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <param name="added"><see langword="true"/> if the value is added; <see langword="false"/> if the value is already exist.</param>
    /// <returns>The existing value; or <paramref name="value"/> if added.</returns>
    public TValue GetOrAdd<TKey>(TValue value, out bool added)
        => GetOrAdd(ITypeMap<TValue>.GetIndex<TKey>(), value, out added);

    private bool AddOrUpdate(int index, TValue value)
    {
        for (Entry[] entries; ;)
        {
            entries = this.entries;

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);

            var added = entry.AcquireLock() is EmptyValueState;
            entry.Value = value;
            entry.ReleaseLock(HasValueState);

            return added;
        }
    }

    /// <summary>
    /// Adds a value to the map if the key does not already exist, or updates the existing value.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is added;
    /// <see langword="false"/> if the existing value is updated with <paramref name="value"/>.
    /// </returns>
    public bool AddOrUpdate<TKey>(TValue value)
        => AddOrUpdate(ITypeMap<TValue>.GetIndex<TKey>(), value);

    private bool Set(int index, TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
    {
        for (bool result; ;)
        {
            var entries = this.entries;

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);

            oldValue = (result = entry.AcquireLock() is HasValueState)
                ? entry.Value
                : default;

            entry.Value = newValue;
            entry.ReleaseLock(HasValueState);

            return result;
        }
    }

    /// <summary>
    /// Replaces the existing value with a new value.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="newValue">A new value.</param>
    /// <param name="oldValue">The replaced value.</param>
    /// <returns><see langword="true"/> if value is replaced; <see langword="false"/> if a new value is added without replacement.</returns>
    public bool Set<TKey>(TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
        => Set(ITypeMap<TValue>.GetIndex<TKey>(), newValue, out oldValue);

    /// <summary>
    /// Replaces the existing value with a new value.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="newValue">A new value.</param>
    /// <returns>The replaced value.</returns>
    [Obsolete("Use Set overload instead")]
    public Optional<TValue> Replace<TKey>(TValue newValue)
        => Set(ITypeMap<TValue>.GetIndex<TKey>(), newValue, out var oldValue) ? Optional.Some(oldValue!) : Optional.None<TValue>();

    private bool Remove(int index, [MaybeNullWhen(false)] out TValue value)
    {
        for (Entry[] entries; ;)
        {
            entries = this.entries;

            if (index >= entries.Length)
                break;

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            if (entry.TryAcquireLock(HasValueState))
            {
                value = entry.Value!;
                entry.Value = default;
                entry.ReleaseLock(EmptyValueState);
                return true;
            }

            break;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value of the removed element.</param>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<TKey>([MaybeNullWhen(false)] out TValue value)
        => Remove(ITypeMap<TValue>.GetIndex<TKey>(), out value);

    /// <summary>
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<TKey>() => Remove<TKey>(out _);

    private bool TryGetValue(int index, [MaybeNullWhen(false)] out TValue value)
    {
        for (Entry[] entries; ;)
        {
            entries = this.entries;

            if (index >= entries.Length)
                break;

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            if (entry.TryAcquireLock(HasValueState))
            {
                value = entry.Value!;
                entry.ReleaseLock(HasValueState);
                return true;
            }

            break;
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
    public void Clear()
    {
        foreach (var entry in entries)
        {
            entry.AcquireLock();
            entry.Value = default;
            entry.ReleaseLock(EmptyValueState);
        }
    }
}