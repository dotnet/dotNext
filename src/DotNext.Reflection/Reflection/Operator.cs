using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using static System.Diagnostics.Debug;

namespace DotNext.Reflection
{
    internal static class Operator
    {
        internal static ExpressionType ToExpressionType(this UnaryOperator @operator) => (ExpressionType)@operator;

        internal static ExpressionType ToExpressionType(this BinaryOperator @operator) => (ExpressionType)@operator;

        internal readonly struct Kind : IEquatable<Kind>
        {
            private sealed class EqualityComparer : IEqualityComparer<Kind>
            {
                public int GetHashCode(Kind key) => key.GetHashCode();

                public bool Equals(Kind first, Kind second) => first.Equals(second);
            }

            internal static IEqualityComparer<Kind> Comparer = new EqualityComparer();

            private readonly bool overloaded;
            private readonly ExpressionType operatorType;

            internal Kind(UnaryOperator operatorType, bool overloaded)
            {
                this.operatorType = operatorType.ToExpressionType();
                this.overloaded = overloaded;
            }

            public static implicit operator UnaryOperator(in Kind key) => (UnaryOperator)key.operatorType;

            public static implicit operator BinaryOperator(in Kind key) => (BinaryOperator)key.operatorType;

            public static implicit operator ExpressionType(in Kind key) => key.operatorType;

            internal Kind(BinaryOperator operatorType, bool overloaded)
            {
                this.operatorType = operatorType.ToExpressionType();
                this.overloaded = overloaded;
            }

            public bool Equals(Kind other) => operatorType == other.operatorType && overloaded == other.overloaded;

            public override bool Equals(object? other)
                => other is Kind key && Equals(key);

            public override int GetHashCode() => overloaded ? (int)operatorType + 100 : (int)operatorType;

            private InvalidOperationException OperatorNotExists()
                => new InvalidOperationException(ExceptionMessages.MissingOperator(operatorType));

            internal UnaryExpression MakeUnary<TResult>(in Operand operand)
            {
                var result = Expression.MakeUnary(operatorType, operand.Argument, typeof(TResult));
                return result.Method is null ^ overloaded ? result : throw OperatorNotExists();
            }

            internal BinaryExpression MakeBinary(in Operand first, in Operand second)
            {
                var result = Expression.MakeBinary(operatorType, first.Argument, second.Argument);
                return result.Method is null ^ overloaded ? result : throw OperatorNotExists();
            }
        }

        internal readonly ref struct Operand
        {
            internal readonly Expression Argument;
            internal readonly ParameterExpression Source;

            private Operand(ParameterExpression operand) => Argument = Source = operand;

            internal Operand(ParameterExpression supplier, Type expectedType) => Argument = Expression.Convert(Source = supplier, expectedType);

            public static implicit operator Operand(ParameterExpression operand) => new Operand(operand);
        }

        internal static bool Upcast(this ref Operand operand)
        {
            var baseType = operand.Argument.Type.BaseType;

            // do not walk through inheritance hierarchy for value types
            if (baseType is null || operand.Argument.Type.IsValueType)
            {
                return false;
            }
            else
            {
                operand = new Operand(operand.Source, baseType);
                return true;
            }
        }

