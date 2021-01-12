using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DotNext
{
    using Reflection;
    using Runtime.CompilerServices;

    /// <summary>
    /// Represents any primitive numeric type.
    /// </summary>
    /// <remarks>
    /// This type demonstrates how to build concept type
    /// using method from type <see cref="Type{T}"/>
    /// </remarks>
    /// <typeparam name="T">Primitive numeric type.</typeparam>
    [CLSCompliant(false)]
    [Concept]
    [BeforeFieldInit(false)]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Number<T> : IEquatable<T>, IEquatable<Number<T>>, IComparable<T>
        where T : struct, IConvertible, IComparable<T>, IEquatable<T>, IFormattable
    {
        #region Concept Definition
        private static readonly Operator<T, T> UnaryPlus = Type<T>.Operator.Require<T>(UnaryOperator.Plus, OperatorLookup.Predefined);
        private static readonly Operator<T, T> UnaryMinus = Type<T>.Operator.Require<T>(UnaryOperator.Negate, OperatorLookup.Predefined);

        private static readonly Operator<T, T, T> BinaryPlus = Type<T>.Operator<T>.Require<T>(BinaryOperator.Add, OperatorLookup.Predefined);

        private static readonly Operator<T, T, T> BinaryMinus = Type<T>.Operator<T>.Require<T>(BinaryOperator.Subtract, OperatorLookup.Predefined);

        private static readonly Operator<T, T, T> Multiply = Type<T>.Operator<T>.Require<T>(BinaryOperator.Multiply, OperatorLookup.Predefined);

        private static readonly Operator<T, T, T> Divide = Type<T>.Operator<T>.Require<T>(BinaryOperator.Divide, OperatorLookup.Predefined);

        private static readonly Function<(string text, Ref<T> result), bool> TryParseMethod = Type<T>.RequireStaticMethod<(string, Ref<T>), bool>(nameof(int.TryParse));
        private static readonly Function<(string text, NumberStyles styles, IFormatProvider provider, Ref<T> result), bool> AdvancedTryParseMethod = Type<T>.RequireStaticMethod<(string, NumberStyles, IFormatProvider, Ref<T>), bool>(nameof(int.TryParse));

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
        public bool Equals(T other) => value.Equals(other);

        bool IEquatable<Number<T>>.Equals(Number<T> other) => Equals(other);

        int IComparable<T>.CompareTo(T other) => value.CompareTo(other);

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

        /// <summary>
        /// Arithmetic unary plus operation.
        /// </summary>
        /// <param name="operand">Unary plus operand.</param>
        /// <returns>The result of unary plus operation.</returns>
        public static Number<T> operator +(Number<T> operand)
            => new Number<T>(UnaryPlus(operand));

        /// <summary>
        /// Arithmetic unary minus operation.
        /// </summary>
        /// <param name="operand">Unary minus operand.</param>
        /// <returns>The result of unary minus operation.</returns>
        public static Number<T> operator -(Number<T> operand)
            => new Number<T>(UnaryMinus(operand));

        /// <summary>
        /// Arithmetic addition operation.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The result of addition.</returns>
        public static Number<T> operator +(Number<T> left, T right)
            => new Number<T>(BinaryPlus(in left.value, in right));

        /// <summary>
        /// Arithmetic subtraction operation.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The result of subtraction.</returns>
        public static Number<T> operator -(Number<T> left, T right)
            => new Number<T>(BinaryMinus(in left.value, in right));

        /// <summary>
        /// Arithmetic multiplication operation.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The result of multiplication operation.</returns>
        public static Number<T> operator *(Number<T> left, T right)
            => new Number<T>(Multiply(in left.value, in right));

        /// <summary>
        /// Arithmetic division operation.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The result of division operation.</returns>
        public static Number<T> operator /(Number<T> left, T right)
            => new Number<T>(Divide(in left.value, in right));

        /// <summary>
        /// Performs equality check.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/>, if two numbers are equal; otherwise, <see langword="false"/>.</returns>
		public static bool operator ==(Number<T> left, T right)
            => left.value.Equals(right);

        /// <summary>
        /// Performs inequality check.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/>, if two numbers are not equal; otherwise, <see langword="false"/>.</returns>
		public static bool operator !=(Number<T> left, T right)
            => !left.value.Equals(right);

        /// <summary>
        /// Determines whether the first number is greater than the second number.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/> if the first number is greater than the second number; otherwise, <see langword="false"/>.</returns>
        public static bool operator >(Number<T> left, T right) => left.value.CompareTo(right) > 0;

        /// <summary>
        /// Determines whether the first number is less than the second number.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/> if the first number is less than the second number; otherwise, <see langword="false"/>.</returns>
        public static bool operator <(Number<T> left, T right) => left.value.CompareTo(right) < 0;

        /// <summary>
        /// Determines whether the first number is greater than or equal to the second number.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/> if the first number is greater than or equal to the second number; otherwise, <see langword="false"/>.</returns>
        public static bool operator >=(Number<T> left, T right) => left.value.CompareTo(right) >= 0;

        /// <summary>
        /// Determines whether the first number is less than or equal to the second number.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/> if the first number is less than or equal to the second number; otherwise, <see langword="false"/>.</returns>
        public static bool operator <=(Number<T> left, T right) => left.value.CompareTo(right) < 0;

        /// <summary>
        /// Determines whether this number is equal to the specified number.
        /// </summary>
        /// <param name="other">The number to compare.</param>
        /// <returns><see langword="true"/>, if two numbers are equal; otherwise, <see langword="false"/>.</returns>
		public override bool Equals(object other)
        {
            switch (other)
            {
                case T number:
                    return Equals(number);
                case Number<T> number:
                    return Equals(number);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Converts the string representation of a number to its typed equivalent.  
        /// </summary>
        /// <param name="text">A string containing a number to convert</param>
        /// <param name="value">Converted number value.</param>
        /// <returns>Parsed number.</returns>
        public static bool TryParse(string text, out Number<T> value)
        {
            var args = TryParseMethod.ArgList();
            args.text = text;
            var success = TryParseMethod(args);
            value = new Number<T>(args.result);
            return success;
        }

        /// <summary>
        /// Converts the string representation of a number to its typed equivalent.  
        /// </summary>
        /// <param name="text">A string containing a number to convert.</param>
        /// <param name="styles">Style of the number supplied as a string.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="value">Converted number value.</param>
        /// <returns>Parsed number.</returns>
        public static bool TryParse(string text, NumberStyles styles, IFormatProvider provider, out Number<T> value)
        {
            var args = AdvancedTryParseMethod.ArgList();
            args.provider = provider;
            args.styles = styles;
            args.text = text;
            var success = AdvancedTryParseMethod(args);
            value = new Number<T>(args.result);
            return success;
        }

        /// <summary>
        /// Converts the string representation of a number to its typed equivalent. 
        /// </summary>
        /// <param name="text">A string containing a number to convert.</param>
        /// <returns>Parsed number.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in the correct format.</exception>
        public static Number<T> Parse(string text) => new Number<T>(ParseMethod(text));
    }
}