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
    }
}