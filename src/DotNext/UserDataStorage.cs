using System;
using System.Collections.Generic;
using System.Threading;

namespace DotNext
{
    using System.Runtime.CompilerServices;
    using static Threading.LockHelpers;

    /// <summary>
    /// Provides access to user data associated with the object.
    /// </summary>
	/// <remarks>
    /// This is by-ref struct because user data should have
    /// the same lifetime as its owner.
	/// </remarks>
    public readonly ref struct UserDataStorage
    {
        private sealed class BackingStorage : Dictionary<long, object>
        {
            private readonly ReaderWriterLockSlim synchronizer = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

            internal V Get<V>(UserDataSlot<V> slot, V defaultValue)
            {
                using (synchronizer.ReadLock())
                {
                    return slot.GetUserData(this, defaultValue);
                }
            }

            internal V GetOrSet<V>(UserDataSlot<V> slot, Func<V> valueFactory)
            {
                V userData;
                //fast path - read user data if it is already exists
                //do not use UpgradableReadLock due to performance reasons
                using (synchronizer.ReadLock())
                {
                    if (slot.GetUserData(this, out userData))
                        return userData;
                }
                //non-fast path, no user data presented
                using (synchronizer.WriteLock())
                {
                    if (!slot.GetUserData(this, out userData))
                        slot.SetUserData(this, userData = valueFactory());
                    return userData;
                }
            }

            internal bool Get<V>(UserDataSlot<V> slot, out V userData)
            {
                using (synchronizer.ReadLock())
                {
                    return slot.GetUserData(this, out userData);
                }
            }

            internal void Set<V>(UserDataSlot<V> slot, V userData)
            {
                using (synchronizer.WriteLock())
                {
                    slot.SetUserData(this, userData);
                }
            }

            internal bool Remove<V>(UserDataSlot<V> slot)
            {
                using (synchronizer.WriteLock())
                {
                    return slot.RemoveUserData(this);
                }
            }

            internal bool Remove<V>(UserDataSlot<V> slot, out V userData)
            {
                //fast path if user data doesn't exist
                using(synchronizer.ReadLock())
                {
                    if(!slot.Contains(this))
                    {
                        userData = default;
                        return false;
                    }
                }
                //non-fast path, user data exists, so remove it
                using(synchronizer.WriteLock())
                {
                    userData = slot.GetUserData(this, default);
                    return slot.RemoveUserData(this);
                }
            }
        }

        private static readonly ConditionalWeakTable<object, BackingStorage> userData = new ConditionalWeakTable<object, BackingStorage>();

        private readonly object owner;

        internal UserDataStorage(object owner)
            => this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

        private BackingStorage GetStorage(bool createIfNeeded)
        {
            if (createIfNeeded)
                return userData.GetOrCreateValue(owner);
            else if (userData.TryGetValue(owner, out var storage))
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
            => GetOrSet(slot, () => new V());

        /// <summary>
        /// Gets existing user data or creates a new data and return it.
        /// </summary>
        /// <typeparam name="V">The type of user data associated with arbitrary object.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <param name="valueFactory">The value supplier which is called when no user data exists.</param>
        /// <returns>The data associated with the slot.</returns>
        public V GetOrSet<V>(UserDataSlot<V> slot, Func<V> valueFactory)
            => GetStorage(true).GetOrSet(slot, valueFactory);

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
        /// <typeparam name="V">Type of data.</typeparam>
        /// <param name="slot">The slot identifying user data.</param>
        /// <returns><see langword="true"/>, if data is removed from this collection.</returns>
        public bool Remove<V>(UserDataSlot<V> slot)
        {
            var storage = GetStorage(false);
            return storage is null ? false : storage.Remove(slot);
        }

        public bool Remove<V>(UserDataSlot<V> slot, out V userData)
        {
            var storage = GetStorage(false);
            if(storage is null)
            {
                userData = default;
                return false;
            }
            else
                return storage.Remove(slot, out userData);
        }

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(owner);
        public override bool Equals(object other) => ReferenceEquals(owner, other);
        public override string ToString() => owner.ToString();

        public static bool operator ==(UserDataStorage first, UserDataStorage second)
            => ReferenceEquals(first.owner, second.owner);

        public static bool operator !=(UserDataStorage first, UserDataStorage second)
            => !ReferenceEquals(first.owner, second.owner);
    }
}