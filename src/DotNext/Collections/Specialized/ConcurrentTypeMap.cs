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
    
    private Entry GetOrAddEntry(int index)
    {
        var entriesCopy = Volatile.Read(in entries);
        entriesCopy = index < entriesCopy.Length ? entriesCopy : EnsureCapacity(entriesCopy, index);
        return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entriesCopy), index);
    }

    private Entry[] EnsureCapacity(Entry[] entriesCopy, int index)
    {
        do
        {
            if (UseReferenceEntry)
            {
                Resize(Unsafe.As<ReferenceEntry[]>(entriesCopy));
            }
            else
            {
                Resize(Unsafe.As<GenericEntry[]>(entriesCopy));
            }

            entriesCopy = Volatile.Read(in entries);
        } while (index >= entriesCopy.Length);

        return entriesCopy;
    }

    /// <summary>
    /// Attempts to associate a value with the type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <returns><see langword="true"/> if the value is added; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd<TKey>(TValue value)
        where TKey : allows ref struct
    {
        var entry = GetOrAddEntry(TypeSlot<TKey>.Index);
        return UseReferenceEntry
            ? Unsafe.As<ReferenceEntry>(entry).TrySet(value)
            : Unsafe.As<GenericEntry>(entry).TrySet(value);
    }

    /// <summary>
    /// Associates the value with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value to set.</param>
    public void Set<TKey>(TValue value)
        where TKey : allows ref struct
    {
        var entry = GetOrAddEntry(TypeSlot<TKey>.Index);
        if (UseReferenceEntry)
        {
            Unsafe.As<ReferenceEntry>(entry).Set(value);
        }
        else
        {
            Unsafe.As<GenericEntry>(entry).Set(value);
        }
    }

    /// <summary>
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey<TKey>()
        where TKey : allows ref struct
    {
        return this[TypeSlot<TKey>.Index] is { } entry && HasValue(entry);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool HasValue(Entry entry)
            => UseReferenceEntry ? Unsafe.As<ReferenceEntry>(entry).HasValue : Unsafe.As<GenericEntry>(entry).HasValue;
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
        where TKey : allows ref struct
    {
        var entry = GetOrAddEntry(TypeSlot<TKey>.Index);
        return UseReferenceEntry
            ? Unsafe.As<ReferenceEntry>(entry).GetOrSet(value, out added)
            : Unsafe.As<GenericEntry>(entry).GetOrSet(value, out added);
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
        where TKey : allows ref struct
    {
        var entry = GetOrAddEntry(TypeSlot<TKey>.Index);
        return UseReferenceEntry
            ? Unsafe.As<ReferenceEntry>(entry).SetOrUpdate(value)
            : Unsafe.As<GenericEntry>(entry).SetOrUpdate(value);
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
        var entry = GetOrAddEntry(TypeSlot<TKey>.Index);
        return UseReferenceEntry
            ? Unsafe.As<ReferenceEntry>(entry).Set(newValue, out oldValue)
            : Unsafe.As<GenericEntry>(entry).Set(newValue, out oldValue);
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
        if (this[TypeSlot<TKey>.Index] is { } entry)
        {
            return UseReferenceEntry
                ? Unsafe.As<ReferenceEntry>(entry).Unset(out value)
                : Unsafe.As<GenericEntry>(entry).Unset(out value);
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<TKey>()
        where TKey : allows ref struct
        => Remove<TKey>(out _);

    /// <summary>
    /// Attempts to get the value associated with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue<TKey>([MaybeNullWhen(false)] out TValue value)
        where TKey : allows ref struct
    {
        if (this[TypeSlot<TKey>.Index] is { } entry)
        {
            return UseReferenceEntry
                ? Unsafe.As<ReferenceEntry>(entry).TryGet(out value)
                : Unsafe.As<GenericEntry>(entry).TryGet(out value);
        }

        value = default;
        return false;
    }
    
    private Entry? this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var entriesCopy = Volatile.Read(in entries);

            return index < entriesCopy.Length
                ? Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entriesCopy), index)
                : null;
        }
    }

    /// <summary>
    /// Removes all elements from this map.
    /// </summary>
    public void Clear()
    {
        var entriesCopy = Volatile.Read(in entries);
        if (UseReferenceEntry)
        {
            Array.ForEach(Unsafe.As<ReferenceEntry[]>(entriesCopy), static entry => entry.Unset());
        }
        else
        {
            Array.ForEach(Unsafe.As<GenericEntry[]>(entriesCopy), static entry => entry.Unset());
        }
    }

    private static bool UseReferenceEntry
        => RuntimeHelpers.IsReferenceOrContainsReferences<TValue>() && Unsafe.SizeOf<TValue>() == nint.Size;

    internal abstract class Entry
    {
        public abstract bool HasValue { get; }

        public abstract bool TrySet(TValue newValue);

        public abstract void Set(TValue newValue);

        public abstract bool Set(TValue newValue, [MaybeNullWhen(false)] out TValue oldValue);

        public abstract TValue GetOrSet(TValue newValue, out bool isSet);

        public abstract bool SetOrUpdate(TValue newValue);

        public abstract bool Unset([MaybeNullWhen(false)] out TValue oldValue);

        public abstract bool TryGet([MaybeNullWhen(false)] out TValue existingValue);

        public abstract void Unset();
    }

    private sealed class ReferenceEntry : Entry
    {
        private volatile object value = Sentinel.Instance;

        public override bool HasValue => !IsEmpty;
        
        private bool IsEmpty => ReferenceEquals(value, Sentinel.Instance);

        public override bool TrySet(TValue newValue)
            => IsEmpty && ReferenceEquals(Interlocked.CompareExchange(
                ref value,
                Unsafe.As<TValue, object>(ref newValue),
                Sentinel.Instance), Sentinel.Instance);

        public override void Set(TValue newValue)
            => value = Unsafe.As<TValue, object>(ref newValue);

        public override TValue GetOrSet(TValue newValue, out bool isSet)
        {
            // Perf: GetOrAdd can be implemented by simple CompareExchange. In this case, the cost of GET is the same as of ADD.
            // However, ADD is more unlikely than GET, since the element once added it becomes available for read.
            // Therefore, change the symmetry between GET and ADD overhead as follows:
            // 1. Make GET cheaper
            // 2. Make ADD more expensive
            // So, GET can be done with simple Read Fence. If it's successful, CompareExchange is not needed.
            var result = value;
            if (ReferenceEquals(result, Sentinel.Instance))
            {
                result = Interlocked.CompareExchange(ref value, Unsafe.As<TValue, object>(ref newValue), Sentinel.Instance);

                if (ReferenceEquals(result, Sentinel.Instance))
                {
                    isSet = true;
                    return newValue;
                }
            }

            isSet = false;
            return Unsafe.As<object, TValue>(ref result);
        }

        public override bool SetOrUpdate(TValue newValue)
        {
            var result = Interlocked.Exchange(ref value, Unsafe.As<TValue, object>(ref newValue));
            return ReferenceEquals(result, Sentinel.Instance);
        }

        public override bool Set(TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
        {
            var result = Interlocked.Exchange(ref value, Unsafe.As<TValue, object>(ref newValue));
            var modified = !ReferenceEquals(result, Sentinel.Instance);
            oldValue = modified
                ? Unsafe.As<object, TValue>(ref result)
                : default;

            return modified;
        }

        public override bool Unset([MaybeNullWhen(false)] out TValue oldValue)
        {
            var removed = HasValue;
            if (removed)
            {
                var result = Interlocked.Exchange(ref value, Sentinel.Instance);
                removed = !ReferenceEquals(result, Sentinel.Instance);
                if (removed)
                {
                    oldValue = Unsafe.As<object, TValue>(ref result);
                    goto exit;
                }
            }

            oldValue = default;

            exit:
            return removed;
        }

        public override bool TryGet([MaybeNullWhen(false)] out TValue existingValue)
        {
            var valueCopy = value;
            var hasValue = !ReferenceEquals(valueCopy, Sentinel.Instance);
            existingValue = hasValue
                ? Unsafe.As<object, TValue>(ref valueCopy)
                : default;

            return hasValue;
        }

        public override void Unset()
        {
            if (HasValue)
            {
                Interlocked.Exchange(ref value, Sentinel.Instance);
            }
        }
    }

    private sealed class GenericEntry : Entry
    {
        private volatile EntryState state;
        private TValue? value;

        public override bool TrySet(TValue newValue)
        {
            if (TryAcquireLock(EntryState.Empty))
            {
                value = newValue;
                ReleaseLock(EntryState.HasValue);
                return true;
            }

            return false;
        }

        public override void Set(TValue newValue)
        {
            AcquireLock();
            value = newValue;
            ReleaseLock(EntryState.HasValue);
        }

        public override TValue GetOrSet(TValue newValue, out bool isSet)
        {
            if (isSet = AcquireLock() is EntryState.Empty)
            {
                value = newValue;
            }
            else
            {
                newValue = value!;
            }

            ReleaseLock(EntryState.HasValue);
            return newValue;
        }

        public override bool SetOrUpdate(TValue newValue)
        {
            var added = AcquireLock() is EntryState.Empty;
            value = newValue;
            ReleaseLock(EntryState.HasValue);
            return added;
        }

        public override bool Set(TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
        {
            var modified = AcquireLock() is EntryState.HasValue;
            oldValue = modified
                ? value
                : default;

            value = newValue;
            ReleaseLock(EntryState.HasValue);
            return modified;
        }

        public override bool Unset([MaybeNullWhen(false)] out TValue oldValue)
        {
            if (TryAcquireLock(EntryState.HasValue))
            {
                oldValue = value!;
                value = default;
                ReleaseLock(EntryState.Empty);
                return true;
            }

            oldValue = default;
            return false;
        }

        public override bool TryGet([MaybeNullWhen(false)] out TValue existingValue)
        {
            var valueTaken = TryAcquireLock(EntryState.HasValue);
            if (valueTaken)
            {
                existingValue = value!;
                ReleaseLock(EntryState.HasValue);
            }
            else
            {
                existingValue = default;
            }

            return valueTaken;
        }

        public override void Unset()
        {
            AcquireLock();
            value = default;
            ReleaseLock(EntryState.Empty);
        }

        private EntryState AcquireLock()
        {
            var oldState = Interlocked.Exchange(ref state, EntryState.Locked);
            return oldState is EntryState.Locked ? AcquireLockContention() : oldState;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private EntryState AcquireLockContention()
        {
            EntryState currentState;
            do
            {
                currentState = Interlocked.Exchange(ref state, EntryState.Locked);
            } while (currentState is EntryState.Locked);

            return currentState;
        }

        private void ReleaseLock([ConstantExpected] EntryState newState) => state = newState;

        public override bool HasValue
        {
            get
            {
                EntryState currentState;
                do
                {
                    currentState = state;
                } while (currentState is EntryState.Locked);

                return currentState is EntryState.HasValue;
            }
        }

        private bool TryAcquireLock([ConstantExpected] EntryState expectedState)
        {
            var currentState = Interlocked.CompareExchange(ref state, EntryState.Locked, expectedState);
            return currentState == expectedState || TryAcquireLockContention(expectedState);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryAcquireLockContention(EntryState expectedState)
        {
            EntryState currentState;
            do
            {
                currentState = Interlocked.CompareExchange(ref state, EntryState.Locked, expectedState);
            } while (currentState is EntryState.Locked);

            return currentState == expectedState;
        }
    }
    
    private enum EntryState
    {
        Empty = 0,
        Locked = 1,
        HasValue = 2,
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

        internal object TrySet(object value, out bool isSet)
        {
            var valueCopy = Value;
            if (valueCopy is null)
            {
                valueCopy = Interlocked.CompareExchange(ref Value, value, null);

                if (valueCopy is null)
                {
                    isSet = true;
                    return value;
                }
            }

            isSet = false;
            return valueCopy;
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

    private void Resize(Entry[] entriesCopy)
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
    
    private Entry GetOrAddEntry(int index)
    {
        var entriesCopy = Volatile.Read(in entries);
        entriesCopy = index < entriesCopy.Length ? entriesCopy : EnsureCapacity(entriesCopy, index);
        return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entriesCopy), index);
    }

    private Entry[] EnsureCapacity(Entry[] entriesCopy, int index)
    {
        do
        {
            Resize(entriesCopy);
            entriesCopy = Volatile.Read(in entries);
        } while (index >= entriesCopy.Length);

        return entriesCopy;
    }
    
    private Entry? this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var entriesCopy = Volatile.Read(in entries);

            return index < entriesCopy.Length
                ? Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entriesCopy), index)
                : null;
        }
    }

    /// <summary>
    /// Attempts to add a new value to this set.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be added.</param>
    /// <returns><see langword="true"/> if the value is added; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd<T>([DisallowNull] T value)
        => GetOrAddEntry(TypeSlot<T>.Index).TrySet(value);

    /// <inheritdoc />
    void ITypeMap.Add<T>([DisallowNull] T value)
    {
        if (!TryAdd(value))
            throw new GenericArgumentException<T>(ExceptionMessages.KeyAlreadyExists);
    }

    /// <inheritdoc cref="ITypeMap.Set{T}(T)"/>
    public void Set<T>([DisallowNull] T value)
        => GetOrAddEntry(TypeSlot<T>.Index).Value = value;

    /// <inheritdoc cref="IReadOnlyTypeMap.Contains{T}"/>
    public bool Contains<T>()
        => this[TypeSlot<T>.Index]?.Value is T;

    /// <summary>
    /// Attempts to add a new value or returns existing value, atomically.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be added.</param>
    /// <param name="added"><see langword="true"/> if the value is added; <see langword="false"/> if the value is already exist.</param>
    /// <returns>The existing value; or <paramref name="value"/> if added.</returns>
    public T GetOrAdd<T>([DisallowNull] T value, out bool added)
        => (T)GetOrAddEntry(TypeSlot<T>.Index).TrySet(value, out added);

    /// <summary>
    /// Adds a new value or updates existing one, atomically.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be set.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is added;
    /// <see langword="false"/> if the existing value is updated with <paramref name="value"/>.
    /// </returns>
    public bool AddOrUpdate<T>([DisallowNull] T value)
        => GetOrAddEntry(TypeSlot<T>.Index).Set(value) is null;

    /// <inheritdoc cref="ITypeMap.Set{T}(T, out T)"/>
    public bool Set<T>([DisallowNull] T newValue, [NotNullWhen(true)] out T? oldValue)
    {
        if (GetOrAddEntry(TypeSlot<T>.Index).Set(newValue) is T previous)
        {
            oldValue = previous;
            return true;
        }

        oldValue = default;
        return false;
    }

    /// <inheritdoc cref="ITypeMap.Remove{T}()"/>
    public bool Remove<T>() => this[TypeSlot<T>.Index]?.Unset() is T;

    /// <inheritdoc cref="ITypeMap.Remove{T}(out T)"/>
    public bool Remove<T>([NotNullWhen(true)] out T? value)
    {
        if (this[TypeSlot<T>.Index]?.Unset() is T previous)
        {
            value = previous;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc cref="ITypeMap.Clear()"/>
    public void Clear()
    {
        foreach (var entry in Volatile.Read(in entries))
        {
            entry.Value = null;
        }
    }

    /// <inheritdoc cref="IReadOnlyTypeMap.TryGetValue{T}(out T)"/>
    public bool TryGetValue<T>([NotNullWhen(true)] out T? value)
    {
        if (this[TypeSlot<T>.Index]?.Value is T result)
        {
            value = result;
            return true;
        }

        value = default;
        return false;
    }
}