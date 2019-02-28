using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Reflection;

    /// <summary>
    /// Represents any expression with full support
    /// of overloaded operators and conversion from
    /// primitive data types.
    /// </summary>
    /// <remarks>
    /// This class is intended for expression building purposes only.
    /// </remarks>
    public readonly struct UniversalExpression
    {
        private readonly Expression expression;

        /// <summary>
        /// Wraps regular LINQ expression into universal expression.
        /// </summary>
        /// <param name="expr">An expression to be wrapped.</param>
        public UniversalExpression(Expression expr) => expression = expr;

        internal static IEnumerable<Expression> AsExpressions(IEnumerable<UniversalExpression> expressions)
            => expressions.Select(Conversion<UniversalExpression, Expression>.Converter.AsFunc());

        internal static Expression[] AsExpressions(UniversalExpression[] expressions)
            => expressions.Convert(Conversion<UniversalExpression, Expression>.Converter);

        /// <summary>
        /// Converts universal expression into regular LINQ expression.
        /// </summary>
        /// <param name="view">Universal expression to be converted.</param>
        public static implicit operator Expression(UniversalExpression view) => view.expression ?? Expression.Empty();

        /// <summary>
        /// Converts universal expression into variable or parameter expression.
        /// </summary>
        /// <param name="view">Universal expression to be converted.</param>
        public static implicit operator ParameterExpression(UniversalExpression view) 
            => view.expression is ParameterExpression parameter ? parameter : throw new InvalidCastException(ExceptionMessages.ParameterExpected);

        /// <summary>
        /// Converts regular LINQ expression into universal expression.
        /// </summary>
        /// <param name="expr">Regular LINQ expression to be wrapped.</param>
        public static implicit operator UniversalExpression(Expression expr) => new UniversalExpression(expr);

        /// <summary>
        /// Constructs constant value of <see cref="long"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(long value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="ulong"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(ulong value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="int"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(int value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="uint"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(uint value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="short"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(short value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="ushort"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(ushort value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="byte"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(byte value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="sbyte"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(sbyte value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="bool"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(bool value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="string"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(string value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="decimal"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(decimal value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="float"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(float value) => value.AsConst();

        /// <summary>
        /// Constructs constant value of <see cref="double"/> type.
        /// </summary>
        /// <param name="value">The constant value.</param>
        public static implicit operator UniversalExpression(double value) => value.AsConst();

        /// <summary>
        /// Logical NOT expression.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Constructed logical NOT expression.</returns>
        public static UniversalExpression operator !(UniversalExpression expr) => expr.expression.Not();

        /// <summary>
        /// Ones complement.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Ones complement expression.</returns>
        public static UniversalExpression operator ~(UniversalExpression expr) => expr.expression.OnesComplement();

        /// <summary>
        /// Unary plus.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UniversalExpression operator +(UniversalExpression expr) => expr.expression.UnaryPlus();
        
        /// <summary>
        /// Unary minus.
        /// </summary>
        /// <param name="expr">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UniversalExpression operator -(UniversalExpression expr) => expr.expression.Negate();

        /// <summary>
        /// Binary arithmetic addition expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator +(UniversalExpression left, UniversalExpression right) => left.expression.Add(right);

        /// <summary>
        /// Binary arithmetic subtraction expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator -(UniversalExpression left, UniversalExpression right) => left.expression.Subtract(right);

        /// <summary>
        /// "greater than" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator >(UniversalExpression left, UniversalExpression right) => left.expression.GreaterThan(right);

        /// <summary>
        /// "greater than or equal" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator >=(UniversalExpression left, UniversalExpression right) => left.expression.GreaterThanOrEqual(right);

        /// <summary>
        /// "less than" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator <(UniversalExpression left, UniversalExpression right) => left.expression.LessThan(right);

        /// <summary>
        /// "less than or equal" numeric comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator <=(UniversalExpression left, UniversalExpression right) => left.expression.LessThanOrEqual(right);

        /// <summary>
        /// Binary arithmetic multiplication expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator *(UniversalExpression left, UniversalExpression right) => left.expression.Multiply(right);

        /// <summary>
        /// Binary arithmetic division expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator /(UniversalExpression left, UniversalExpression right) => left.expression.Divide(right);

        /// <summary>
        /// Binary logical OR expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator |(UniversalExpression left, UniversalExpression right) => left.expression.Or(right);

        /// <summary>
        /// Binary logical AND expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator &(UniversalExpression left, UniversalExpression right) => left.expression.Divide(right);

        /// <summary>
        /// Binary logical exclusive OR expression.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator ^(UniversalExpression left, UniversalExpression right) => left.expression.Xor(right);

        /// <summary>
        /// Equality comparison.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UniversalExpression operator ==(UniversalExpression left, UniversalExpression right) => left.expression.Equal(right);

        public static UniversalExpression operator !=(UniversalExpression left, UniversalExpression right) => left.expression.NotEqual(right);

        public static UniversalExpression operator %(UniversalExpression left, UniversalExpression right) => left.expression.Modulo(right);

        public static UniversalExpression operator >>(UniversalExpression left, int right) => left.expression.RightShift(right.AsConst());

        public static UniversalExpression operator <<(UniversalExpression left, int right) => left.expression.LeftShift(right.AsConst());

        [SpecialName]
        public static UniversalExpression op_Exponent(UniversalExpression left, UniversalExpression right)
            => left.expression.Power(right);

        [SpecialName]
        public static UniversalExpression op_LeftShift(UniversalExpression left, UniversalExpression right)
            => left.expression.LeftShift(right);

        [SpecialName]
        public static UniversalExpression op_RightShift(UniversalExpression left, UniversalExpression right)
            => left.expression.RightShift(right);

        public UniversalExpression Await() => expression.Await();

        public UniversalExpression Convert(Type type) => expression.Convert(type);

        public UniversalExpression Convert<T>() => expression.Convert<T>();

        public UniversalExpression InstanceOf(Type type) => expression.InstanceOf(type);

        public UniversalExpression InstanceOf<T>() => expression.InstanceOf<T>();

        public UniversalExpression TryConvert(Type type) => expression.TryConvert(type);

        public UniversalExpression TryConvert<T>() => expression.TryConvert<T>();

        public UniversalExpression PreDecrementAssign() => expression.PreDecrementAssign();

        public UniversalExpression PostDecrementAssign() => expression.PostDecrementAssign();

        public UniversalExpression PreIncrementAssign() => expression.PreIncrementAssign();

        public UniversalExpression PostIncrementAssign() => expression.PostIncrementAssign();

        public UniversalExpression OrElse(Expression other) => expression.OrElse(other);

        public UniversalExpression AndAlso(Expression other) => expression.AndAlso(other);

        public UniversalExpression Unbox(Type type) => expression.Unbox(type);

        public UniversalExpression Unbox<T>()
            where T : struct
            => expression.Unbox<T>();

        public InvocationExpression Invoke(params UniversalExpression[] arguments)
            => expression.Invoke(AsExpressions(arguments));

        public UniversalExpression ElementAt(params UniversalExpression[] indexes) => expression.ElementAt(AsExpressions(indexes));

        public UniversalExpression ArrayLength() => expression.ArrayLength();

        public UniversalExpression Call(MethodInfo method, params UniversalExpression[] arguments) => expression.Call(method, AsExpressions(arguments));

        public UniversalExpression Call(string methodName, params UniversalExpression[] arguments) => expression.Call(methodName, AsExpressions(arguments));

        public UniversalExpression Call(Type interfaceType, string methodName, params UniversalExpression[] arguments) => expression.Call(interfaceType, methodName, AsExpressions(arguments));

        public UniversalExpression Property(PropertyInfo property, params UniversalExpression[] indicies) => expression.Property(property, AsExpressions(indicies));

        public UniversalExpression Property(Type interfaceType, string propertyName, params UniversalExpression[] indicies) => expression.Property(interfaceType, propertyName, AsExpressions(indicies));

        public UniversalExpression Property(string propertyName, params UniversalExpression[] indicies) => expression.Property(propertyName, AsExpressions(indicies));

        public UniversalExpression Field(FieldInfo field) => expression.Field(field);

        public UniversalExpression Field(string fieldName) => expression.Field(fieldName);

        public UniversalExpression Loop(LabelTarget @break, LabelTarget @continue) => expression.Loop(@break, @continue);
        
        public UniversalExpression Loop(LabelTarget @break) => expression.Loop(@break);
        
        public UniversalExpression Loop() => expression.Loop();

        public UniversalExpression Condition(Expression ifTrue = null, Expression ifFalse = null, Type type = null) 
            => expression.Condition(ifTrue, ifFalse, type);
        
        public UniversalExpression Condition<R>(Expression ifTrue, Expression ifFalse)
            => expression.Condition<R>(ifTrue, ifFalse);

        public ConditionalBuilder Condition(ExpressionBuilder parent = null)
            => expression.Condition(parent);

        public UniversalExpression With(Action<WithBlockBuilder> scope, ExpressionBuilder parent = null) => expression.With(scope, parent);

        public UniversalExpression Using(Action<UsingBlockBuilder> scope, ExpressionBuilder parent)
            => expression.Using(scope, parent);

        public SwitchBuilder Switch(ExpressionBuilder parent = null)
            => new SwitchBuilder(expression, parent, false);

        public TryBuilder Try(ExpressionBuilder parent = null) => expression.Try(parent);

        public UnaryExpression Throw() => expression.Throw();

        public override int GetHashCode() => expression.GetHashCode();

        public override bool Equals(object other)
        {
            switch(other)
            {
                case Expression expr:
                    return Equals(expression, expr);
                case UniversalExpression view:
                    return Equals(expression, view.expression);
                default:
                    return false;
            }
        }

        public override string ToString() => expression?.ToString();
    }
}
