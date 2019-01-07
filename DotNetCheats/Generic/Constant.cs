using System;

namespace Cheats.Generic
{
    /// <summary>
    /// Allows to use constant values as generic parameters.
    /// </summary>
    /// <remarks>
    /// Derived class must be sealed or abstract. If class is sealed
    /// then it should have at least one constructor without parameters.
    /// </remarks>
    /// <typeparam name="T">Type of constant to be passed as generic parameter.</typeparam>
    public abstract class Constant<T>
    {
        private static class Cache<G>
            where G: Constant<T>, new()
        {
            internal static readonly T Value = new G();
        }

        private readonly T Value;

        /// <summary>
        /// Initializes a new generic-level constant.
        /// </summary>
        /// <param name="constVal">Constant value.</param>
        protected Constant(T constVal)
        {
            Value = constVal;
        }

        public sealed override string ToString()
        {
            object boxed = Value;
            return boxed is null ? "NULL" : boxed.ToString();
        }

        public sealed override int GetHashCode()
        {
            object boxed = Value;
            return boxed is null ? 0 : boxed.GetHashCode();
        }

        public sealed override bool Equals(object other)
        {
            switch(other)
            {
                case T obj: return Equals(obj, Value);
                case Constant<T> @const: return Equals(Value, @const.Value);
                default: return false;
            }
        }

        public static implicit operator T(Constant<T> other) => other.Value;
        
        /// <summary>
        /// Extracts constant value from generic parameter.
        /// </summary>
        /// <param name="intern">True to return interned constant value; otherwise, false.</param>
        /// <typeparam name="G">A type representing a constant value.</typeparam>
        /// <returns>Constant value extracted from generic.</returns>
        public static T Of<G>(bool intern = false)
            where G: Constant<T>, new()
            => intern ? Cache<G>.Value : new G();
    }
}