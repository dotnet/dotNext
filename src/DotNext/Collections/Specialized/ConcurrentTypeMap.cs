using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Collections.Specialized;

/// <summary>
/// Represents thread-safe implementation of <see cref="ITypeMap{TValue}"/> interface.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public class ConcurrentTypeMap<TValue> : ITypeMap<TValue>
{
    [StructLayout(LayoutKind.Auto)]
    private struct Entry
    {
        internal readonly object Lock = new();
        private bool hasValue;
        private TValue? value;

        internal readonly bool HasValue => hasValue;

        internal TValue? Value
        {
            readonly get => value;
            set
            {
                hasValue = true;
                this.value = value;
            }
        }

        internal readonly bool TryGetValue([MaybeNullWhen(false)]out TValue value)
        {
            value = this.value;
            return hasValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Clear()
        {
            hasValue = false;
            value = default;
        }
    }

    // Assuming that the map will not contain hunders or thousands for entries.
    // If so, we can keep the lock for each entry instead of buckets as in ConcurrentDictionaryMap.
    // As a result, we don't need the concurrent level
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

        var entries = capacity == 0 ? Array.Empty<Entry>() : new Entry[capacity];
        entries.Initialize();
        this.entries = entries;
    }

    /// <summary>
    /// Initializes a new map of recommended capacity.
    /// </summary>
    public ConcurrentTypeMap()
    {
        var entries = new Entry[ITypeMap<TValue>.RecommendedCapacity];
        entries.Initialize();
        this.entries = entries;
    }

    private void Resize(Entry[] snapshot)
    {
        var locksTaken = 0;

        // the thread that first obtains the first lock will be the one doing the resize operation
        Monitor.Enter(snapshot[0].Lock);
        try
        {
            locksTaken = 1;

            // make sure nobody resized the table while we were waiting for the first lock
            if (!ReferenceEquals(snapshot, entries))
                return;

            // acquire remaining locks
            for (var i = 1; i < snapshot.Length; i++, locksTaken++)
                Monitor.Enter(snapshot[i].Lock);

            // do resize
            Resize(ref snapshot);

            // commit resized storage
            entries = snapshot;
        }
        finally
        {
            // release locks starting from the last lock
            for (var i = locksTaken - 1; i >= 0; i--)
                Monitor.Exit(Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(snapshot), i).Lock);
        }

        static void Resize(ref Entry[] entries)
        {
            var firstUnitialized = entries.Length;
            Array.Resize(ref entries, ITypeMap<TValue>.RecommendedCapacity);

            // initializes the rest of the array
            for (var i = firstUnitialized; i < entries.Length; i++)
                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), i) = new();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Entry Get<TKey>(Entry[] entries)
    {
        Debug.Assert(ITypeMap<TValue>.GetIndex<TKey>() < entries.Length);

        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), ITypeMap<TValue>.GetIndex<TKey>());
    }

    /// <inheritdoc />
    void ITypeMap<TValue>.Add<TKey>(TValue value)
    {
        if (!TryAdd<TKey>(value))
            throw new GenericArgumentException<TKey>(ExceptionMessages.KeyAlreadyExists);
    }

    /// <summary>
    /// Attempts to associate a value with the type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <returns><see langword="true"/> if the value is added; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd<TKey>(TValue value)
    {
        var snapshot = entries;

        if (ITypeMap<TValue>.GetIndex<TKey>() >= entries.Length)
        {
            Resize(snapshot);
            snapshot = entries;
        }

        bool added;

    try_again:
        ref var entry = ref Get<TKey>(snapshot);

        lock (entry.Lock)
        {
            var tmp = entries;

            if (!ReferenceEquals(tmp, snapshot))
            {
                snapshot = tmp;
                goto try_again;
            }
            else if (entry.HasValue)
            {
                added = false;
            }
            else
            {
                added = true;
                entry.Value = value;
            }
        }

        return added;
    }

    /// <summary>
    /// Associates the value with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value to set.</param>
    public void Set<TKey>(TValue value)
    {
        var snapshot = entries;

        if (ITypeMap<TValue>.GetIndex<TKey>() >= entries.Length)
        {
            Resize(snapshot);
            snapshot = entries;
        }

    try_again:
        ref var entry = ref Get<TKey>(snapshot);

        lock (entry.Lock)
        {
            var tmp = entries;

            if (!ReferenceEquals(snapshot, tmp))
            {
                snapshot = tmp;
                goto try_again;
            }

            entry.Value = value;
        }
    }

    /// <summary>
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey<TKey>()
    {
        var snapshot = entries;
        return ITypeMap<TValue>.GetIndex<TKey>() < snapshot.Length && Get<TKey>(snapshot).HasValue;
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
    {
        var snapshot = entries;

        if (ITypeMap<TValue>.GetIndex<TKey>() >= entries.Length)
        {
            Resize(snapshot);
            snapshot = entries;
        }

    try_again:
        ref var entry = ref Get<TKey>(snapshot);

        lock (entry.Lock)
        {
            var tmp = entries;

            if (!ReferenceEquals(snapshot, tmp))
            {
                snapshot = tmp;
                goto try_again;
            }
            else if (entry.HasValue)
            {
                added = false;
                value = entry.Value!;
            }
            else
            {
                added = true;
                entry.Value = value;
            }
        }

        return value;
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
    {
        bool added;
        var snapshot = entries;

        if (ITypeMap<TValue>.GetIndex<TKey>() >= entries.Length)
        {
            Resize(snapshot);
            snapshot = entries;
        }

    try_again:
        ref var entry = ref Get<TKey>(snapshot);

        lock (entry.Lock)
        {
            var tmp = entries;

            if (!ReferenceEquals(snapshot, tmp))
            {
                snapshot = tmp;
                goto try_again;
            }

            added = !entry.HasValue;
            entry.Value = value;
        }

        return added;
    }

    /// <summary>
    /// Replaces the existing value with a new value.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">A new value.</param>
    /// <returns>The replaced value.</returns>
    public Optional<TValue> Replace<TKey>(TValue value)
    {
        Optional<TValue> result;

        var snapshot = entries;

        if (ITypeMap<TValue>.GetIndex<TKey>() >= entries.Length)
        {
            Resize(snapshot);
            snapshot = entries;
        }

    try_again:
        ref var entry = ref Get<TKey>(snapshot);

        lock (entry.Lock)
        {
            var tmp = entries;

            if (!ReferenceEquals(snapshot, tmp))
            {
                snapshot = tmp;
                goto try_again;
            }

            result = entry.HasValue ? entry.Value : Optional<TValue>.None;
            entry.Value = value;
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
    {
        bool result;
        var snapshot = entries;

        if (ITypeMap<TValue>.GetIndex<TKey>() >= entries.Length)
        {
            value = default;
            return false;
        }

    try_again:
        ref var entry = ref Get<TKey>(snapshot);

        lock (entry.Lock)
        {
            var tmp = entries;

            if (!ReferenceEquals(snapshot, tmp))
            {
                snapshot = tmp;
                goto try_again;
            }

            result = entry.TryGetValue(out value);
            entry.Clear();
        }

        return result;
    }

    /// <summary>
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<TKey>() => Remove<TKey>(out _);

    /// <summary>
    /// Attempts to get the value associated with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value associated with the type.</param>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue<TKey>([MaybeNullWhen(false)] out TValue value)
    {
        bool result;
        var snapshot = entries;

        if (ITypeMap<TValue>.GetIndex<TKey>() >= entries.Length)
        {
            value = default;
            return false;
        }

    try_again:
        ref var entry = ref Get<TKey>(snapshot);

        lock (entry.Lock)
        {
            var tmp = entries;

            if (!ReferenceEquals(snapshot, tmp))
            {
                snapshot = tmp;
                goto try_again;
            }

            result = entry.TryGetValue(out value);
        }

        return result;
    }

    /// <summary>
    /// Removes all elements from this map.
    /// </summary>
    public void Clear()
    {
        var locksTaken = 0;
        var snapshot = entries;

        Monitor.Enter(snapshot[0].Lock);
        try
        {
            locksTaken = 1;
            snapshot = entries;

            ref var entry = ref MemoryMarshal.GetArrayDataReference(snapshot);
            entry.Clear();

            // acquire remaining locks
            for (var i = 1; i < snapshot.Length; i++, locksTaken++)
            {
                entry = ref snapshot[i];
                Monitor.Enter(entry.Lock);
                entry.Clear();
            }
        }
        finally
        {
            // release locks starting from the last lock
            for (var i = locksTaken - 1; i >= 0; i--)
                Monitor.Exit(Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(snapshot), i).Lock);
        }
    }
}