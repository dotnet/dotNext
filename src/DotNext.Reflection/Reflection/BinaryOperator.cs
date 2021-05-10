using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents binary operator.
    /// </summary>
    [Serializable]
    public enum BinaryOperator
    {
        /// <summary>
        /// An addition operation, such as a + b, without overflow checking.
        /// </summary>
        Add = ExpressionType.Add,

        /// <summary>
        /// An addition operation, such as (a + b), with overflow checking.
        /// </summary>
        AddChecked = ExpressionType.AddChecked,

        /// <summary>
        /// An subtraction operation, such as (a - b), without overflow checking.
        /// </summary>
        Subtract = ExpressionType.Subtract,

        /// <summary>
        /// A subtraction operation, such as (a - b), with overflow checking.
        /// </summary>
        SubtractChecked = ExpressionType.SubtractChecked,

        /// <summary>
        /// a &amp; b
        /// </summary>
        And = ExpressionType.And,

        /// <summary>
        /// a | b
        /// </summary>
        Or = ExpressionType.Or,

        /// <summary>
        /// a / b
        /// </summary>
        Divide = ExpressionType.Divide,

        /// <summary>
        /// A multiply operation, such as (a * b), without overflow checking.
        /// </summary>
        Multiply = ExpressionType.Multiply,

        /// <summary>
        /// a &lt;&lt; b
        /// </summary>
        LeftShift = ExpressionType.LeftShift,

        /// <summary>
        /// a &gt;&gt; b
        /// </summary>
        RightShift = ExpressionType.RightShift,

        /// <summary>
        /// A multiply operation, such as (a * b), with overflow checking.
        /// </summary>
        MultiplyChecked = ExpressionType.MultiplyChecked,

        /// <summary>
        /// a % b
        /// </summary>
        Modulo = ExpressionType.Modulo,

        /// <summary>
        /// a == b
        /// </summary>
        Equal = ExpressionType.Equal,

        /// <summary>
        /// a != b
        /// </summary>
        NotEqual = ExpressionType.NotEqual,

        /// <summary>
        /// a ^ b
        /// </summary>
        Xor = ExpressionType.ExclusiveOr,

        /// <summary>
        /// a &gt; b
        /// </summary>
        GreaterThan = ExpressionType.GreaterThan,

        /// <summary>
        /// a &gt;= b
        /// </summary>
        GreaterThanOrEqual = ExpressionType.GreaterThanOrEqual,

        /// <summary>
        /// a &lt; b
        /// </summary>
        LessThan = ExpressionType.LessThan,

        /// <summary>
        /// a &lt;= b
        /// </summary>
        LessThanOrEqual = ExpressionType.LessThanOrEqual,

        /// <summary>
        /// POW(a, b)
        /// </summary>
        Power = ExpressionType.Power,
    }

    /// <summary>
    /// Represents reflected binary operator.
    /// </summary>
    /// <typeparam name="TOperand1">The type of the first operand.</typeparam>
    /// <typeparam name="TOperand2">The type of the second operand.</typeparam>
    /// <typeparam name="TResult">The type of the operator result.</typeparam>
    public sealed class BinaryOperator<TOperand1, TOperand2, TResult> : Operator<Operator<TOperand1, TOperand2, TResult>>
    {
        private sealed class Cache : Cache<BinaryOperator<TOperand1, TOperand2, TResult>>
        {
            private protected override BinaryOperator<TOperand1, TOperand2, TResult>? Create(Operator.Kind kind) => Reflect(kind);
        }

        private BinaryOperator(Expression<Operator<TOperand1, TOperand2, TResult>> invoker, BinaryOperator type, MethodInfo? overloaded)
            : base(invoker.Compile(), type.ToExpressionType(), overloaded)
        {
        }

        private protected override Type DeclaringType => typeof(TOperand1);

        /// <summary>
        /// Invokes binary operator.
        /// </summary>
        /// <param name="first">First operand.</param>
        /// <param name="second">Second operand.</param>
        /// <returns>Result of binary operator.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult? Invoke(in TOperand1 first, in TOperand2 second) => Invoker(in first, in second);

        /// <summary>
        /// Type of operator.
        /// </summary>
        public new BinaryOperator Type => (BinaryOperator)base.Type;

        private static Expression<Operator<TOperand1, TOperand2, TResult>>? MakeBinary(Operator.Kind @operator, Operator.Operand first, Operator.Operand second, out MethodInfo? overloaded)
        {
            var resultType = typeof(TResult);

            // perform automatic cast from byte/short/ushort/sbyte so binary operators become available for these types
            var usePrimitiveCast = resultType.IsPrimitive && first.NormalizePrimitive() && second.NormalizePrimitive();
        tail_call: // C# doesn't support tail calls so replace it with label/goto
            overloaded = null;
            try
            {
                var body = @operator.MakeBinary(first, second);
                overloaded = body.Method;
                return overloaded is null && usePrimitiveCast ?
                    Expression.Lambda<Operator<TOperand1, TOperand2, TResult>>(Expression.Convert(body, resultType), first.Source, second.Source) :
                    Expression.Lambda<Operator<TOperand1, TOperand2, TResult>>(body, first.Source, second.Source);
            }
            catch (ArgumentException e)
            {
                Debug.WriteLine(e);
                return null;
            }
            catch (InvalidOperationException)
            {
                if (second.Upcast())
                {
                    goto tail_call;
                }
                else if (first.Upcast())
                {
                    second = second.Source;
                    goto tail_call;
                }
                else
                {
                    return null;
                }
            }
        }

        private static BinaryOperator<TOperand1, TOperand2, TResult>? Reflect(Operator.Kind op)
        {
            var first = Expression.Parameter(typeof(TOperand1).MakeByRefType(), "first");
            var second = Expression.Parameter(typeof(TOperand2).MakeByRefType(), "second");
            var expr = MakeBinary(op, first, second, out var overloaded);
            return expr is null ? null : new BinaryOperator<TOperand1, TOperand2, TResult>(expr, op, overloaded);
        }

        private static BinaryOperator<TOperand1, TOperand2, TResult>? GetOrCreate(Operator.Kind op) => Cache.Of<Cache>(typeof(TOperand1)).GetOrCreate(op);

        internal static BinaryOperator<TOperand1, TOperand2, TResult>? GetOrCreate(BinaryOperator @operator, OperatorLookup lookup) => lookup switch
        {
            OperatorLookup.Predefined => GetOrCreate(new Operator.Kind(@operator, false)),
            OperatorLookup.Overloaded => GetOrCreate(new Operator.Kind(@operator, true)),
            OperatorLookup.Any => GetOrCreate(new Operator.Kind(@operator, true)) ?? GetOrCreate(new Operator.Kind(@operator, false)),
            _ => null,
        };
    }
}
