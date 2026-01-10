using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Specialized;

/// <summary>
/// Represents thread-safe implementation of <see cref="ITypeMap{TValue}"/> interface.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public partial class ConcurrentTypeMap<TValue> : ITypeMap<TValue>
{
    private readonly Lock syncRoot;

    // Assuming that the map will not contain hundreds or thousands for entries.
    // If so, we can keep the lock for each entry instead of buckets as in ConcurrentDictionaryMap.
    // As a result, we don't need the concurrency level. Also, we can modify different entries concurrently
    // and perform resizing in parallel with read/write of individual entry.
    // For the entry of reference type, we don't even need the lock per entry, because Sentinel and CAS operations
    // can protect the atomic read/write.
    private Entry[] entries;

    /// <summary>
    /// Initializes a new map.
    /// </summary>
    /// <param name="capacity">The initial capacity of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
    public ConcurrentTypeMap(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        entries = UseReferenceEntry
            ? CreateEntries<ReferenceEntry>(capacity)
            : CreateEntries<GenericEntry>(capacity);
        syncRoot = new();

        static TEntry[] CreateEntries<TEntry>(int capacity)
            where TEntry : Entry, new()
        {
            var array = capacity is 0 ? [] : new TEntry[capacity];
            Span.Initialize(array);
            return array;
        }
    }

    /// <summary>
    /// Initializes a new map of recommended capacity.
    /// </summary>
    public ConcurrentTypeMap()
        : this(ITypeMap.RecommendedCapacity)
    {
    }

    private void Resize<TEntry>(TEntry[] entriesCopy)
        where TEntry : Entry, new()
    {
        lock (syncRoot)
        {
            // make sure nobody resized the table while we were waiting for the lock
            if (!ReferenceEquals(entriesCopy, entries)) // read barrier is provided by monitor lock
                return;

            // do resize
            var firstUninitialized = entriesCopy.Length;
            Array.Resize(ref entriesCopy, ITypeMap.RecommendedCapacity);

            // initializes the rest of the array
            entriesCopy.AsSpan(firstUninitialized).Initialize();

            // commit resized storage
            entries = entriesCopy; // write barrier is provided by monitor lock
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
        for (Entry[] entries;;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                if (UseReferenceEntry)
                {
                    Resize(Unsafe.As<ReferenceEntry[]>(entries));
                }
                else
                {
                    Resize(Unsafe.As<GenericEntry[]>(entries));
                }

                continue;
            }

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            return UseReferenceEntry
                ? Unsafe.As<ReferenceEntry>(entry).TryAdd(value)
                : Unsafe.As<GenericEntry>(entry).TryAdd(value);
        }
    }

    /// <summary>
    /// Attempts to associate a value with the type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <returns><see langword="true"/> if the value is added; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd<TKey>(TValue value)
        where TKey : allows ref struct
        => TryAdd(TypeSlot<TKey>.Index, value);

    private void Set(int index, TValue value)
    {
        for (Entry[] entries; ;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                if (UseReferenceEntry)
                {
                    Resize(Unsafe.As<ReferenceEntry[]>(entries));
                }
                else
                {
                    Resize(Unsafe.As<GenericEntry[]>(entries));
                }

                continue;
            }

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            if (UseReferenceEntry)
            {
                Unsafe.As<ReferenceEntry>(entry).TryAdd(value);
            }
            else
            {
                Unsafe.As<GenericEntry>(entry).TryAdd(value);
            }
            
            break;
        }
    }

    /// <summary>
    /// Associates the value with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value to set.</param>
    public void Set<TKey>(TValue value)
        where TKey : allows ref struct
        => Set(TypeSlot<TKey>.Index, value);

    /// <summary>
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey<TKey>()
        where TKey : allows ref struct
    {
        return ContainsKey(Volatile.Read(ref entries), TypeSlot<TKey>.Index);

        static bool ContainsKey(Entry[] entries, int index)
            => (uint)index < (uint)entries.Length && HasValue(Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool HasValue(Entry entry)
            => UseReferenceEntry ? Unsafe.As<ReferenceEntry>(entry).HasValue : Unsafe.As<GenericEntry>(entry).HasValue;
    }

    private TValue GetOrAdd(int index, TValue value, out bool added)
    {
        for (Entry[] entries;;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                if (UseReferenceEntry)
                {
                    Resize(Unsafe.As<ReferenceEntry[]>(entries));
                }
                else
                {
                    Resize(Unsafe.As<GenericEntry[]>(entries));
                }

                continue;
            }

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            return UseReferenceEntry
                ? Unsafe.As<ReferenceEntry>(entry).GetOrAdd(value, out added)
                : Unsafe.As<GenericEntry>(entry).GetOrAdd(value, out added);
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
        => GetOrAdd(TypeSlot<TKey>.Index, value, out added);

    private bool AddOrUpdate(int index, TValue value)
    {
        for (Entry[] entries;;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                if (UseReferenceEntry)
                {
                    Resize(Unsafe.As<ReferenceEntry[]>(entries));
                }
                else
                {
                    Resize(Unsafe.As<GenericEntry[]>(entries));
                }

                continue;
            }

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            return UseReferenceEntry
                ? Unsafe.As<ReferenceEntry>(entry).AddOrUpdate(value)
                : Unsafe.As<GenericEntry>(entry).AddOrUpdate(value);
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
        => AddOrUpdate(TypeSlot<TKey>.Index, value);

    private bool Set(int index, TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
    {
        for (Entry[] entries;;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
            {
                if (UseReferenceEntry)
                {
                    Resize(Unsafe.As<ReferenceEntry[]>(entries));
                }
                else
                {
                    Resize(Unsafe.As<GenericEntry[]>(entries));
                }

                continue;
            }

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            return UseReferenceEntry
                ? Unsafe.As<ReferenceEntry>(entry).Set(newValue, out oldValue)
                : Unsafe.As<GenericEntry>(entry).Set(newValue, out oldValue);
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
        where TKey : allows ref struct
        => Set(TypeSlot<TKey>.Index, newValue, out oldValue);

    private bool Remove(int index, [MaybeNullWhen(false)] out TValue value)
    {
        for (Entry[] entries;;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
                break;

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            return UseReferenceEntry
                ? Unsafe.As<ReferenceEntry>(entry).Remove(out value)
                : Unsafe.As<GenericEntry>(entry).Remove(out value);
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
        where TKey : allows ref struct
        => Remove(TypeSlot<TKey>.Index, out value);

    /// <summary>
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<TKey>()
        where TKey : allows ref struct
        => Remove<TKey>(out _);

    private bool TryGetValue(int index, [MaybeNullWhen(false)] out TValue value)
    {
        for (Entry[] entries;;)
        {
            entries = Volatile.Read(ref this.entries);

            if (index >= entries.Length)
                break;

            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), index);
            return UseReferenceEntry
                ? Unsafe.As<ReferenceEntry>(entry).TryGetValue(out value)
                : Unsafe.As<GenericEntry>(entry).TryGetValue(out value);
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
        where TKey : allows ref struct
        => TryGetValue(TypeSlot<TKey>.Index, out value);

    /// <summary>
    /// Removes all elements from this map.
    /// </summary>
    public void Clear()
    {
        var entries = Volatile.Read(ref this.entries);
        if (UseReferenceEntry)
        {
            Array.ForEach(Unsafe.As<ReferenceEntry[]>(entries), static entry => entry.Clear());
        }
        else
        {
            Array.ForEach(Unsafe.As<GenericEntry[]>(entries), static entry => entry.Clear());
        }
    }

    private static bool UseReferenceEntry
        => RuntimeHelpers.IsReferenceOrContainsReferences<TValue>() && Unsafe.SizeOf<TValue>() == nint.Size;

    internal abstract class Entry
    {
        public abstract bool HasValue { get; }

        public abstract bool TryAdd(TValue newValue);

        public abstract void Set(TValue newValue);

        public abstract bool Set(TValue newValue, [MaybeNullWhen(false)] out TValue oldValue);

        public abstract TValue GetOrAdd(TValue newValue, out bool added);

        public abstract bool AddOrUpdate(TValue newValue);

        public abstract bool Remove([MaybeNullWhen(false)] out TValue oldValue);

        public abstract bool TryGetValue([MaybeNullWhen(false)] out TValue existingValue);

        public abstract void Clear();
    }

    private sealed class ReferenceEntry : Entry
    {
        private volatile object value = Sentinel.Instance;

        public override bool HasValue => !ReferenceEquals(value, Sentinel.Instance);

        public override bool TryAdd(TValue newValue)
            => Interlocked.CompareExchange(
                ref this.value,
                Sentinel.Instance,
                Unsafe.As<TValue, object>(ref newValue)) == Sentinel.Instance;

        public override void Set(TValue newValue)
            => this.value = Unsafe.As<TValue, object>(ref newValue);

        public override TValue GetOrAdd(TValue newValue, out bool added)
        {
            var result = Interlocked.CompareExchange(ref this.value, Sentinel.Instance, Unsafe.As<TValue, object>(ref newValue));
            return (added = ReferenceEquals(result, Sentinel.Instance))
                ? newValue
                : Unsafe.As<object, TValue>(ref result);
        }

        public override bool AddOrUpdate(TValue newValue)
        {
            var result = Interlocked.Exchange(ref this.value, Unsafe.As<TValue, object>(ref newValue));
            return ReferenceEquals(result, Sentinel.Instance);
        }

        public override bool Set(TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
        {
            var result = Interlocked.Exchange(ref this.value, Unsafe.As<TValue, object>(ref newValue));
            bool modified;
            oldValue = (modified = !ReferenceEquals(result, Sentinel.Instance))
                ? Unsafe.As<object, TValue>(ref result)
                : default;

            return modified;
        }

        public override bool Remove([MaybeNullWhen(false)] out TValue oldValue)
        {
            var result = Interlocked.Exchange(ref this.value, Sentinel.Instance);
            bool removed;
            oldValue = (removed = !ReferenceEquals(result, Sentinel.Instance))
                ? Unsafe.As<object, TValue>(ref result)
                : default;

            return removed;
        }

        public override bool TryGetValue([MaybeNullWhen(false)] out TValue existingValue)
        {
            var valueCopy = this.value;
            bool hasValue;
            existingValue = (hasValue = !ReferenceEquals(valueCopy, Sentinel.Instance))
                ? Unsafe.As<object, TValue>(ref valueCopy)
                : default;

            return hasValue;
        }

        public override void Clear() => Interlocked.Exchange(ref value, Sentinel.Instance);
    }

    private sealed class GenericEntry : Entry
    {
        private const int EmptyValueState = 0;
        private const int LockedState = 1;
        private const int HasValueState = 2;
        
        private volatile int state;
        private TValue? value;

        public override bool TryAdd(TValue newValue)
        {
            if (TryAcquireLock(EmptyValueState))
            {
                value = newValue;
                ReleaseLock(HasValueState);
                return true;
            }

            return false;
        }

        public override void Set(TValue newValue)
        {
            AcquireLock();
            value = newValue;
            ReleaseLock(HasValueState);
        }

        public override TValue GetOrAdd(TValue newValue, out bool added)
        {
            if (added = AcquireLock() is EmptyValueState)
            {
                value = newValue;
            }
            else
            {
                newValue = value!;
            }

            ReleaseLock(HasValueState);
            return newValue;
        }

        public override bool AddOrUpdate(TValue newValue)
        {
            var added = AcquireLock() is EmptyValueState;
            value = newValue;
            ReleaseLock(HasValueState);
            return added;
        }

        public override bool Set(TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
        {
            bool modified;
            oldValue = (modified = AcquireLock() is HasValueState)
                ? value
                : default;

            value = newValue;
            ReleaseLock(HasValueState);
            return modified;
        }

        public override bool Remove([MaybeNullWhen(false)] out TValue oldValue)
        {
            if (TryAcquireLock(HasValueState))
            {
                oldValue = value!;
                value = default;
                ReleaseLock(EmptyValueState);
                return true;
            }

            oldValue = default;
            return false;
        }

        public override bool TryGetValue([MaybeNullWhen(false)] out TValue existingValue)
        {
            bool valueTaken;
            if (valueTaken = TryAcquireLock(HasValueState))
            {
                existingValue = value!;
                ReleaseLock(HasValueState);
            }
            else
            {
                existingValue = default;
            }

            return valueTaken;
        }

        public override void Clear()
        {
            AcquireLock();
            value = default;
            ReleaseLock(EmptyValueState);
        }

        private int AcquireLock()
        {
            for (var spinner = new SpinWait(); ; spinner.SpinOnce())
            {
                var currentState = state;

                if (currentState is not LockedState && Interlocked.CompareExchange(ref state, LockedState, currentState) == currentState)
                    return currentState;
            }
        }

        private void ReleaseLock(int newState) => state = newState;

        public override bool HasValue
        {
            get
            {
                for (var spinner = new SpinWait(); ; spinner.SpinOnce())
                {
                    var currentState = state;

                    if (currentState is LockedState)
                        continue;

                    return currentState is HasValueState;
                }
            }
        }

        private bool TryAcquireLock(int expectedState)
        {
            for (var spinner = new SpinWait(); ; spinner.SpinOnce())
            {
                var currentState = state;

                if (currentState is LockedState)
                    continue;

                if (currentState != expectedState)
                    return false;

                if (Interlocked.CompareExchange(ref state, LockedState, currentState) == currentState)
                    return true;
            }
        }
    }
}

/// <summary>
/// Represents thread-safe implementation of <see cref="ITypeMap"/> interface.
/// </summary>
public partial class ConcurrentTypeMap : ITypeMap
{
    internal sealed class Entry
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

    private readonly Lock syncRoot;
    private Entry[] entries;

    /// <summary>
    /// Initializes a new empty set.
    /// </summary>
    /// <param name="capacity">The initial capacity of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
    public ConcurrentTypeMap(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        Span.Initialize(entries = capacity is 0 ? [] : new Entry[capacity]);
        syncRoot = new();
    }

    /// <summary>
    /// Initializes a new empty set.
    /// </summary>
    public ConcurrentTypeMap()
        : this(ITypeMap.RecommendedCapacity)
    {
    }

    private void Resize(Entry[] entries)
    {
        lock (syncRoot)
        {
            // make sure nobody resized the table while we were waiting for the lock
            if (!ReferenceEquals(entries, this.entries)) // read barrier is provided by monitor lock
                return;

            // do resize
            var firstUninitialized = entries.Length;
            Array.Resize(ref entries, ITypeMap.RecommendedCapacity);

            // initializes the rest of the array
            entries.AsSpan(firstUninitialized).Initialize();

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
        => TryAdd(TypeSlot<T>.Index, value);

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
        => Set(TypeSlot<T>.Index, value);

    /// <inheritdoc cref="IReadOnlyTypeMap.Contains{T}"/>
    public bool Contains<T>()
    {
        return ContainsKey(Volatile.Read(ref entries), TypeSlot<T>.Index);

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
    /// Attempts to add a new value or returns existing value, atomically.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be added.</param>
    /// <param name="added"><see langword="true"/> if the value is added; <see langword="false"/> if the value is already exist.</param>
    /// <returns>The existing value; or <paramref name="value"/> if added.</returns>
    public T GetOrAdd<T>([DisallowNull] T value, out bool added)
        => (T)GetOrAdd(TypeSlot<T>.Index, value, out added);

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
        => AddOrUpdate(TypeSlot<T>.Index, value);

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
        => Set(TypeSlot<T>.Index, newValue, out oldValue);

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
    public bool Remove<T>() => Remove(TypeSlot<T>.Index);

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
        => Remove(TypeSlot<T>.Index, out value);

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
        => TryGetValue(TypeSlot<T>.Index, out value);
}