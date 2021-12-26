using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext;

using static Collections.Generic.Collection;
using ReaderWriterSpinLock = Threading.ReaderWriterSpinLock;

/// <summary>
/// Provides access to user data associated with the object.
/// </summary>
/// <remarks>
/// This is by-ref struct because user data should have
/// the same lifetime as its owner.
/// </remarks>
public readonly ref struct UserDataStorage
{
    /// <summary>
    /// Implementation of this interface allows to customize behavior of
    /// <see cref="ObjectExtensions.GetUserData{T}(T)"/> method.
    /// </summary>
    /// <remarks>
    /// If runtime type of object passed to <see cref="ObjectExtensions.GetUserData{T}(T)"/> method
    /// provides implementation of this interface then actual <see cref="UserDataStorage"/>
    /// depends on the <see cref="Source"/> implementation.
    /// It is recommended to implement this interface explicitly.
    /// </remarks>
    public interface IContainer
    {
        /// <summary>
        /// Gets the actual source of user data for this object.
        /// </summary>
        /// <remarks>
        /// If this property returns <c>this</c> object then user data has to be attached to the object itself;
        /// otherwise, use the data attached to the returned object.
        /// Additionally, you can store user data explicitly in the backing field which is initialized
        /// with real user data storage using <see cref="CreateStorage"/> method.
        /// </remarks>
        /// <value>The source of user data for this object.</value>
        object Source { get; }

        /// <summary>
        /// Creates a storage of user data that can be saved into field
        /// and returned via <see cref="Source"/> property.
        /// </summary>
        /// <returns>The object representing storage for user data.</returns>
        protected static object CreateStorage() => new BackingStorage();
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct DelegatingValueFactory<T, TResult> : ISupplier<TResult>
    {
        private readonly T arg;
        private readonly Func<T, TResult> factory;

        internal DelegatingValueFactory(T arg, Func<T, TResult> factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.arg = arg;
        }

        TResult ISupplier<TResult>.Invoke() => factory(arg);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly unsafe struct ValueFactory<T, TResult> : ISupplier<TResult>
    {
        private readonly T arg;
        private readonly delegate*<T, TResult> factory;

        internal ValueFactory(T arg, delegate*<T, TResult> factory)
        {
            this.factory = factory == null ? throw new ArgumentNullException(nameof(factory)) : factory;
            this.arg = arg;
        }

        TResult ISupplier<TResult>.Invoke() => factory(arg);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly unsafe struct ValueFactory<T1, T2, TResult> : ISupplier<TResult>
    {
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly delegate*<T1, T2, TResult> factory;

        internal ValueFactory(T1 arg1, T2 arg2, delegate*<T1, T2, TResult> factory)
        {
            this.factory = factory == null ? throw new ArgumentNullException(nameof(factory)) : factory;
            this.arg1 = arg1;
            this.arg2 = arg2;
        }

        TResult ISupplier<TResult>.Invoke() => factory(arg1, arg2);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct DelegatingValueFactory<T1, T2, TResult> : ISupplier<TResult>
    {
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly Func<T1, T2, TResult> factory;

        internal DelegatingValueFactory(T1 arg1, T2 arg2, Func<T1, T2, TResult> factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.arg1 = arg1;
            this.arg2 = arg2;
        }

        TResult ISupplier<TResult>.Invoke() => factory(arg1, arg2);
    }

    private sealed class BackingStorage : Dictionary<long, object?>
    {
        // ReaderWriterLockSlim is not used because it is heavyweight
        // spin-based lock is used instead because it is very low probability of concurrent
        // updates of the same backing storage.
        private ReaderWriterSpinLock lockState;

        // should be public because called through Activator by ConditionalWeakTable
        public BackingStorage()
            : base(3)
        {
        }

        private BackingStorage(IDictionary<long, object?> source)
            : base(source)
        {
        }

        internal BackingStorage Copy()
        {
            BackingStorage copy;
            lockState.EnterReadLock();

            try
            {
                copy = new BackingStorage(this);
            }
            finally
            {
                lockState.ExitReadLock();
            }

            return copy;
        }

        internal void CopyTo(BackingStorage dest)
        {
            lockState.EnterReadLock();
            dest.lockState.EnterWriteLock();
            try
            {
                dest.Clear();
                dest.AddAll(this);
            }
            finally
            {
                dest.lockState.ExitWriteLock();
                lockState.ExitReadLock();
            }
        }

        [return: NotNullIfNotNull("defaultValue")]
        internal TValue? Get<TValue>(UserDataSlot<TValue> slot, TValue? defaultValue)
        {
            TValue? result;
            lockState.EnterReadLock();
            try
            {
                result = slot.GetUserData(this, defaultValue);
            }
            finally
            {
                lockState.ExitReadLock();
            }

            return result;
        }

        internal TValue? GetOrSet<TValue, TSupplier>(UserDataSlot<TValue> slot, TSupplier valueFactory)
            where TSupplier : struct, ISupplier<TValue>
        {
            // fast path - read lock is required
            lockState.EnterReadLock();
            var exists = slot.GetUserData(this, out var userData);
            lockState.ExitReadLock();
            if (exists)
                goto exit;

            // non-fast path: factory should be called
            lockState.EnterWriteLock();
            if (slot.GetUserData(this, out userData))
            {
                lockState.ExitWriteLock();
            }
            else
            {
                try
                {
                    userData = valueFactory.Invoke();
                    if (userData is not null)
                        slot.SetUserData(this, userData);
                }
                finally
                {
                    lockState.ExitWriteLock();
                }
            }

        exit:
            return userData;
        }

        internal bool Get<TValue>(UserDataSlot<TValue> slot, [MaybeNullWhen(false)] out TValue userData)
        {
            lockState.EnterReadLock();
            try
            {
                return slot.GetUserData(this, out userData);
            }
            finally
            {
                lockState.ExitReadLock();
            }
        }

        internal void Set<TValue>(UserDataSlot<TValue> slot, TValue userData)
        {
            lockState.EnterWriteLock();
            try
            {
                slot.SetUserData(this, userData);
            }
            finally
            {
                lockState.ExitWriteLock();
            }
        }

        internal bool Remove<TValue>(UserDataSlot<TValue> slot)
        {
            lockState.EnterWriteLock();
            var result = slot.RemoveUserData(this);
            lockState.ExitWriteLock();
            return result;
        }

        internal bool Remove<TValue>(UserDataSlot<TValue> slot, [MaybeNullWhen(false)] out TValue userData)
        {
            lockState.EnterWriteLock();
            try
            {
                return slot.RemoveUserData(this, out userData);
            }
            finally
            {
                lockState.ExitWriteLock();
            }
        }
    }

    /*
     * ConditionalWeakTable is synchronized so we use a bucket of tables
     * to reduce the risk of lock contention. The specific table for the object
     * is based on object's identity hash code.
     */
    private static readonly ConditionalWeakTable<object, BackingStorage>[] Buckets;

    static UserDataStorage()
    {
        var size = Environment.ProcessorCount;
        size += size / 2;

        Span.Initialize<ConditionalWeakTable<object, BackingStorage>>(Buckets = new ConditionalWeakTable<object, BackingStorage>[size]);
    }

    private readonly object source;

    internal UserDataStorage(object source) => this.source = source switch
    {
        null => throw new ArgumentNullException(nameof(UserDataStorage.source)),
        IContainer support => support.Source,
        _ => source,
    };

    private static ConditionalWeakTable<object, BackingStorage> GetStorage(object source)
    {
        var bucket = (RuntimeHelpers.GetHashCode(source) % Buckets.Length) & int.MaxValue;
        Debug.Assert(bucket >= 0 && bucket < Buckets.Length);

        return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Buckets), bucket);
    }

    private BackingStorage? GetStorage()
        => source is BackingStorage storage || GetStorage(source).TryGetValue(source, out storage!) ? storage : null;

    private BackingStorage GetOrCreateStorage()
    {
        if (source is not BackingStorage storage)
            storage = GetStorage(source).GetOrCreateValue(source);

        return storage;
    }

    /// <summary>
    /// Gets user data.
    /// </summary>
    /// <typeparam name="TValue">Type of data.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <param name="defaultValue">Default value to be returned if no user data contained in this collection.</param>
    /// <returns>User data.</returns>
    [return: NotNullIfNotNull("defaultValue")]
    public TValue? Get<TValue>(UserDataSlot<TValue> slot, TValue? defaultValue)
    {
        var storage = GetStorage();
        return storage is null ? defaultValue : storage.Get(slot!, defaultValue);
    }

    /// <summary>
    /// Gets user data.
    /// </summary>
    /// <typeparam name="TValue">Type of data.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <returns>User data; or <c>default(V)</c> if there is no user data associated with <paramref name="slot"/>.</returns>
    public TValue? Get<TValue>(UserDataSlot<TValue> slot)
    {
        var storage = GetStorage();
        return storage is null ? default : storage.Get(slot!, default);
    }

    /// <summary>
    /// Gets existing user data or creates a new data and return it.
    /// </summary>
    /// <typeparam name="TValue">The type of user data associated with arbitrary object.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <returns>The data associated with the slot.</returns>
    public TValue GetOrSet<TValue>(UserDataSlot<TValue> slot)
        where TValue : notnull, new()
        => GetOrSet(slot, new Activator<TValue>());

    /// <summary>
    /// Gets existing user data or creates a new data and return it.
    /// </summary>
    /// <typeparam name="TBase">The type of user data associated with arbitrary object.</typeparam>
    /// <typeparam name="T">The derived type with public parameterless constructor.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <returns>The data associated with the slot.</returns>
    public TBase GetOrSet<TBase, T>(UserDataSlot<TBase> slot)
        where T : class, TBase, new()
        => GetOrSet(slot, new Activator<T>());

    /// <summary>
    /// Gets existing user data or creates a new data and return it.
    /// </summary>
    /// <typeparam name="TValue">The type of user data associated with arbitrary object.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
    /// <returns>The data associated with the slot.</returns>
    public TValue GetOrSet<TValue>(UserDataSlot<TValue> slot, Func<TValue> valueFactory)
        => GetOrSet(slot, new DelegatingSupplier<TValue>(valueFactory));

    /// <summary>
    /// Gets existing user data or creates a new data and return it.
    /// </summary>
    /// <typeparam name="TValue">The type of user data associated with arbitrary object.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
    /// <returns>The data associated with the slot.</returns>
    [CLSCompliant(false)]
    public unsafe TValue GetOrSet<TValue>(UserDataSlot<TValue> slot, delegate*<TValue> valueFactory)
        => GetOrSet(slot, new Supplier<TValue>(valueFactory));

    /// <summary>
    /// Gets existing user data or creates a new data and return it.
    /// </summary>
    /// <typeparam name="T">The type of the argument to be passed into factory.</typeparam>
    /// <typeparam name="TValue">The type of user data associated with arbitrary object.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <param name="arg">The argument to be passed into factory.</param>
    /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
    /// <returns>The data associated with the slot.</returns>
    public TValue GetOrSet<T, TValue>(UserDataSlot<TValue> slot, T arg, Func<T, TValue> valueFactory)
        => GetOrSet(slot, new DelegatingValueFactory<T, TValue>(arg, valueFactory));

    /// <summary>
    /// Gets existing user data or creates a new data and return it.
    /// </summary>
    /// <typeparam name="T">The type of the argument to be passed into factory.</typeparam>
    /// <typeparam name="TValue">The type of user data associated with arbitrary object.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <param name="arg">The argument to be passed into factory.</param>
    /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
    /// <returns>The data associated with the slot.</returns>
    [CLSCompliant(false)]
    public unsafe TValue GetOrSet<T, TValue>(UserDataSlot<TValue> slot, T arg, delegate*<T, TValue> valueFactory)
        => GetOrSet(slot, new ValueFactory<T, TValue>(arg, valueFactory));

    /// <summary>
    /// Gets existing user data or creates a new data and return it.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument to be passed into factory.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed into factory.</typeparam>
    /// <typeparam name="TValue">The type of user data associated with arbitrary object.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <param name="arg1">The first argument to be passed into factory.</param>
    /// <param name="arg2">The second argument to be passed into factory.</param>
    /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
    /// <returns>The data associated with the slot.</returns>
    public TValue GetOrSet<T1, T2, TValue>(UserDataSlot<TValue> slot, T1 arg1, T2 arg2, Func<T1, T2, TValue> valueFactory)
        => GetOrSet(slot, new DelegatingValueFactory<T1, T2, TValue>(arg1, arg2, valueFactory));

    /// <summary>
    /// Gets existing user data or creates a new data and return it.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument to be passed into factory.</typeparam>
    /// <typeparam name="T2">The type of the second argument to be passed into factory.</typeparam>
    /// <typeparam name="TValue">The type of user data associated with arbitrary object.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <param name="arg1">The first argument to be passed into factory.</param>
    /// <param name="arg2">The second argument to be passed into factory.</param>
    /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
    /// <returns>The data associated with the slot.</returns>
    [CLSCompliant(false)]
    public unsafe TValue GetOrSet<T1, T2, TValue>(UserDataSlot<TValue> slot, T1 arg1, T2 arg2, delegate*<T1, T2, TValue> valueFactory)
        => GetOrSet(slot, new ValueFactory<T1, T2, TValue>(arg1, arg2, valueFactory));

    /// <summary>
    /// Gets existing user data or creates a new data and return it.
    /// </summary>
    /// <typeparam name="TValue">The type of user data associated with arbitrary object.</typeparam>
    /// <typeparam name="TFactory">The type of the factory.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
    /// <returns>The data associated with the slot.</returns>
    public TValue GetOrSet<TValue, TFactory>(UserDataSlot<TValue> slot, TFactory valueFactory)
        where TFactory : struct, ISupplier<TValue>
        => GetOrCreateStorage().GetOrSet(slot, valueFactory)!;

    /// <summary>
    /// Tries to get user data.
    /// </summary>
    /// <typeparam name="TValue">Type of data.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <param name="userData">User data.</param>
    /// <returns><see langword="true"/>, if user data slot exists in this collection.</returns>
    public bool TryGet<TValue>(UserDataSlot<TValue> slot, [MaybeNullWhen(false)] out TValue userData)
    {
        var storage = GetStorage();
        if (storage is null)
        {
            userData = default!;
            return false;
        }

        return storage.Get(slot, out userData);
    }

    /// <summary>
    /// Sets user data.
    /// </summary>
    /// <typeparam name="TValue">Type of data.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <param name="userData">User data to be saved in this collection.</param>
    public void Set<TValue>(UserDataSlot<TValue> slot, [DisallowNull] TValue userData)
        => GetOrCreateStorage().Set(slot, userData);

    /// <summary>
    /// Removes user data slot.
    /// </summary>
    /// <typeparam name="TValue">The type of user data.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <returns><see langword="true"/>, if data is removed from this collection.</returns>
    public bool Remove<TValue>(UserDataSlot<TValue> slot)
    {
        var storage = GetStorage();
        return storage?.Remove(slot) ?? false;
    }

    /// <summary>
    /// Removes user data slot.
    /// </summary>
    /// <typeparam name="TValue">The type of user data.</typeparam>
    /// <param name="slot">The slot identifying user data.</param>
    /// <param name="userData">Remove user data.</param>
    /// <returns><see langword="true"/>, if data is removed from this collection.</returns>
    public bool Remove<TValue>(UserDataSlot<TValue> slot, [MaybeNullWhen(false)] out TValue userData)
    {
        var storage = GetStorage();
        if (storage is null)
        {
            userData = default;
            return false;
        }

        return storage.Remove(slot, out userData);
    }

    /// <summary>
    /// Replaces user data of the object with the copy of the current one.
    /// </summary>
    /// <param name="obj">The object which user data has to be replaced with the copy of the current one.</param>
    public void CopyTo(object obj)
    {
        switch (obj)
        {
            case null:
                throw new ArgumentNullException(nameof(obj));
            case IContainer container:
                obj = container.Source;
                break;
        }

        var source = GetStorage();
        if (source is not null)
        {
            if (obj is BackingStorage destination)
                source.CopyTo(destination);
            else
                GetStorage(obj).Add(obj, source.Copy());
        }
    }

    /// <summary>
    /// Computes identity hash code for this storage.
    /// </summary>
    /// <returns>The identity hash code for this storage.</returns>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(source);

    /// <summary>
    /// Determines whether this storage is attached to
    /// the given object.
    /// </summary>
    /// <param name="other">Other object to check.</param>
    /// <returns><see langword="true"/>, if this storage is attached to <paramref name="other"/> object; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? other) => ReferenceEquals(source, other);

    /// <summary>
    /// Returns textual representation of this storage.
    /// </summary>
    /// <returns>The textual representation of this storage.</returns>
    public override string? ToString() => source?.ToString();

    /// <summary>
    /// Determines whether two stores are for the same object.
    /// </summary>
    /// <param name="first">The first storage to compare.</param>
    /// <param name="second">The second storage to compare.</param>
    /// <returns><see langword="true"/>, if two stores are for the same object; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(UserDataStorage first, UserDataStorage second)
        => ReferenceEquals(first.source, second.source);

    /// <summary>
    /// Determines whether two stores are not for the same object.
    /// </summary>
    /// <param name="first">The first storage to compare.</param>
    /// <param name="second">The second storage to compare.</param>
    /// <returns><see langword="true"/>, if two stores are not for the same object; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(UserDataStorage first, UserDataStorage second)
        => !ReferenceEquals(first.source, second.source);
}