        internal static bool NormalizePrimitive(this ref Operand operand)
        {
            switch (Type.GetTypeCode(operand.Argument.Type))
            {
                case TypeCode.Byte or TypeCode.UInt16 or TypeCode.SByte or TypeCode.Int16:
                    operand = new Operand(operand.Source, typeof(int));
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Abstract class for all reflected operators.
    /// </summary>
    /// <typeparam name="TSignature">Type of delegate representing operator signature.</typeparam>
    public abstract class Operator<TSignature> : IOperator<TSignature>
        where TSignature : Delegate
    {
        private protected abstract class Cache<TOperand> : Cache<Operator.Kind, TOperand>
            where TOperand : class, IOperator<TSignature>
        {
            private static readonly UserDataSlot<Cache<TOperand>> Slot = UserDataSlot<Cache<TOperand>>.Allocate();

            internal static Cache<TOperand> Of<TCache>(Type cacheHolder)
                where TCache : Cache<TOperand>, new()
                => cacheHolder.GetUserData().GetOrSet<Cache<TOperand>, TCache>(Slot);
        }

        private protected readonly TSignature Invoker;

        private protected Operator(TSignature invoker, ExpressionType type, MethodInfo? overloaded)
        {
            Type = type;
            Invoker = invoker;
            Method = overloaded;
        }

        /// <summary>
        /// Gets the implementing method for the operator.
        /// </summary>
        public MethodInfo? Method { get; }

        private protected abstract Type DeclaringType { get; }

        /// <inheritdoc/>
        MemberInfo IMember<MemberInfo>.RuntimeMember => Method ?? (MemberInfo)new BuiltinOperatorInfo(DeclaringType, Type);

        /// <inheritdoc/>
        TSignature IMember<MemberInfo, TSignature>.Invoker => Invoker;

        /// <inheritdoc/>
        string IMember<MemberInfo>.Name => Type.ToString();

        /// <summary>
        /// Returns the delegate instance that can be used to invoke operator.
        /// </summary>
        /// <param name="op">The reflected operator.</param>
        [return: NotNullIfNotNull("op")]
        public static implicit operator TSignature?(Operator<TSignature>? op) => op?.Invoker;

        /// <summary>
        /// Gets type of operator.
        /// </summary>
        public ExpressionType Type { get; }

        private static Expression<TSignature>? Convert(ParameterExpression parameter, Expression operand, Type conversionType, bool @checked)
        {
            try
            {
                return Expression.Lambda<TSignature>(@checked ? Expression.ConvertChecked(operand, conversionType) : Expression.Convert(operand, conversionType), parameter);
            }
            catch (ArgumentException e)
            {
                WriteLine(e);
                return null;
            }
            catch (InvalidOperationException)
            {
                // do not walk through inheritance hierarchy for value types
                if (parameter.Type.IsValueType)
                    return null;
                var lookup = operand.Type.BaseType;
                return lookup is null ? null : Convert(parameter, Expression.Convert(parameter, lookup), conversionType, @checked);
            }
        }

        private protected static Expression<TSignature>? MakeConvert<T>(ParameterExpression parameter, bool @checked) => Convert(parameter, parameter, typeof(T), @checked);

        /// <summary>
        /// Determines whether this object reflects the same operator as other object.
        /// </summary>
        /// <param name="other">Other reflected operator to be compared.</param>
        /// <returns><see langword="true"/>, if  this object reflects the same operator as other object; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other switch
        {
            Operator<TSignature> op => Type == op.Type && Method == op.Method,
            TSignature invoker => Equals(Invoker, invoker),
            _ => false,
        };

        /// <summary>
        /// Computes hash code of the reflected operator.
        /// </summary>
        /// <returns>The hash code of the reflected operator.</returns>
        public override int GetHashCode() => HashCode.Combine(typeof(TSignature), Type, Method);

        /// <summary>
        /// Returns textual representation of the reflected operator.
        /// </summary>
        /// <returns>The textual representation of the reflected operator.</returns>
        public override string ToString() => Type.ToString();
    }

    /// <summary>
    /// A delegate representing unary operator.
    /// </summary>
    /// <param name="operand">Operand.</param>
    /// <typeparam name="TOperand">Type of operand.</typeparam>
    /// <typeparam name="TResult">Type of operator result.</typeparam>
    /// <returns>Result of unary operation.</returns>
    public delegate TResult? Operator<TOperand, out TResult>(in TOperand operand);

    /// <summary>
    /// Represents binary operator.
    /// </summary>
    /// <param name="first">First operand.</param>
    /// <param name="second">Second operand.</param>
    /// <typeparam name="T1">Type of first operand.</typeparam>
    /// <typeparam name="T2">Type of second operand.</typeparam>
    /// <typeparam name="TResult">Type of operator result.</typeparam>
    /// <returns>Result of binary operator.</returns>
    public delegate TResult? Operator<T1, T2, out TResult>(in T1 first, in T2 second);
}