using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Collections.Specialized;

using Threading;

/// <summary>
/// Represents thread-safe implementation of <see cref="ITypeMap{TValue}"/> interface.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public class ConcurrentTypeMap<TValue> : ITypeMap<TValue>
{
    private const int EmptyValueState = 0;
    private const int LockedState = 1;
    private const int NotEmptyValueState = 2;

    private ReaderWriterSpinLock rwLock;
    private (int, TValue?)[] storage;

    /// <summary>
    /// Initializes a new map.
    /// </summary>
    /// <param name="capacity">The initial capacity of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
    public ConcurrentTypeMap(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        storage = capacity == 0 ? Array.Empty<(int, TValue?)>() : new (int, TValue?)[capacity];
    }

    /// <summary>
    /// Initializes a new map of recommended capacity.
    /// </summary>
    public ConcurrentTypeMap()
        => storage = new (int, TValue?)[ITypeMap<TValue>.RecommendedCapacity];

    private void Resize<TKey>()
    {
        rwLock.UpgradeToWriteLock();

        if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
            Array.Resize(ref storage, ITypeMap<TValue>.RecommendedCapacity);

        rwLock.DowngradeFromWriteLock();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref (int State, TValue? Value) Get<TKey>((int, TValue?)[] storage)
    {
        Debug.Assert(ITypeMap<TValue>.GetIndex<TKey>() < storage.Length);

        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(storage), ITypeMap<TValue>.GetIndex<TKey>());
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
        bool result;

        rwLock.EnterReadLock();
        if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
            Resize<TKey>();

        ref var holder = ref Get<TKey>(storage);
        if (TryAcquireLock(ref holder.State, EmptyValueState))
        {
            holder.Value = value;
            holder.State.VolatileWrite(NotEmptyValueState); // release
            result = true;
        }
        else
        {
            result = false;
        }

        rwLock.ExitReadLock();
        return result;
    }

    /// <summary>
    /// Associates the value with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value to set.</param>
    public void Set<TKey>(TValue value)
    {
        rwLock.EnterReadLock();
        if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
            Resize<TKey>();

        ref var holder = ref Get<TKey>(storage);
        AcquireLock(ref holder.State); // acquire

        holder.Value = value;

        holder.State.VolatileWrite(NotEmptyValueState); // release
        rwLock.ExitReadLock();
    }

    /// <summary>
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey<TKey>()
    {
        rwLock.EnterReadLock();
        var result = ITypeMap<TValue>.GetIndex<TKey>() < storage.Length && CheckState(ref Get<TKey>(storage).State, NotEmptyValueState);
        rwLock.ExitReadLock();
        return result;
    }

    private static bool CheckState(ref int state, int expected)
    {
        bool result;

        for (var spinner = new SpinWait(); ; spinner.SpinOnce())
        {
            var currentState = state.VolatileRead();
            if (currentState == LockedState)
                continue;

            result = currentState == expected;
            break;
        }

        return result;
    }

    private static int AcquireLock(ref int state)
    {
        int currentState;

        for (var spinner = new SpinWait(); ; spinner.SpinOnce())
        {
            currentState = state.VolatileRead();
            if (currentState == LockedState)
                continue;

            if (state.CompareAndSet(currentState, LockedState))
                break;
        }

        return currentState;
    }

    private static bool TryAcquireLock(ref int state, int expected)
    {
        for (var spinner = new SpinWait(); ; spinner.SpinOnce())
        {
            var currentState = state.VolatileRead();

            if (currentState == LockedState)
                continue;

            if (currentState != expected)
                return false;

            if (state.CompareAndSet(currentState, LockedState))
                break;
        }

        return true;
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
        rwLock.EnterReadLock();
        if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
            Resize<TKey>();

        ref var holder = ref Get<TKey>(storage);

        // acquire
        if (AcquireLock(ref holder.State) == EmptyValueState)
        {
            holder.Value = value;
            added = true;
        }
        else
        {
            value = holder.Value!;
            added = false;
        }

        holder.State.VolatileWrite(NotEmptyValueState); // release
        rwLock.ExitReadLock();

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

        rwLock.EnterReadLock();
        if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
            Resize<TKey>();

        ref var holder = ref Get<TKey>(storage);
        added = AcquireLock(ref holder.State) == EmptyValueState; // acquire
        holder.Value = value;

        holder.State.VolatileWrite(NotEmptyValueState); // release
        rwLock.ExitReadLock();

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
        rwLock.EnterReadLock();
        if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
            Resize<TKey>();

        ref var holder = ref Get<TKey>(storage);
        Optional<TValue> result = AcquireLock(ref holder.State) == EmptyValueState // acquire
            ? Optional<TValue>.None
            : holder.Value;

        holder.Value = value;

        holder.State.VolatileWrite(NotEmptyValueState); // release
        rwLock.ExitReadLock();

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

        rwLock.EnterReadLock();
        if (ITypeMap<TValue>.GetIndex<TKey>() < storage.Length)
        {
            ref var holder = ref Get<TKey>(storage);
            if (TryAcquireLock(ref holder.State, NotEmptyValueState))
            {
                value = holder.Value;
                holder.Value = default;
                holder.State.VolatileWrite(EmptyValueState); // release
                result = true;
                goto exit;
            }
        }

        value = default;
        result = false;

    exit:
        rwLock.ExitReadLock();
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
        rwLock.EnterReadLock();

        if (ITypeMap<TValue>.GetIndex<TKey>() < storage.Length)
        {
            ref var holder = ref Get<TKey>(storage);
            if (TryAcquireLock(ref holder.State, NotEmptyValueState))
            {
                value = holder.Value;
                holder.State.VolatileWrite(NotEmptyValueState); // release
                result = true;
                goto exit;
            }
        }

        result = false;
        value = default;

    exit:
        rwLock.ExitReadLock();
        return result;
    }

    /// <summary>
    /// Removes all elements from this map.
    /// </summary>
    public void Clear()
    {
        rwLock.EnterWriteLock();
        Array.Clear(storage);
        rwLock.ExitWriteLock();
    }
}