using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace DotNext
{
    using Threading;
    
    /// <summary>
    /// Provides access to user data associated with the object.
    /// </summary>
	/// <remarks>
	/// All instance members of this class are thread-safe.
	/// </remarks>
    public sealed class UserDataStorage
    {
        private readonly ConcurrentDictionary<long, object> storage;

        internal UserDataStorage()
            => storage = new ConcurrentDictionary<long, object>(ValueType<long>.EqualityComparer);

		/// <summary>
		/// Gets user data.
		/// </summary>
		/// <typeparam name="V">Type of data.</typeparam>
		/// <param name="slot">User data slot.</param>
		/// <param name="defaultValue">Default value to be returned if no user data contained in this collection.</param>
		/// <returns>User data.</returns>
        public V Get<V>(UserDataSlot<V> slot, V defaultValue = default) => slot.GetUserData(storage, defaultValue);

		/// <summary>
		/// Tries to get user data.
		/// </summary>
		/// <typeparam name="V">Type of data.</typeparam>
		/// <param name="slot">User data slot.</param>
		/// <param name="userData">User data.</param>
		/// <returns><see langword="true"/>, if user data slot exists in this collection.</returns>
		public bool Get<V>(UserDataSlot<V> slot, out V userData) => slot.GetUserData(storage, out userData);

		/// <summary>
		/// Sets user data.
		/// </summary>
		/// <typeparam name="V">Type of data.</typeparam>
		/// <param name="slot">User data slot.</param>
		/// <param name="userData">User data to be saved in this collection.</param>
		public void Set<V>(UserDataSlot<V> slot, V userData) => slot.SetUserData(storage, userData);

		/// <summary>
		/// Removes user data slot.
		/// </summary>
		/// <typeparam name="V">Type of data.</typeparam>
		/// <param name="slot">User data slot to be removed from this collection.</param>
		/// <returns><see langword="true"/>, if data is removed from this collection.</returns>
		public bool Remove<V>(UserDataSlot<V> slot) => slot.RemoveUserData(storage);
    }

    internal static class UserDataSlot
    {
        
        private static long counter;

        internal static long NewId => counter.IncrementAndGet();
    }

    /// <summary>
    /// Uniquely identifies user data which can be associated
    /// with any object.
    /// </summary>
    public readonly struct UserDataSlot<V>: IEquatable<UserDataSlot<V>>
    {   
        /// <summary>
        /// Unique identifier of the data slot.
        /// </summary>
        private readonly long Id;

        private UserDataSlot(long id) => Id = id;

		/// <summary>
		/// Allocates a new data slot.
		/// </summary>
		/// <returns>Allocated data slot.</returns>
		public static UserDataSlot<V> Allocate() => new UserDataSlot<V>(UserDataSlot.NewId);

        internal V GetUserData(IDictionary<long, object> storage, V defaultValue)
            => storage.TryGetValue(Id, out var userData) && userData is V result ? result : defaultValue;

        internal bool GetUserData(IDictionary<long, object> storage, out V userData)
        {
            if(storage.TryGetValue(Id, out var value) && value is V typedValue)
            {
                userData = typedValue;
                return true;
            }
            else
            {
                userData = default;
                return false;
            }
        }

        internal void SetUserData(IDictionary<long, object> storage, V userData)
        {
            if(Id == 0)
                throw new ArgumentException(ExceptionMessages.InvalidUserDataSlot);
            else 
                storage[Id] = userData;
        }

        internal bool RemoveUserData(IDictionary<long, object> storage)
            => storage.Remove(Id);
        
        /// <summary>
        /// Checks whether the two data slots are the same.
        /// </summary>
        /// <param name="other">Other data slot to check.</param>
        /// <returns><see langword="true"/> if both data slots identifies the same data key.</returns>
        public bool Equals(UserDataSlot<V> other) => Id == other.Id;

        /// <summary>
        /// Checks whether the two data slots are the same.
        /// </summary>
        /// <param name="other">Other data slot to check.</param>
        /// <returns><see langword="true"/> if both data slots identifies the same data key.</returns>
        public override bool Equals(object other) => other is UserDataSlot<V> slot && Equals(slot);

        /// <summary>
        /// Computes hash code for this data slot.
        /// </summary>
        /// <returns>Hash code.</returns>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// Gets textual representation of this data slot
        /// useful for debugging.
        /// </summary>
        /// <returns>Textual representation of this data slot.</returns>
        public override string ToString() => Id.ToString();

        /// <summary>
        /// Checks whether the two data slots are the same.
        /// </summary>
        /// <param name="first">The first data slot to check.</param>
        /// <param name="second">The second data slot to check.</param>
        /// <returns><see langword="true"/> if both data slots identifies the same data key.</returns>
        public static bool operator==(UserDataSlot<V> first, UserDataSlot<V> second)
            => first.Id == second.Id;
        
        /// <summary>
        /// Checks whether the two data slots are not the same.
        /// </summary>
        /// <param name="first">The first data slot to check.</param>
        /// <param name="second">The second data slot to check.</param>
        /// <returns><see langword="false"/> if both data slots identifies the same data key.</returns>
        public static bool operator!=(UserDataSlot<V> first, UserDataSlot<V> second)
            => first.Id == second.Id;
    }
}