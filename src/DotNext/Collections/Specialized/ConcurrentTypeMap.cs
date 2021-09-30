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
    private const int UndefinedValueState = 0;
    private const int InProgressState = 1;
    private const int HasValueState = 2;

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

    private void EnterReadLockAndEnsureCapacity<TKey>()
    {
        rwLock.EnterReadLock();
        if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
        {
            rwLock.UpgradeToWriteLock();
            try
            {
                if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
                    Array.Resize(ref storage, ITypeMap<TValue>.RecommendedCapacity);
            }
            finally
            {
                rwLock.DowngradeToReadLock();
            }
        }
    }

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
        EnterReadLockAndEnsureCapacity<TKey>();
        try
        {
            ref var holder = ref Get<TKey>(storage);
            if (!TryAcquireUpdateLock(ref holder.State)) // acquire
                return false;

            holder.Value = value;
            holder.State.VolatileWrite(HasValueState); // release
        }
        finally
        {
            rwLock.ExitReadLock();
        }

        return true;
    }

    /// <summary>
    /// Associates the value with the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <param name="value">The value to set.</param>
    public void Set<TKey>(TValue value)
    {
        EnterReadLockAndEnsureCapacity<TKey>();
        try
        {
            ref var holder = ref Get<TKey>(storage);
            AcquireUpdateLock(ref holder.State); // acquire

            holder.Value = value;
            holder.State = HasValueState; // release
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Determines whether the map has association between the value and the specified type.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if there is a value associated with <typeparamref name="TKey"/>; otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey<TKey>()
    {
        rwLock.EnterReadLock();
        try
        {
            if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
                return false;

            ref var holder = ref Get<TKey>(storage);

            for (var spin = new SpinWait(); ; spin.SpinOnce())
            {
                var currentState = holder.State.VolatileRead();
                if (currentState == InProgressState)
                    continue;

                return currentState == HasValueState;
            }
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    private static int AcquireUpdateLock(ref int state)
    {
        int currentState;

        for (var spin = new SpinWait(); ; spin.SpinOnce())
        {
            currentState = state.VolatileRead();
            if (currentState == InProgressState)
                continue;

            if (state.CompareAndSet(currentState, InProgressState))
                break;
        }

        return currentState;
    }

    private static bool TryAcquireUpdateLock(ref int state)
    {
        for (var spin = new SpinWait(); ; spin.SpinOnce())
        {
            var currentState = state.VolatileRead();
            switch (currentState)
            {
                default:
                    continue;
                case HasValueState:
                    return false;
                case UndefinedValueState:
                    break;
            }

            if (state.CompareAndSet(currentState, InProgressState))
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
        EnterReadLockAndEnsureCapacity<TKey>();
        try
        {
            ref var holder = ref Get<TKey>(storage);

            // acquire
            if (AcquireUpdateLock(ref holder.State) == UndefinedValueState)
            {
                holder.Value = value;
                added = true;
            }
            else
            {
                value = holder.Value!;
                added = false;
            }

            holder.State.VolatileWrite(HasValueState); // release
        }
        finally
        {
            rwLock.ExitReadLock();
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

        EnterReadLockAndEnsureCapacity<TKey>();
        try
        {
            ref var holder = ref Get<TKey>(storage);
            added = AcquireUpdateLock(ref holder.State) == UndefinedValueState; // acquire
            holder.Value = value;
            holder.State.VolatileWrite(HasValueState); // release
        }
        finally
        {
            rwLock.ExitReadLock();
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

        EnterReadLockAndEnsureCapacity<TKey>();
        try
        {
            ref var holder = ref Get<TKey>(storage);
            result = AcquireUpdateLock(ref holder.State) == UndefinedValueState // acquire
                ? Optional<TValue>.None
                : holder.Value;

            holder.Value = value;
            holder.State.VolatileWrite(HasValueState); // release
        }
        finally
        {
            rwLock.ExitReadLock();
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

        rwLock.EnterReadLock();
        try
        {
            if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
            {
                value = default;
                result = false;
            }
            else
            {
                ref var holder = ref Get<TKey>(storage);
                result = AcquireUpdateLock(ref holder.State) == HasValueState; // acquire
                value = holder.Value;
                holder.Value = default;
                holder.State.VolatileWrite(UndefinedValueState); // release
            }
        }
        finally
        {
            rwLock.ExitReadLock();
        }

        return result;
    }

    /// <summary>
    /// Attempts to remove the value from the map.
    /// </summary>
    /// <typeparam name="TKey">The type acting as a key.</typeparam>
    /// <returns><see langword="true"/> if the element successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<TKey>()
    {
        bool result;

        rwLock.EnterReadLock();
        try
        {
            if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
            {
                result = false;
            }
            else
            {
                ref var holder = ref Get<TKey>(storage);
                result = AcquireUpdateLock(ref holder.State) == HasValueState; // acquire
                holder.Value = default;
                holder.State.VolatileWrite(UndefinedValueState); // release
            }
        }
        finally
        {
            rwLock.ExitReadLock();
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
    {
        bool result;

        rwLock.EnterReadLock();
        try
        {
            if (ITypeMap<TValue>.GetIndex<TKey>() >= storage.Length)
            {
                result = false;
                value = default;
            }
            else
            {
                ref var holder = ref Get<TKey>(storage);
                var previousState = AcquireUpdateLock(ref holder.State);
                result = previousState == HasValueState; // acquire
                value = holder.Value;
                holder.State.VolatileWrite(previousState); // release
            }
        }
        finally
        {
            rwLock.ExitReadLock();
        }

        return result;
    }

    /// <summary>
    /// Removes all elements from this map.
    /// </summary>
    public void Clear()
    {
        rwLock.EnterWriteLock();
        try
        {
            Array.Clear(storage);
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }
}