using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents enhanced view of expression.
    /// </summary>
    /// <remarks>
    /// This class is intended for expression building purposes only.
    /// It cannot be stored in the field, or be a property type.
    /// </remarks>
    public readonly struct ExpressionView
    {
        private readonly Expression expression;

        public ExpressionView(Expression expr) => expression = expr ?? throw new ArgumentNullException(nameof(expr));

        public static implicit operator Expression(ExpressionView view) => view.expression ?? Expression.Empty();

        public static implicit operator ParameterExpression(ExpressionView view) 
            => view.expression is ParameterExpression parameter ? parameter : throw new InvalidCastException("Parameter expression expected");

        public static implicit operator ExpressionView(Expression expr) => new ExpressionView(expr);

        public static implicit operator ExpressionView(long value) => value.AsConst();
        [CLSCompliant(false)]
        public static implicit operator ExpressionView(ulong value) => value.AsConst();

        public static implicit operator ExpressionView(int value) => value.AsConst();
        [CLSCompliant(false)]
        public static implicit operator ExpressionView(uint value) => value.AsConst();

        public static implicit operator ExpressionView(short value) => value.AsConst();

        [CLSCompliant(false)]
        public static implicit operator ExpressionView(ushort value) => value.AsConst();

        public static implicit operator ExpressionView(byte value) => value.AsConst();

        [CLSCompliant(false)]
        public static implicit operator ExpressionView(sbyte value) => value.AsConst();

        public static implicit operator ExpressionView(bool value) => value.AsConst();

        public static implicit operator ExpressionView(string value) => value.AsConst();

        public static implicit operator ExpressionView(decimal value) => value.AsConst();

        public static implicit operator ExpressionView(float value) => value.AsConst();

        public static implicit operator ExpressionView(double value) => value.AsConst();

        public static implicit operator ExpressionView(DateTime value) => value.AsConst();

        public static ExpressionView operator !(ExpressionView expr) => expr.expression.Not();

        public static ExpressionView operator ~(ExpressionView expr) => expr.expression.OnesComplement();

        public static ExpressionView operator +(ExpressionView expr) => expr.expression.UnaryPlus();
        
        public static ExpressionView operator -(ExpressionView expr) => expr.expression.Negate();
        
        public static ExpressionView operator +(ExpressionView left, ExpressionView right) => left.expression.Add(right);
        
        public static ExpressionView operator -(ExpressionView left, ExpressionView right) => left.expression.Subtract(right);

        public static ExpressionView operator >(ExpressionView left, ExpressionView right) => left.expression.GreaterThan(right);

        public static ExpressionView operator >=(ExpressionView left, ExpressionView right) => left.expression.GreaterThanOrEqual(right);

        public static ExpressionView operator <(ExpressionView left, ExpressionView right) => left.expression.LessThan(right);

        public static ExpressionView operator <=(ExpressionView left, ExpressionView right) => left.expression.LessThanOrEqual(right);

        public static ExpressionView operator *(ExpressionView left, ExpressionView right) => left.expression.Multiply(right);

        public static ExpressionView operator /(ExpressionView left, ExpressionView right) => left.expression.Divide(right);

        public static ExpressionView operator |(ExpressionView left, ExpressionView right) => left.expression.Or(right);

        public static ExpressionView operator &(ExpressionView left, ExpressionView right) => left.expression.Divide(right);

        public static ExpressionView operator ^(ExpressionView left, ExpressionView right) => left.expression.Xor(right);

        public static ExpressionView operator ==(ExpressionView left, ExpressionView right) => left.expression.Equal(right);

        public static ExpressionView operator !=(ExpressionView left, ExpressionView right) => left.expression.NotEqual(right);

        public static ExpressionView operator %(ExpressionView left, ExpressionView right) => left.expression.Modulo(right);

        public static ExpressionView operator >>(ExpressionView left, int right) => left.expression.RightShift((ExpressionView)right);

        public static ExpressionView operator <<(ExpressionView left, int right) => left.expression.LeftShift((ExpressionView)right);

        [SpecialName]
        public static ExpressionView op_Exponent(ExpressionView left, ExpressionView right)
            => left.expression.Power(right);

        [SpecialName]
        public static ExpressionView op_LeftShift(ExpressionView left, ExpressionView right)
            => left.expression.LeftShift(right);

        [SpecialName]
        public static ExpressionView op_RightShift(ExpressionView left, ExpressionView right)
            => left.expression.RightShift(right);

        public ExpressionView Convert(Type type) => expression.Convert(type);

        public ExpressionView Convert<T>() => expression.Convert<T>();

        public ExpressionView InstanceOf(Type type) => expression.InstanceOf(type);

        public ExpressionView InstanceOf<T>() => expression.InstanceOf<T>();

        public ExpressionView TryConvert(Type type) => expression.TryConvert(type);

        public ExpressionView TryConvert<T>() => expression.TryConvert<T>();

        public ExpressionView PreDecrementAssign() => expression.PreDecrementAssign();

        public ExpressionView PostDecrementAssign() => expression.PostDecrementAssign();

        public ExpressionView OrElse(Expression other) => expression.OrElse(other);

        public ExpressionView AndAlso(Expression other) => expression.AndAlso(other);

        public ExpressionView Unbox(Type type) => expression.Unbox(type);

        public ExpressionView Unbox<T>()
            where T : struct
            => expression.Unbox<T>();

        public MethodCallExpression Call(MethodInfo method, params Expression[] arguments) => expression.Call(method, arguments);

        public MethodCallExpression Call(string methodName, params Expression[] arguments) => expression.Call(methodName, arguments);

        public MethodCallExpression Call(Type interfaceType, string methodName, params Expression[] arguments) => expression.Call(interfaceType, methodName, arguments);

        public Expression Property(PropertyInfo property, params Expression[] indicies) => expression.Property(property, indicies);

        public Expression Property(Type interfaceType, string propertyName, params Expression[] indicies) => expression.Property(interfaceType, propertyName, indicies);

        public Expression Property(string propertyName, params Expression[] indicies) => expression.Property(propertyName, indicies);
        
        public LoopExpression Loop(LabelTarget @break, LabelTarget @continue) => expression.Loop(@break, @continue);
        
        public LoopExpression Loop(LabelTarget @break) => expression.Loop(@break);
        
        public LoopExpression Loop() => expression.Loop();

        public ConditionalExpression Condition(Expression ifTrue = null, Expression ifFalse = null, Type type = null) 
            => expression.Condition(ifTrue, ifFalse, type);
        
        public ConditionalExpression Condition<R>(Expression ifTrue, Expression ifFalse)
            => expression.Condition<R>(ifTrue, ifFalse);

        public UnaryExpression Throw() => expression.Throw();

        public override int GetHashCode() => expression.GetHashCode();

        public override bool Equals(object other)
        {
            switch(other)
            {
                case Expression expr:
                    return Equals(expression, expr);
                case ExpressionView view:
                    return Equals(expression, view.expression);
                default:
                    return false;
            }
        }

        public override string ToString() => expression?.ToString();
    }
}
