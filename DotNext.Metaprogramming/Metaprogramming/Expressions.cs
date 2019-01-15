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
        public static BinaryExpression Add(this Expression left, Expression right)
            => Expression.Add(left, right);

        public static BinaryExpression Subtract(this Expression left, Expression right)
            => Expression.Subtract(left, right);

        public static BinaryExpression Multiply(this Expression left, Expression right)
            => Expression.Multiply(left, right);

        public static BinaryExpression GreaterThan(this Expression left, Expression right)
            => Expression.GreaterThan(left, right);
        
        public static BinaryExpression LessThan(this Expression left, Expression right)
            => Expression.LessThan(left, right);

        public static UnaryExpression PreDecrementAssign(this Expression left)
            => Expression.PreDecrementAssign(left);
    }
}