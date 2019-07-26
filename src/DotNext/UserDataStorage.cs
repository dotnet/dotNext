using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext
{
    using System.Runtime.CompilerServices;
    using static Threading.LockAcquisition;

    /// <summary>
    /// Provides access to user data associated with the object.
    /// </summary>
	/// <remarks>
    /// This is by-ref struct because user data should have
    /// the same lifetime as its owner.
	/// </remarks>
    [SuppressMessage("Design", "CA1066")]
    public readonly ref struct UserDataStorage
    {
        private readonly struct Supplier<T, V> : ISupplier<V>
        {
            private readonly T arg;
            private readonly ValueFunc<T, V> factory;

            internal Supplier(T arg, ValueFunc<T, V> factory)
            {
                this.arg = arg;
                this.factory = factory;
            }

            V ISupplier<V>.Supply() => factory.Invoke(arg);
        }

        private readonly struct Supplier<T1, T2, V> : ISupplier<V>
        {
            private readonly T1 arg1;
            private readonly T2 arg2;
            private readonly ValueFunc<T1, T2, V> factory;

            internal Supplier(T1 arg1, T2 arg2, ValueFunc<T1, T2, V> factory)
            {
                this.arg1 = arg1;
                this.arg2 = arg2;
                this.factory = factory;
            }

            V ISupplier<V>.Supply() => factory.Invoke(arg1, arg2);
        }

        [SuppressMessage("Performance", "CA1812", Justification = "It is instantiated by method GetOrCreateValue")]
        private sealed class BackingStorage : Dictionary<long, object>, IDisposable
        {
            private readonly ReaderWriterLockSlim synchronizer = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

            internal V Get<V>(UserDataSlot<V> slot, V defaultValue)
            {
                using (synchronizer.AcquireReadLock())
                {
                    return slot.GetUserData(this, defaultValue);
                }
            }

            internal V GetOrSet<V, S>(UserDataSlot<V> slot, ref S valueFactory)
                where S : struct, ISupplier<V>
            {
                V userData;
                //fast path - read user data if it is already exists
                //do not use UpgradeableReadLock due to performance reasons
                using (synchronizer.AcquireReadLock())
                {
                    if (slot.GetUserData(this, out userData))
                        return userData;
                }
                //non-fast path, no user data presented
                using (synchronizer.AcquireWriteLock())
                {
                    if (!slot.GetUserData(this, out userData))
                        slot.SetUserData(this, userData = valueFactory.Supply());
                    return userData;
                }
            }

            internal bool Get<V>(UserDataSlot<V> slot, out V userData)
            {
                using (synchronizer.AcquireReadLock())
                {
                    return slot.GetUserData(this, out userData);
                }
            }

            internal void Set<V>(UserDataSlot<V> slot, V userData)
            {
                using (synchronizer.AcquireWriteLock())
                {
                    slot.SetUserData(this, userData);
                }
            }

            internal bool Remove<V>(UserDataSlot<V> slot)
            {
                using (synchronizer.AcquireWriteLock())
                {
                    return slot.RemoveUserData(this);
                }
            }

            internal bool Remove<V>(UserDataSlot<V> slot, out V userData)
            {
                //fast path if user data doesn't exist
                using (synchronizer.AcquireReadLock())
                {
                    if (!slot.Contains(this))
                    {
                        userData = default;
                        return false;
                    }
                }
                //non-fast path, user data exists, so remove it
                using (synchronizer.AcquireWriteLock())
                {
                    userData = slot.GetUserData(this, default);
                    return slot.RemoveUserData(this);
                }
            }

            void IDisposable.Dispose()
            {
                synchronizer.Dispose();
                Clear();
                GC.SuppressFinalize(this);
            }
        }

        private static readonly ConditionalWeakTable<object, BackingStorage> UserData = new ConditionalWeakTable<object, BackingStorage>();

        private readonly object owner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal UserDataStorage(object owner)
            => this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

        private BackingStorage GetStorage(bool createIfNeeded)
        {
            if (createIfNeeded)
                return UserData.GetOrCreateValue(owner);
            else if (UserData.TryGetValue(owner, out var storage))
                return storage;
            else
                return null;
        }

        /// <summary>
		/// Gets user data.
		/// </summary>
		/// <typeparam name="V">Type of data.</typeparam>
		/// <param name="slot">The slot identifying user data.</param>
		/// <param name="defaultValue">Default value to be returned if no user data contained in this collection.</param>
		/// <returns>User data.</returns>
        public V Get<V>(UserDataSlot<V> slot, V defaultValue = default)
        {
            var storage = GetStorage(false);
            return storage is null ? defaultValue : storage.Get(slot, defaultValue);
        }

        /// <summary>
        /// Gets existing user data or creates a new data and return it.
        /// </summary>
        /// <typeparam name="V">The type of user data associated with arbitrary object.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <returns>The data associated with the slot.</returns>
        public V GetOrSet<V>(UserDataSlot<V> slot) 
            where V : new()
        {
            var activator = ValueFunc<V>.Activator;
            return GetStorage(true).GetOrSet(slot, ref activator);
        }

        /// <summary>
        /// Gets existing user data or creates a new data and return it.
        /// </summary>
        /// <typeparam name="B">The type of user data associated with arbitrary object.</typeparam>
        /// <typeparam name="D">The derived type with public parameterless constructor.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <returns>The data associated with the slot.</returns>
        public B GetOrSet<B, D>(UserDataSlot<B> slot)
            where D : class, B, new()
        {
            var activator = ValueFunc<D>.Activator;
            return GetStorage(true).GetOrSet(slot, ref activator);
        }

        /// <summary>
        /// Gets existing user data or creates a new data and return it.
        /// </summary>
        /// <typeparam name="V">The type of user data associated with arbitrary object.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
        /// <returns>The data associated with the slot.</returns>
        public V GetOrSet<V>(UserDataSlot<V> slot, Func<V> valueFactory) => GetOrSet(slot, new ValueFunc<V>(valueFactory));

        /// <summary>
        /// Gets existing user data or creates a new data and return it.
        /// </summary>
        /// <typeparam name="T">The type of the argument to be passed into factory.</typeparam>
        /// <typeparam name="V">The type of user data associated with arbitrary object.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <param name="arg">The argument to be passed into factory.</param>
        /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
        /// <returns>The data associated with the slot.</returns>
        public V GetOrSet<T, V>(UserDataSlot<V> slot, T arg, Func<T, V> valueFactory)
            => GetOrSet(slot, arg, new ValueFunc<T, V>(valueFactory));

        /// <summary>
        /// Gets existing user data or creates a new data and return it.
        /// </summary>
        /// <typeparam name="T1">The type of the first argument to be passed into factory.</typeparam>
        /// <typeparam name="T2">The type of the first argument to be passed into factory.</typeparam>
        /// <typeparam name="V">The type of user data associated with arbitrary object.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <param name="arg1">The first argument to be passed into factory.</param>
        /// <param name="arg2">The second argument to be passed into factory.</param>
        /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
        /// <returns>The data associated with the slot.</returns>
        public V GetOrSet<T1, T2, V>(UserDataSlot<V> slot, T1 arg1, T2 arg2, Func<T1, T2, V> valueFactory)
            => GetOrSet(slot, arg1, arg2, new ValueFunc<T1, T2, V>(valueFactory));

        /// <summary>
        /// Gets existing user data or creates a new data and return it.
        /// </summary>
        /// <typeparam name="V">The type of user data associated with arbitrary object.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
        /// <returns>The data associated with the slot.</returns>
        public V  GetOrSet<V>(UserDataSlot<V> slot, ValueFunc<V> valueFactory)
            => GetStorage(true).GetOrSet(slot, ref valueFactory);

        /// <summary>
        /// Gets existing user data or creates a new data and return it.
        /// </summary>
        /// <typeparam name="T">The type of the argument to be passed into factory.</typeparam>
        /// <typeparam name="V">The type of user data associated with arbitrary object.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <param name="arg">The argument to be passed into factory.</param>
        /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
        /// <returns>The data associated with the slot.</returns>
        public V GetOrSet<T, V>(UserDataSlot<V> slot, T arg, ValueFunc<T, V> valueFactory)
        {
            var supplier = new Supplier<T, V>(arg, valueFactory);
            return GetStorage(true).GetOrSet(slot, ref supplier);
        }

        /// <summary>
        /// Gets existing user data or creates a new data and return it.
        /// </summary>
        /// <typeparam name="T1">The type of the first argument to be passed into factory.</typeparam>
        /// <typeparam name="T2">The type of the first argument to be passed into factory.</typeparam>
        /// <typeparam name="V">The type of user data associated with arbitrary object.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <param name="arg1">The first argument to be passed into factory.</param>
        /// <param name="arg2">The second argument to be passed into factory.</param>
        /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
        /// <returns>The data associated with the slot.</returns>
        public V GetOrSet<T1, T2, V>(UserDataSlot<V> slot, T1 arg1, T2 arg2, ValueFunc<T1, T2, V> valueFactory)
        {
            var supplier = new Supplier<T1, T2, V>(arg1, arg2, valueFactory);
            return GetStorage(true).GetOrSet(slot, ref supplier);
        }

        /// <summary>
        /// Tries to get user data.
        /// </summary>
        /// <typeparam name="V">Type of data.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <param name="userData">User data.</param>
        /// <returns><see langword="true"/>, if user data slot exists in this collection.</returns>
        public bool TryGet<V>(UserDataSlot<V> slot, out V userData)
        {
            var storage = GetStorage(false);
            if (storage is null)
            {
                userData = default;
                return false;
            }
            else
                return storage.Get(slot, out userData);
        }

        /// <summary>
        /// Sets user data.
        /// </summary>
        /// <typeparam name="V">Type of data.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <param name="userData">User data to be saved in this collection.</param>
        public void Set<V>(UserDataSlot<V> slot, V userData)
            => GetStorage(true).Set(slot, userData);

        /// <summary>
        /// Removes user data slot.
        /// </summary>
        /// <typeparam name="V">The type of user data.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <returns><see langword="true"/>, if data is removed from this collection.</returns>
        public bool Remove<V>(UserDataSlot<V> slot)
        {
            var storage = GetStorage(false);
            return (storage?.Remove(slot)).GetValueOrDefault();
        }

        /// <summary>
        /// Removes user data slot.
        /// </summary>
        /// <typeparam name="V">The type of user data.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <param name="userData">Remove user data.</param>
        /// <returns><see langword="true"/>, if data is removed from this collection.</returns>
        public bool Remove<V>(UserDataSlot<V> slot, out V userData)
        {
            var storage = GetStorage(false);
            if (storage is null)
            {
                userData = default;
                return false;
            }
            else
                return storage.Remove(slot, out userData);
        }

        /// <summary>
        /// Computes identity hash code for this storage.
        /// </summary>
        /// <returns>The identity hash code for this storage.</returns>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(owner);

        /// <summary>
        /// Determines whether this storage is attached to
        /// the given object.
        /// </summary>
        /// <param name="other">Other object to check.</param>
        /// <returns><see langword="true"/>, if this storage is attached to <paramref name="other"/> object; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => ReferenceEquals(owner, other);

        /// <summary>
        /// Returns textual representation of this storage.
        /// </summary>
        /// <returns>The textual representation of this storage.</returns>
        public override string ToString() => owner.ToString();

        /// <summary>
        /// Determines whether two stores are for the same object.
        /// </summary>
        /// <param name="first">The first storage to compare.</param>
        /// <param name="second">The second storage to compare.</param>
        /// <returns><see langword="true"/>, if two stores are for the same object; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(UserDataStorage first, UserDataStorage second)
            => ReferenceEquals(first.owner, second.owner);

        /// <summary>
        /// Determines whether two stores are not for the same object.
        /// </summary>
        /// <param name="first">The first storage to compare.</param>
        /// <param name="second">The second storage to compare.</param>
        /// <returns><see langword="true"/>, if two stores are not for the same object; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(UserDataStorage first, UserDataStorage second)
            => !ReferenceEquals(first.owner, second.owner);
    }
}