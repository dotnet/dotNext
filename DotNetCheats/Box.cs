using System;
using System.Collections.Generic;

namespace MissingPieces
{
    /// <summary>
    /// Various extensions for boxed value types.
    /// </summary>
    public static class Box
    {
        public static bool Equals<T>(this Box<T> value, T other)
            where T: struct, IEquatable<T>
            => value.GetPinnableReference().Equals(other);

        public static bool Equals<T>(this Box<T> value, Box<T> other)
            where T: struct, IEquatable<T>
            => !(other is null) && value.GetPinnableReference().Equals(other.GetPinnableReference());
    }

    /// <summary>
    /// Boxed representation of value type.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    [Serializable]
    public sealed class Box<T>
        where T: struct
    {
        private readonly T value;

        /// <summary>
        /// Creates boxed representation of value type.
        /// </summary>
        /// <param name="value">Value to be placed onto heap.</param>
        public Box(T value) => this.value = value;

        /// <summary>
        /// Unbox value type.
        /// </summary>
        /// <returns>Unboxed value type.</returns>
        public T Unbox() => value;

        /// <summary>
        /// Gets pinnable reference to boxed value type.
        /// </summary>
        /// <returns>Managed reference to boxed value type.</returns>
        public ref readonly T GetPinnableReference() => ref value;

        /// <summary>
        /// Unbox value type.
        /// </summary>
        /// <param name="boxed">Boxed value type.</param>
        public static implicit operator T(Box<T> boxed) => boxed.Unbox();

        public override string ToString() => value.ToString();

        public override int GetHashCode() => value.GetHashCode();

        public int GetHashCode(IEqualityComparer<T> comparer) => comparer.GetHashCode(value);

        public bool Equals(Box<T> other, IEqualityComparer<T> comparer)
            => !(other is null) && Equals(other.value, comparer);

        public bool Equals(T other, IEqualityComparer<T> comparer)
            => comparer.Equals(value, other);

        public override bool Equals(object other)
        {
            switch(other)
            {
                case T value: return this.value.Equals(value);
                case Box<T> boxed: return this.value.Equals(boxed.value);
                default: return false;
            }
        }
    }
}