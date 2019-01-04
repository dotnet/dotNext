using System;

namespace Cheats
{
    using Reflection;

    [CLSCompliant(false)]
    [Serializable]
    public readonly struct Number<T>
        where T: struct, IConvertible, IComparable, IFormattable
    {
        private static readonly Operator<T, T> UnaryPlus = Type<T>.Operator.Require<T>(UnaryOperator.Plus, OperatorLookup.Predefined);
        private static readonly Operator<T, T> UnaryMinus = Type<T>.Operator.Require<T>(UnaryOperator.Negate, OperatorLookup.Predefined);
        

        private readonly T number;

        public Number(T value)
            => this.number = value;

        public static implicit operator Number<T>(T value)
            => new Number<T>(value);

        public static implicit operator T(Number<T> value)
            => value.number;

        public static Number<T> operator +(Number<T> other)
            => UnaryPlus(other);
        
        
    }
}