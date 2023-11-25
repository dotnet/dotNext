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
        private volatile int state;
        internal TValue? Value;

        internal int AcquireLock()
        {
            int currentState;
            for (var spinner = new SpinWait(); ; spinner.SpinOnce())
            {
                currentState = state;

                if (currentState is not LockedState && Interlocked.CompareExchange(ref state, LockedState, currentState) == currentState)
                    return currentState;
            }
        }

        internal void ReleaseLock(int newState) => state = newState;

        internal bool HasValue
        {
            get
            {
                int currentState;

                for (var spinner = new SpinWait(); ; spinner.SpinOnce())
                {
                    currentState = state;

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
                currentState = state;

                if (currentState is LockedState)
                    continue;

                if (currentState != expectedState)
                    return false;

                if (Interlocked.CompareExchange(ref state, LockedState, currentState) == currentState)
                    return true;
            }
        }
    }

    private readonly object syncRoot;

    // Assuming that the map will not contain hunders or thousands for entries.
    // If so, we can keep the lock for each entry instead of buckets as in ConcurrentDictionaryMap.
    // As a result, we don't need the concurrency level. Also, we can modify different entries concurrently
    // and perform resizing in parallel with read/write of individual entry
    private Entry[] entries;

    /// <summary>
    /// Initializes a new map.
    /// </summary>
    /// <param name="capacity">The initial capacity of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
    public ConcurrentTypeMap(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Span.Initialize<Entry>(entries = capacity is 0 ? [] : new Entry[capacity]);
        syncRoot = new();
    }

    /// <summary>
    /// Initializes a new map of recommended capacity.
    /// </summary>
    public ConcurrentTypeMap()
    {
        Span.Initialize<Entry>(entries = new Entry[ITypeMap.RecommendedCapacity]);
        syncRoot = new();
    }

    private void Resize(Entry[] entries)
    {
        lock (syncRoot)
        {
            // make sure nobody resized the table while we were waiting for the lock
            if (!ReferenceEquals(entries, this.entries)) // read barrier is provided by monitor lock
                return;

            // do resize
            var firstUnitialized = entries.Length;
            Array.Resize(ref entries, ITypeMap.RecommendedCapacity);

            // initializes the rest of the array
            entries.AsSpan(firstUnitialized).Initialize();

            // commit resized storage
            this.entries = entries; // write barrier is provided by monitor lock
        }
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
            entries = Volatile.Read(ref this.entries);

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
        => TryAdd(ITypeMap.GetIndex<TKey>(), value);

    private void Set(int index, TValue value)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

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
        => Set(ITypeMap.GetIndex<TKey>(), value);

    /// <summary>
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey<TKey>()
    {
        return ContainsKey(Volatile.Read(ref entries), ITypeMap.GetIndex<TKey>());

        static bool ContainsKey(Entry[] entries, int index)
            => (uint)index < (uint)entries.Length && Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).HasValue;
    }

    private TValue GetOrAdd(int index, TValue value, out bool added)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

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
        => GetOrAdd(ITypeMap.GetIndex<TKey>(), value, out added);

    private bool AddOrUpdate(int index, TValue value)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

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
        => AddOrUpdate(ITypeMap.GetIndex<TKey>(), value);

    private bool Set(int index, TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
    {
        for (bool result; ;)
        {
            var entries = Volatile.Read(ref this.entries);

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
        => Set(ITypeMap.GetIndex<TKey>(), newValue, out oldValue);

    private bool Remove(int index, [MaybeNullWhen(false)] out TValue value)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

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
        => Remove(ITypeMap.GetIndex<TKey>(), out value);

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
            entries = Volatile.Read(ref this.entries);

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
        => TryGetValue(ITypeMap.GetIndex<TKey>(), out value);

    /// <summary>
    /// Removes all elements from this map.
    /// </summary>
    public void Clear()
    {
        foreach (var entry in Volatile.Read(ref entries))
        {
            entry.AcquireLock();
            entry.Value = default;
            entry.ReleaseLock(EmptyValueState);
        }
    }
}

/// <summary>
/// Represents thread-safe implementation of <see cref="ITypeMap"/> interface.
/// </summary>
public class ConcurrentTypeMap : ITypeMap
{
    private sealed class Entry
    {
        internal volatile object? Value;

        internal bool TrySet(object newValue)
            => Interlocked.CompareExchange(ref Value, newValue, null) is null;

        internal object? Unset() => Interlocked.Exchange(ref Value, null);

        internal object TryUpdate(object value, out bool updated)
        {
            var result = Interlocked.CompareExchange(ref Value, value, null);
            if (result is null)
            {
                result = value;
                updated = true;
            }
            else
            {
                updated = false;
            }

            return result;
        }

        internal object? Set(object newValue) => Interlocked.Exchange(ref Value, newValue);
    }

    private readonly object syncRoot;
    private Entry[] entries;

    /// <summary>
    /// Initializes a new empty set.
    /// </summary>
    /// <param name="capacity">The initial capacity of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
    public ConcurrentTypeMap(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Span.Initialize<Entry>(entries = capacity is 0 ? [] : new Entry[capacity]);
        syncRoot = new();
    }

    /// <summary>
    /// Initializes a new empty set.
    /// </summary>
    public ConcurrentTypeMap()
    {
        Span.Initialize<Entry>(entries = new Entry[ITypeMap.RecommendedCapacity]);
        syncRoot = new();
    }

    private void Resize(Entry[] entries)
    {
        lock (syncRoot)
        {
            // make sure nobody resized the table while we were waiting for the lock
            if (!ReferenceEquals(entries, this.entries)) // read barrier is provided by monitor lock
                return;

            // do resize
            var firstUnitialized = entries.Length;
            Array.Resize(ref entries, ITypeMap.RecommendedCapacity);

            // initializes the rest of the array
            entries.AsSpan(firstUnitialized).Initialize();

            // commit resized storage
            this.entries = entries; // write barrier is provided by monitor lock
        }
    }

    private bool TryAdd(int index, object value)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).TrySet(value);
        }
    }

    /// <summary>
    /// Attempts to add a new value to this set.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be added.</param>
    /// <returns><see langword="true"/> if the value is added; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd<T>([DisallowNull] T value)
        => TryAdd(ITypeMap.GetIndex<T>(), value);

    /// <inheritdoc />
    void ITypeMap.Add<T>([DisallowNull] T value)
    {
        if (!TryAdd(value))
            throw new GenericArgumentException<T>(ExceptionMessages.KeyAlreadyExists);
    }

    private void Set(int index, object value)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).Value = value;
            break;
        }
    }

    /// <inheritdoc cref="ITypeMap.Set{T}(T)"/>
    public void Set<T>([DisallowNull] T value)
        => Set(ITypeMap.GetIndex<T>(), value);

    /// <inheritdoc cref="IReadOnlyTypeMap.Contains{T}"/>
    public bool Contains<T>()
    {
        return ContainsKey(Volatile.Read(ref entries), ITypeMap.GetIndex<T>());

        static bool ContainsKey(Entry[] entries, int index)
            => (uint)index < (uint)entries.Length && Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).Value is T;
    }

    private object GetOrAdd(int index, object value, out bool added)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).TryUpdate(value, out added);
        }
    }

    /// <summary>
    /// Attempts to add a new value or returns existing value, atomitcally.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be added.</param>
    /// <param name="added"><see langword="true"/> if the value is added; <see langword="false"/> if the value is already exist.</param>
    /// <returns>The existing value; or <paramref name="value"/> if added.</returns>
    public T GetOrAdd<T>([DisallowNull] T value, out bool added)
        => (T)GetOrAdd(ITypeMap.GetIndex<T>(), value, out added);

    private bool AddOrUpdate(int index, object value)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).Set(value) is null;
        }
    }

    /// <summary>
    /// Adds a new value or updates existing one, atomically.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be set.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is added;
    /// <see langword="false"/> if the existing value is updated with <paramref name="value"/>.
    /// </returns>
    public bool AddOrUpdate<T>([DisallowNull] T value)
        => AddOrUpdate(ITypeMap.GetIndex<T>(), value);

    private bool Set<T>(int index, object newValue, [NotNullWhen(true)] out T? oldValue)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            var previous = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).Set(newValue);
            if (previous is T)
            {
                oldValue = (T)previous;
                return true;
            }

            oldValue = default;
            return false;
        }
    }

    /// <inheritdoc cref="ITypeMap.Set{T}(T, out T)"/>
    public bool Set<T>([DisallowNull] T newValue, [NotNullWhen(true)] out T? oldValue)
        => Set(ITypeMap.GetIndex<T>(), newValue, out oldValue);

    private bool Remove(int index)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).Unset() is not null;
        }
    }

    /// <inheritdoc cref="ITypeMap.Remove{T}()"/>
    public bool Remove<T>() => Remove(ITypeMap.GetIndex<T>());

    private bool Remove<T>(int index, [NotNullWhen(true)] out T? value)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            var previous = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).Unset();
            if (previous is T)
            {
                value = (T)previous;
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <inheritdoc cref="ITypeMap.Remove{T}(out T)"/>
    public bool Remove<T>([NotNullWhen(true)] out T? value)
        => Remove(ITypeMap.GetIndex<T>(), out value);

    /// <inheritdoc cref="ITypeMap.Clear()"/>
    public void Clear()
    {
        foreach (var entry in Volatile.Read(ref entries))
        {
            entry.Value = null;
        }
    }

    private bool TryGetValue<T>(int index, [NotNullWhen(true)] out T? value)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                Resize(entries);
                continue;
            }

            var current = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index).Value;
            if (current is T)
            {
                value = (T)current;
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <inheritdoc cref="IReadOnlyTypeMap.TryGetValue{T}(out T)"/>
    public bool TryGetValue<T>([NotNullWhen(true)] out T? value)
        => TryGetValue(ITypeMap.GetIndex<T>(), out value);
}