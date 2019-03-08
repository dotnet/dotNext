using System;

namespace DotNext
{
    using Reflection;
    
    /// <summary>
    /// Represents any primitive numeric type.
    /// </summary>
    /// <remarks>
    /// This type demonstrates how to build concept type
    /// using method from type <see cref="Type{T}"/>
    /// </remarks>
    /// <typeparam name="T">Primitive numeric type.</typeparam>
    [CLSCompliant(false)]
    [Serializable]
    public readonly struct Number<T>: IEquatable<T>, IConcept<T>
        where T: struct, IConvertible, IComparable, IFormattable
    {
		#region Concept Definition
		private static readonly Operator<T, T> UnaryPlus = Type<T>.Operator.Require<T>(UnaryOperator.Plus, OperatorLookup.Predefined);
        private static readonly Operator<T, T> UnaryMinus = Type<T>.Operator.Require<T>(UnaryOperator.Negate, OperatorLookup.Predefined);
        
        private static readonly Operator<T, T, T> BinaryPlus = Type<T>.Operator<T>.Require<T>(BinaryOperator.Add, OperatorLookup.Predefined);

        private static readonly Operator<T, T, T> BinaryMinus = Type<T>.Operator<T>.Require<T>(BinaryOperator.Subtract, OperatorLookup.Predefined);

        private static readonly Operator<T, T, T> Multiply = Type<T>.Operator<T>.Require<T>(BinaryOperator.Multiply, OperatorLookup.Predefined);

        private static readonly Operator<T, T, T> Divide = Type<T>.Operator<T>.Require<T>(BinaryOperator.Divide, OperatorLookup.Predefined);
        
        private static readonly Operator<T, T, bool> Equality = Type<T>.Operator<T>.Require<bool>(BinaryOperator.Equal, OperatorLookup.Predefined);
		private static readonly Operator<T, T, bool> Inequality = Type<T>.Operator<T>.Require<bool>(BinaryOperator.NotEqual, OperatorLookup.Predefined);

        private static readonly Function<(string text, Ref<T> result), bool> TryParseMethod = Type<T>.RequireStaticMethod<(string, Ref<T>), bool>(nameof(int.TryParse));

        private static readonly Func<string, T> ParseMethod = Type<T>.Method<string>.GetStatic<T>(nameof(int.Parse));

        private static readonly Operator<T, string> ToStringMethod = Type<T>.Method.Require<Operator<T, string>>(nameof(int.ToString), MethodLookup.Instance);

        private static readonly Operator<T, int> GetHashCodeMethod = Type<T>.Method.Require<Operator<T, int>>(nameof(int.GetHashCode), MethodLookup.Instance);
		#endregion

		private readonly T value;

        /// <summary>
        /// Initializes a new generic numeric value.
        /// </summary>
        /// <param name="value">Underlying numeric value.</param>
        public Number(T value)
            => this.value = value;

        /// <summary>
        /// Determines whether this number is equal to another.
        /// </summary>
        /// <param name="other">Other number to be compared.</param>
        /// <returns><see langword="true"/> if this number is equal to the given number; otherwise, <see langword="false"/>.</returns>
        public bool Equals(T other) => Equality(in value, in other);
        
        /// <summary>
        /// Converts the number into string.
        /// </summary>
        /// <returns>The textual representation of the number.</returns>
        public override string ToString() => ToStringMethod(in value);

        /// <summary>
        /// Computes hash code of the number.
        /// </summary>
        /// <returns>Number hash code.</returns>
        public override int GetHashCode() => GetHashCodeMethod(in value);

        /// <summary>
        /// Converts container instance into the underlying numeric type.
        /// </summary>
        /// <param name="value">The container instance to be converted.</param>
        public static implicit operator T(Number<T> value)
            => value.value;

        public static Number<T> operator +(Number<T> other)
            => new Number<T>(UnaryPlus(other));

        public static Number<T> operator+(Number<T> left, T right)
            => new Number<T>(BinaryPlus(in left.value, in right));
        
        public static Number<T> operator-(Number<T> left, T right)
            => new Number<T>(BinaryMinus(in left.value, in right));
        
        public static Number<T> operator *(Number<T> left, T right)
            => new Number<T>(Multiply(in left.value, in right));
        
        public static Number<T> operator /(Number<T> left, T right)
            => new Number<T>(Divide(in left.value, in right));

		public static bool operator ==(Number<T> left, T right)
			=> Equality(in left.value, right);

		public static bool operator !=(Number<T> left, T right)
			=> Inequality(in left.value, right);

		public override bool Equals(object other)
		{
			switch(other)
			{
				case T number:
					return Equals(number);
				case Number<T> number:
					return Equals(number);
				default:
					return false;
			}
		}
        
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