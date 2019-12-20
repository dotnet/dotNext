using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents unary operator.
    /// </summary>
    public enum UnaryOperator : int
    {
        /// <summary>
        /// A unary plus operation, such as (+a).
        /// </summary>
        Plus = ExpressionType.UnaryPlus,

        /// <summary>
        /// An arithmetic negation operation, such as (-a)
        /// </summary>
        Negate = ExpressionType.Negate,

        /// <summary>
        /// A cast or unchecked conversion operation.
        /// </summary>
        Convert = ExpressionType.Convert,

        /// <summary>
        /// A cast or checked conversion operation.
        /// </summary>
        ConvertChecked = ExpressionType.ConvertChecked,

        /// <summary>
        /// A bitwise complement or logical negation operation.
        /// </summary>
        Not = ExpressionType.Not,

        /// <summary>
        /// A ones complement operation.
        /// </summary>
        OnesComplement = ExpressionType.OnesComplement,

        /// <summary>
        /// A unary increment operation, such as (a + 1).
        /// </summary>
        Increment = ExpressionType.Increment,

        /// <summary>
        /// A unary decrement operation, such as (a - 1).
        /// </summary>
        Decrement = ExpressionType.Decrement,

        /// <summary>
        /// A type test, such as obj is T
        /// </summary>
        IsInstanceOf = ExpressionType.TypeIs,

        /// <summary>
        /// An exact type test.
        /// </summary>
        TypeTest = ExpressionType.TypeEqual,

        /// <summary>
        /// Safe typecast operation, such as obj as T
        /// </summary>
        TryConvert = ExpressionType.TypeAs,

        /// <summary>
        /// if(value)
        /// </summary>
        IsTrue = ExpressionType.IsTrue,

        /// <summary>
        /// if(!value)
        /// </summary>
        IsFalse = ExpressionType.IsFalse
    }

    /// <summary>
    /// Represents unary operator applicable to type <typeparamref name="T"/>.
    /// </summary>
	/// <typeparam name="T">Target type.</typeparam>
    /// <typeparam name="R">Type of unary operator result.</typeparam>
	[DefaultMember("Invoke")]
    public sealed class UnaryOperator<T, R> : Operator<Operator<T, R>>
    {
        private sealed class Cache : Cache<UnaryOperator<T, R>>
        {
            private protected override UnaryOperator<T, R> Create(Operator.Kind kind) => Reflect(kind);
        }

        private UnaryOperator(Expression<Operator<T, R>> invoker, UnaryOperator type, MethodInfo overloaded)
            : base(invoker.Compile(), type.ToExpressionType(), overloaded)
        {
        }

        /// <summary>
        /// Type of operator.
        /// </summary>
        public new UnaryOperator Type => (UnaryOperator)base.Type;

        /// <summary>
        /// Invokes unary operator.
        /// </summary>
        /// <param name="operand">An operand.</param>
        /// <returns>Result of unary operator.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public R Invoke(in T operand) => Invoker(in operand);

        private static Expression<Operator<T, R>> MakeUnary(Operator.Kind @operator, Operator.Operand operand, out MethodInfo overloaded)
        {
            var resultType = typeof(R);
            bool usePrimitiveCast;
            //perform automatic cast from byte/short/ushort/sbyte so unary operators become available for these types
            switch ((ExpressionType)@operator)
            {
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    usePrimitiveCast = false;
                    break;
                default:
                    usePrimitiveCast = resultType.IsPrimitive && operand.NormalizePrimitive();
                    break;
            }
            tail_call: //C# doesn't support tail calls so replace it with label/goto
            overloaded = null;
            try
            {
                var body = @operator.MakeUnary<R>(operand);
                overloaded = body.Method;
                if (overloaded is null && usePrimitiveCast)
                    body = Expression.Convert(body, resultType);
                return Expression.Lambda<Operator<T, R>>(body, operand.Source);
            }
            catch (ArgumentException e)
            {
                Debug.WriteLine(e);
                return null;
            }
            catch (InvalidOperationException)
            {
                //ignore exception
            }
            if (operand.Upcast())
                goto tail_call;
            else
                return null;
        }


        private static UnaryOperator<T, R> Reflect(Operator.Kind op)
        {
            var parameter = Expression.Parameter(typeof(T).MakeByRefType(), "operand");
            var result = MakeUnary(op, parameter, out var overloaded);
            if (result is null)
                return null;
            //handle situation when trying to cast two incompatible reference types
            else if (overloaded is null && (op == ExpressionType.Convert || op == ExpressionType.ConvertChecked) && !parameter.Type.IsValueType && !typeof(R).IsAssignableFrom(parameter.Type))
                return null;
            else
                return new UnaryOperator<T, R>(result, op, overloaded);
        }

        private static UnaryOperator<T, R> GetOrCreate(Operator.Kind op) => Cache.Of<Cache>(typeof(T)).GetOrCreate(op);

        internal static UnaryOperator<T, R> GetOrCreate(UnaryOperator @operator, OperatorLookup lookup)
        {
            switch (lookup)
            {
                case OperatorLookup.Predefined:
                    return GetOrCreate(new Operator.Kind(@operator, false));
                case OperatorLookup.Overloaded:
                    return GetOrCreate(new Operator.Kind(@operator, true));
                case OperatorLookup.Any:
                    return GetOrCreate(new Operator.Kind(@operator, true)) ?? GetOrCreate(new Operator.Kind(@operator, false));
                default:
                    return null;
            }
        }
    }
}
