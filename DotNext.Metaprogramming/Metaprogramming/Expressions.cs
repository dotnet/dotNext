using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public static class Expressions
    {
        public static UnaryExpression UnaryPlus(this Expression expression)
            => Expression.UnaryPlus(expression);

        public static UnaryExpression Negate(this Expression expression)
            => Expression.Negate(expression);

        public static UnaryExpression Not(this Expression expression)
            => Expression.Not(expression);

        public static UnaryExpression OnesComplement(this Expression expression)
            => Expression.OnesComplement(expression);

        public static BinaryExpression And(this Expression left, Expression right)
            => Expression.And(left, right);

        public static BinaryExpression Or(this Expression left, Expression right)
            => Expression.Or(left, right);

        public static BinaryExpression Xor(this Expression left, Expression right)
            => Expression.ExclusiveOr(left, right);

        public static BinaryExpression Modulo(this Expression left, Expression right)
            => Expression.Modulo(left, right);

        public static BinaryExpression Add(this Expression left, Expression right)
            => Expression.Add(left, right);

        public static BinaryExpression Subtract(this Expression left, Expression right)
            => Expression.Subtract(left, right);

        public static BinaryExpression Multiply(this Expression left, Expression right)
            => Expression.Multiply(left, right);

        public static BinaryExpression Divide(this Expression left, Expression right)
            => Expression.Divide(left, right);

        public static BinaryExpression GreaterThan(this Expression left, Expression right)
            => Expression.GreaterThan(left, right);

        public static BinaryExpression LessThan(this Expression left, Expression right)
            => Expression.LessThan(left, right);

        public static BinaryExpression GreaterThanOrEqual(this Expression left, Expression right)
            => Expression.GreaterThanOrEqual(left, right);

        public static BinaryExpression LessThanOrEqual(this Expression left, Expression right)
            => Expression.LessThanOrEqual(left, right);

        public static BinaryExpression Equal(this Expression left, Expression right)
            => Expression.Equal(left, right);

        public static BinaryExpression NotEqual(this Expression left, Expression right)
            => Expression.NotEqual(left, right);

        public static BinaryExpression Power(this Expression left, Expression right)
            => Expression.Power(left, right);

        public static BinaryExpression LeftShift(this Expression left, Expression right)
            => Expression.LeftShift(left, right);

        public static BinaryExpression RightShift(this Expression left, Expression right)
            => Expression.RightShift(left, right);

        public static UnaryExpression PreDecrementAssign(this Expression left)
            => Expression.PreDecrementAssign(left);

        public static UnaryExpression PostDecrementAssign(this Expression left)
            => Expression.PostDecrementAssign(left);

        public static UnaryExpression Convert(this Expression expression, Type targetType)
            => Expression.Convert(expression, targetType);

        public static UnaryExpression Convert<T>(this Expression expression)
            => expression.Convert(typeof(T));

        public static TypeBinaryExpression InstanceOf(this Expression expression, Type type)
            => Expression.TypeIs(expression, type);

        public static TypeBinaryExpression InstanceOf<T>(this Expression expression)
            => expression.InstanceOf(typeof(T));

        public static UnaryExpression TryConvert(this Expression expression, Type type)
            => Expression.TypeAs(expression, type);

        public static UnaryExpression TryConvert<T>(this Expression expression)
            => expression.TryConvert(typeof(T));

        public static BinaryExpression AndAlso(this Expression left, Expression right)
            => Expression.AndAlso(left, right);

        public static BinaryExpression OrElse(this Expression left, Expression right)
            => Expression.OrElse(left, right);

        public static UnaryExpression Unbox(this Expression expression, Type type)
            => Expression.Unbox(expression, type);

        public static UnaryExpression Unbox<T>(this Expression expression)
            where T: struct
            => expression.Unbox(typeof(T));
    }
}