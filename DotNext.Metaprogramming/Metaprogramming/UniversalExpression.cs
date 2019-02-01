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

        public UniversalExpression(Expression expr) => expression = expr;

        internal static IEnumerable<Expression> AsExpressions(IEnumerable<UniversalExpression> expressions)
            => expressions.Select(Conversion<UniversalExpression, Expression>.Converter.AsFunc());

        internal static Expression[] AsExpressions(UniversalExpression[] expressions)
            => expressions.Convert(Conversion<UniversalExpression, Expression>.Converter);

        public static implicit operator Expression(UniversalExpression view) => view.expression ?? Expression.Empty();

        public static implicit operator ParameterExpression(UniversalExpression view) 
            => view.expression is ParameterExpression parameter ? parameter : throw new InvalidCastException("Parameter expression expected");

        public static implicit operator UniversalExpression(Expression expr) => new UniversalExpression(expr);

        public static implicit operator UniversalExpression(long value) => value.AsConst();
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(ulong value) => value.AsConst();

        public static implicit operator UniversalExpression(int value) => value.AsConst();
        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(uint value) => value.AsConst();

        public static implicit operator UniversalExpression(short value) => value.AsConst();

        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(ushort value) => value.AsConst();

        public static implicit operator UniversalExpression(byte value) => value.AsConst();

        [CLSCompliant(false)]
        public static implicit operator UniversalExpression(sbyte value) => value.AsConst();

        public static implicit operator UniversalExpression(bool value) => value.AsConst();

        public static implicit operator UniversalExpression(string value) => value.AsConst();

        public static implicit operator UniversalExpression(decimal value) => value.AsConst();

        public static implicit operator UniversalExpression(float value) => value.AsConst();

        public static implicit operator UniversalExpression(double value) => value.AsConst();

        public static implicit operator UniversalExpression(DateTime value) => value.AsConst();

        public static UniversalExpression operator !(UniversalExpression expr) => expr.expression.Not();

        public static UniversalExpression operator ~(UniversalExpression expr) => expr.expression.OnesComplement();

        public static UniversalExpression operator +(UniversalExpression expr) => expr.expression.UnaryPlus();
        
        public static UniversalExpression operator -(UniversalExpression expr) => expr.expression.Negate();
        
        public static UniversalExpression operator +(UniversalExpression left, UniversalExpression right) => left.expression.Add(right);
        
        public static UniversalExpression operator -(UniversalExpression left, UniversalExpression right) => left.expression.Subtract(right);

        public static UniversalExpression operator >(UniversalExpression left, UniversalExpression right) => left.expression.GreaterThan(right);

        public static UniversalExpression operator >=(UniversalExpression left, UniversalExpression right) => left.expression.GreaterThanOrEqual(right);

        public static UniversalExpression operator <(UniversalExpression left, UniversalExpression right) => left.expression.LessThan(right);

        public static UniversalExpression operator <=(UniversalExpression left, UniversalExpression right) => left.expression.LessThanOrEqual(right);

        public static UniversalExpression operator *(UniversalExpression left, UniversalExpression right) => left.expression.Multiply(right);

        public static UniversalExpression operator /(UniversalExpression left, UniversalExpression right) => left.expression.Divide(right);

        public static UniversalExpression operator |(UniversalExpression left, UniversalExpression right) => left.expression.Or(right);

        public static UniversalExpression operator &(UniversalExpression left, UniversalExpression right) => left.expression.Divide(right);

        public static UniversalExpression operator ^(UniversalExpression left, UniversalExpression right) => left.expression.Xor(right);

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

        public UniversalExpression OrElse(Expression other) => expression.OrElse(other);

        public UniversalExpression AndAlso(Expression other) => expression.AndAlso(other);

        public UniversalExpression Unbox(Type type) => expression.Unbox(type);

        public UniversalExpression Unbox<T>()
            where T : struct
            => expression.Unbox<T>();

        public InvocationExpression Invoke(params UniversalExpression[] arguments)
            => expression.Invoke(AsExpressions(arguments));

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
