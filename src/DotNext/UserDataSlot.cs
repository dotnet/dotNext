using System;
using System.Collections.Generic;

namespace DotNext
{
    using static Threading.AtomicInt64;

    internal static class UserDataSlot
    {

        private static long counter;

        internal static long NewId => counter.IncrementAndGet();
    }

    /// <summary>
    /// Uniquely identifies user data which can be associated
    /// with any object.
    /// </summary>
    public readonly struct UserDataSlot<V> : IEquatable<UserDataSlot<V>>
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


        internal bool Contains(IDictionary<long, object> storage) => storage.ContainsKey(Id);

        internal V GetUserData(IDictionary<long, object> storage, V defaultValue)
            => storage.TryGetValue(Id, out var userData) && userData is V result ? result : defaultValue;

        internal bool GetUserData(IDictionary<long, object> storage, out V userData)
        {
            if (storage.TryGetValue(Id, out var value) && value is V typedValue)
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
            if (Id == 0)
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
        public override string ToString() => Id.ToString(default(IFormatProvider));

        /// <summary>
        /// Checks whether the two data slots are the same.
        /// </summary>
        /// <param name="first">The first data slot to check.</param>
        /// <param name="second">The second data slot to check.</param>
        /// <returns><see langword="true"/> if both data slots identifies the same data key.</returns>
        public static bool operator ==(UserDataSlot<V> first, UserDataSlot<V> second)
            => first.Id == second.Id;

        /// <summary>
        /// Checks whether the two data slots are not the same.
        /// </summary>
        /// <param name="first">The first data slot to check.</param>
        /// <param name="second">The second data slot to check.</param>
        /// <returns><see langword="false"/> if both data slots identifies the same data key.</returns>
        public static bool operator !=(UserDataSlot<V> first, UserDataSlot<V> second)
            => first.Id == second.Id;
    }
}