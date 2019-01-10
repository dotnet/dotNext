using System;

namespace Cheats
{
    using Reflection;
    
    /// <summary>
    /// Represents any primitive numeric type.
    /// </summary>
    /// <typeparam name="T">Primitive numeric type.</typeparam>
    [CLSCompliant(false)]
    [Serializable]
    public readonly struct Number<T>: IEquatable<T>
        where T: struct, IConvertible, IComparable, IFormattable
    {

        private static readonly Operator<T, T> UnaryPlus = Type<T>.Operator.Require<T>(UnaryOperator.Plus, OperatorLookup.Predefined);
        private static readonly Operator<T, T> UnaryMinus = Type<T>.Operator.Require<T>(UnaryOperator.Negate, OperatorLookup.Predefined);
        
        private static readonly Operator<T, T, T> BinaryPlus = Type<T>.Operator<T>.Require<T>(BinaryOperator.Add, OperatorLookup.Predefined);

        private static readonly Operator<T, T, bool> Equality = Type<T>.Operator<T>.Require<bool>(BinaryOperator.Equal, OperatorLookup.Predefined);

        private static readonly Function<(string text, Ref<T> result), bool> TryParseMethod = Type<T>.RequireStaticMethod<(string, Ref<T>), bool>(nameof(int.TryParse));

        private static readonly Func<string, T> ParseMethod = Type<T>.Method<string>.GetStatic<T>(nameof(int.Parse));

        private static readonly Operator<T, string> ToStringMethod = Type<T>.Method.Require<Operator<T, string>>(nameof(int.ToString), MemberLookup.Instance);

        private static readonly Operator<T, int> GetHashCodeMethod = Type<T>.Method.Require<Operator<T, int>>(nameof(int.GetHashCode), MemberLookup.Instance);

        private readonly T number;

        public Number(T value)
            => this.number = value;

        public bool Equals(T other) => Equality(in number, in other);
        
        public override string ToString() => ToStringMethod(in number);

        public override int GetHashCode() => GetHashCodeMethod(in number);

        public static implicit operator T(Number<T> value)
            => value.number;

        public static Number<T> operator +(Number<T> other)
            => new Number<T>(UnaryPlus(other));

        public static Number<T> operator+(Number<T> left, T right)
            => new Number<T>(BinaryPlus(in left.number, in right));
        
        public static bool TryParse(string text, out Number<T> value)
        {
            var args = TryParseMethod.ArgList();
            args.text = text;
            var success = TryParseMethod(args);
            value = new Number<T>(args.result);
            return success;
        }

        public static Number<T> Parse(string text) => new Number<T>(ParseMethod(text));
    }
}