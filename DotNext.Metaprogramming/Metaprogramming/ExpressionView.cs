using System;
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

        public static implicit operator Expression(ExpressionView view) => view.expression;

        public static implicit operator ParameterExpression(ExpressionView view) => view.expression as ParameterExpression;

        public static implicit operator ExpressionView(Expression expr) => new ExpressionView(expr);

        public static implicit operator ExpressionView(long value) => new ExpressionView(Expression.Constant(value, typeof(long)));

        public static ExpressionView operator+(ExpressionView expr) => expr.expression.UnaryPlus();
        
        public static ExpressionView operator-(ExpressionView expr) => expr.expression.Negate();
        
        public static ExpressionView operator+(ExpressionView left, ExpressionView right) => left.expression.Add(right);
        
        public static ExpressionView operator-(ExpressionView left, ExpressionView right) => left.expression.Subtract(right);

        public static ExpressionView operator>(ExpressionView left, ExpressionView right) => left.expression.GreaterThan(right);

        public static ExpressionView operator<(ExpressionView left, ExpressionView right) => left.expression.LessThan(right);

        public static ExpressionView operator*(ExpressionView left, ExpressionView right) => left.expression.Multiply(right);

        public override int GetHashCode() => expression.GetHashCode();

        public override bool Equals(object other)
        {
            switch(other)
            {
                case Expression expr:
                    return Equals(expression, expr);
                default:
                    return false;
            }
        }

        public override string ToString() => expression?.ToString();
    }
}
