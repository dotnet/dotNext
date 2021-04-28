using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
    /// <typeparam name="TValue">The type of the value stored in user data slot.</typeparam>
    public readonly struct UserDataSlot<TValue> : IEquatable<UserDataSlot<TValue>>
    {
        /// <summary>
        /// Unique identifier of the data slot.
        /// </summary>
        private readonly long id;

        private UserDataSlot(long id) => this.id = id;

        /// <summary>
        /// Allocates a new data slot.
        /// </summary>
        /// <returns>Allocated data slot.</returns>
        public static UserDataSlot<TValue> Allocate() => new(UserDataSlot.NewId);

        [return: NotNullIfNotNull("defaultValue")]
        internal TValue? GetUserData(IDictionary<long, object?> storage, TValue? defaultValue)
            => storage.TryGetValue(id, out var userData) && userData is TValue result ? result : defaultValue;

        internal bool GetUserData(IDictionary<long, object?> storage, [MaybeNullWhen(false)] out TValue userData)
        {
            if (storage.TryGetValue(id, out var value) && value is TValue typedValue)
            {
                userData = typedValue;
                return true;
            }

            userData = default;
            return false;
        }

        internal void SetUserData(IDictionary<long, object?> storage, TValue userData)
        {
            if (id == 0)
                throw new ArgumentException(ExceptionMessages.InvalidUserDataSlot);
            storage[id] = userData;
        }

        internal bool RemoveUserData(IDictionary<long, object?> storage)
            => storage.Remove(id);

        internal bool RemoveUserData(Dictionary<long, object?> storage, [MaybeNullWhen(false)] out TValue userData)
        {
            if (storage.Remove(id, out var value) && value is TValue typedValue)
            {
                userData = typedValue;
                return true;
            }

            userData = default;
            return false;
        }

        /// <summary>
        /// Checks whether the two data slots are the same.
        /// </summary>
        /// <param name="other">Other data slot to check.</param>
        /// <returns><see langword="true"/> if both data slots identifies the same data key.</returns>
        public bool Equals(UserDataSlot<TValue> other) => id == other.id;

        /// <summary>
        /// Checks whether the two data slots are the same.
        /// </summary>
        /// <param name="other">Other data slot to check.</param>
        /// <returns><see langword="true"/> if both data slots identifies the same data key.</returns>
        public override bool Equals(object? other) => other is UserDataSlot<TValue> slot && Equals(slot);

        /// <summary>
        /// Computes hash code for this data slot.
        /// </summary>
        /// <returns>Hash code.</returns>
        public override int GetHashCode() => id.GetHashCode();

        /// <summary>
        /// Gets textual representation of this data slot
        /// useful for debugging.
        /// </summary>
        /// <returns>Textual representation of this data slot.</returns>
        public override string ToString() => id.ToString(default(IFormatProvider));

        /// <summary>
        /// Checks whether the two data slots are the same.
        /// </summary>
        /// <param name="first">The first data slot to check.</param>
        /// <param name="second">The second data slot to check.</param>
        /// <returns><see langword="true"/> if both data slots identifies the same data key.</returns>
        public static bool operator ==(UserDataSlot<TValue> first, UserDataSlot<TValue> second)
            => first.id == second.id;

        /// <summary>
        /// Checks whether the two data slots are not the same.
        /// </summary>
        /// <param name="first">The first data slot to check.</param>
        /// <param name="second">The second data slot to check.</param>
        /// <returns><see langword="false"/> if both data slots identifies the same data key.</returns>
        public static bool operator !=(UserDataSlot<TValue> first, UserDataSlot<TValue> second)
            => first.id != second.id;
    }
}