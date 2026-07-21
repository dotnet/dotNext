using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Specialized;

using Concurrent;
using Threading;

/// <summary>
/// Represents thread-safe implementation of <see cref="ITypeMap{TValue}"/> interface.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public partial class ConcurrentTypeMap<TValue> : ITypeMap<TValue>
{
    private ConcurrentArray<Entry> entries;

    /// <summary>
    /// Initializes a new map.
    /// </summary>
    /// <param name="capacity">The initial capacity of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
    public ConcurrentTypeMap(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        entries = new()
        {
            Array = UseReferenceEntry
                ? CreateEntries<ReferenceEntry>(capacity)
                : CreateEntries<GenericEntry>(capacity)
        };

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
        where TKey : allows ref struct
    {
        var entry = entries.Get<Initializer>(TypeSlot<TKey>.Index);
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
        var entry = entries.Get<Initializer>(TypeSlot<TKey>.Index);
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
        ref var itemRef = ref entries.TryGet(TypeSlot<TKey>.Index);
        return !Unsafe.IsNullRef(ref itemRef) && HasValue(itemRef);

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
        var entry = entries.Get<Initializer>(TypeSlot<TKey>.Index);
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
        var entry = entries.Get<Initializer>(TypeSlot<TKey>.Index);
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
        var entry = entries.Get<Initializer>(TypeSlot<TKey>.Index);
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
        ref var itemRef = ref entries.TryGet(TypeSlot<TKey>.Index);
        if (Unsafe.IsNullRef(ref itemRef))
        {
            value = default;
            return false;
        }

        return UseReferenceEntry
            ? Unsafe.As<ReferenceEntry>(itemRef).Unset(out value)
            : Unsafe.As<GenericEntry>(itemRef).Unset(out value);
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
        ref var itemRef = ref entries.TryGet(TypeSlot<TKey>.Index);
        if (Unsafe.IsNullRef(ref itemRef))
        {
            value = default;
            return false;
        }

        return UseReferenceEntry
            ? Unsafe.As<ReferenceEntry>(itemRef).TryGet(out value)
            : Unsafe.As<GenericEntry>(itemRef).TryGet(out value);
    }

    /// <summary>
    /// Removes all elements from this map.
    /// </summary>
    public void Clear()
    {
        var entriesCopy = entries.Array;
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
        private object value = Sentinel.Instance;

        public override bool HasValue => Volatile.Read(in value) != Sentinel.Instance;

        public override bool TrySet(TValue newValue)
            => value == Sentinel.Instance && Interlocked.CompareExchange(
                ref value,
                Unsafe.As<TValue, object>(ref newValue),
                Sentinel.Instance) == Sentinel.Instance;

        public override void Set(TValue newValue)
            => Volatile.Write(ref value, Unsafe.As<TValue, object>(ref newValue));

        public override TValue GetOrSet(TValue newValue, out bool isSet)
        {
            // Perf: GetOrAdd can be implemented by simple CompareExchange. In this case, the cost of GET is the same as of ADD.
            // However, ADD is more unlikely than GET, since the element once added it becomes available for read.
            // Therefore, change the symmetry between GET and ADD overhead as follows:
            // 1. Make GET cheaper
            // 2. Make ADD more expensive
            // So, GET can be done with a simple read. If it's successful, CompareExchange is not needed.
            var result = value;
            if (result == Sentinel.Instance)
            {
                result = Interlocked.CompareExchange(ref value, Unsafe.As<TValue, object>(ref newValue), Sentinel.Instance);

                if (result == Sentinel.Instance)
                {
                    isSet = true;
                    return newValue;
                }
            }

            isSet = false;
            return Unsafe.As<object, TValue>(ref result);
        }

        public override bool SetOrUpdate(TValue newValue)
            => Interlocked.Exchange(ref value, Unsafe.As<TValue, object>(ref newValue)) == Sentinel.Instance;

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
            var valueCopy = value;
            bool removed;
            if (valueCopy != Sentinel.Instance)
            {
                valueCopy = Interlocked.Exchange(ref value, Sentinel.Instance);
                removed = valueCopy != Sentinel.Instance;
                if (removed)
                {
                    oldValue = Unsafe.As<object, TValue>(ref valueCopy);
                    goto exit;
                }
            }
            else
            {
                removed = false;
            }

            oldValue = default;

            exit:
            return removed;
        }

        public override bool TryGet([MaybeNullWhen(false)] out TValue existingValue)
        {
            var valueCopy = Volatile.Read(in value);
            var hasValue = valueCopy != Sentinel.Instance;
            existingValue = hasValue
                ? Unsafe.As<object, TValue>(ref valueCopy)
                : default;

            return hasValue;
        }

        public override void Unset()
        {
            if (value != Sentinel.Instance)
            {
                Interlocked.Exchange(ref value, Sentinel.Instance);
            }
        }
    }

    private sealed class GenericEntry : Entry
    {
        private Atomic<Optional<TValue>> atomic;

        public override bool TrySet(TValue newValue) => atomic.TrySet(newValue);

        public override void Set(TValue newValue) => atomic.Value = newValue;

        public override TValue GetOrSet(TValue newValue, out bool isSet)
            => atomic.GetOrSet(newValue, out isSet);

        public override bool SetOrUpdate(TValue newValue)
        {
            var optional = new Optional<TValue>(newValue);
            atomic.Swap(ref optional);
            return optional.IsUndefined;
        }

        public override bool Set(TValue newValue, [MaybeNullWhen(false)] out TValue oldValue)
        {
            var optional = new Optional<TValue>(newValue);
            atomic.Swap(ref optional);
            return optional.TryGet(out oldValue);
        }

        public override bool Unset([MaybeNullWhen(false)] out TValue oldValue)
        {
            atomic.Clear(out var optional);
            return optional.TryGet(out oldValue);
        }

        public override bool TryGet([MaybeNullWhen(false)] out TValue existingValue)
            => atomic.Value.TryGet(out existingValue);

        public override void Unset() => atomic.Clear();

        public override bool HasValue => !atomic.IsUndefined;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct Initializer : ConcurrentArray<Entry>.IElementInitializer
    {
        static void ConcurrentArray<Entry>.IElementInitializer.Initialize(out Entry value)
            => value = UseReferenceEntry
                ? new ReferenceEntry()
                : new GenericEntry();
    }
}

/// <summary>
/// Represents thread-safe implementation of <see cref="ITypeMap"/> interface.
/// </summary>
public partial class ConcurrentTypeMap : ITypeMap
{
    internal sealed class Entry : ConcurrentArray<Entry>.IElementInitializer
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

        static void ConcurrentArray<Entry>.IElementInitializer.Initialize(out Entry value) => value = new();
    }

    private ConcurrentArray<Entry> entries;

    /// <summary>
    /// Initializes a new empty set.
    /// </summary>
    /// <param name="capacity">The initial capacity of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
    public ConcurrentTypeMap(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        var array = capacity is 0 ? [] : new Entry[capacity];
        Span.Initialize(array);
        entries = new() { Array = array };
    }

    /// <summary>
    /// Initializes a new empty set.
    /// </summary>
    public ConcurrentTypeMap()
        : this(ITypeMap.RecommendedCapacity)
    {
    }

    /// <summary>
    /// Attempts to add a new value to this set.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be added.</param>
    /// <returns><see langword="true"/> if the value is added; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd<T>([DisallowNull] T value)
        => entries.Get<Entry>(TypeSlot<T>.Index).TrySet(value);

    /// <inheritdoc />
    void ITypeMap.Add<T>([DisallowNull] T value)
    {
        if (!TryAdd(value))
            throw new GenericArgumentException<T>(ExceptionMessages.KeyAlreadyExists);
    }

    /// <inheritdoc cref="ITypeMap.Set{T}(T)"/>
    public void Set<T>([DisallowNull] T value)
        => entries.Get<Entry>(TypeSlot<T>.Index).Value = value;

    /// <inheritdoc cref="IReadOnlyTypeMap.Contains{T}"/>
    public bool Contains<T>()
    {
        ref var itemRef = ref entries.TryGet(TypeSlot<T>.Index);
        return !Unsafe.IsNullRef(ref itemRef) && itemRef.Value is T;
    }

    /// <summary>
    /// Attempts to add a new value or returns existing value, atomically.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be added.</param>
    /// <param name="added"><see langword="true"/> if the value is added; <see langword="false"/> if the value is already exist.</param>
    /// <returns>The existing value; or <paramref name="value"/> if added.</returns>
    public T GetOrAdd<T>([DisallowNull] T value, out bool added)
        => (T)entries.Get<Entry>(TypeSlot<T>.Index).TrySet(value, out added);

    /// <summary>
    /// Adds a new value or updates existing one, atomically.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be set.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is added;
    /// <see langword="false"/> if the existing value is updated with <paramref name="value"/>.
    /// </returns>
    public bool AddOrUpdate<T>([DisallowNull] T value)
        => entries.Get<Entry>(TypeSlot<T>.Index).Set(value) is null;

    /// <inheritdoc cref="ITypeMap.Set{T}(T, out T)"/>
    public bool Set<T>([DisallowNull] T newValue, [NotNullWhen(true)] out T? oldValue)
    {
        if (entries.Get<Entry>(TypeSlot<T>.Index).Set(newValue) is T previous)
        {
            oldValue = previous;
            return true;
        }

        oldValue = default;
        return false;
    }

    /// <inheritdoc cref="ITypeMap.Remove{T}()"/>
    public bool Remove<T>()
    {
        ref var itemRef = ref entries.TryGet(TypeSlot<T>.Index);
        return !Unsafe.IsNullRef(ref itemRef) && itemRef.Unset() is T;
    }

    /// <inheritdoc cref="ITypeMap.Remove{T}(out T)"/>
    public bool Remove<T>([NotNullWhen(true)] out T? value)
    {
        ref var itemRef = ref entries.TryGet(TypeSlot<T>.Index);
        if (!Unsafe.IsNullRef(ref itemRef) && itemRef.Unset() is T previous)
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
        foreach (var entry in entries.Array)
        {
            entry.Value = null;
        }
    }

    /// <inheritdoc cref="IReadOnlyTypeMap.TryGetValue{T}(out T)"/>
    public bool TryGetValue<T>([NotNullWhen(true)] out T? value)
    {
        ref var itemRef = ref entries.TryGet(TypeSlot<T>.Index);
        if (!Unsafe.IsNullRef(ref itemRef) && itemRef.Value is T result)
        {
            value = result;
            return true;
        }

        value = default;
        return false;
    }
}