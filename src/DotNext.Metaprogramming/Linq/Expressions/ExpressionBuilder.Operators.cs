using System.Linq.Expressions;

namespace DotNext.Linq.Expressions;

partial class ExpressionBuilder
{
    /// <summary>
    /// Extends <see cref="Expression"/> type with operators.
    /// </summary>
    extension(Expression)
    {
        /// <summary>
        /// Constructs unary plus expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>+a</c>.
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression operator +(Expression expression)
            => Expression.UnaryPlus(expression);

        /// <summary>
        /// Constructs unchecked negate expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>-a</c>.
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression operator -(Expression expression)
            => Expression.Negate(expression);

        /// <summary>
        /// Constructs checked negate expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>checked(-a)</c>.
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression operator checked -(Expression expression)
            => Expression.NegateChecked(expression);

        /// <summary>
        /// Constructs logical NOT expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>!a</c>.
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression operator !(Expression expression)
            => Expression.Not(expression);

        /// <summary>
        /// Constructs ones complement.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>~a</c>.
        /// </remarks>
        /// <param name="expression">The operand.</param>
        /// <returns>Unary expression.</returns>
        public static UnaryExpression operator ~(Expression expression)
            => Expression.OnesComplement(expression);

        /// <summary>
        /// Constructs binary logical AND expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a &amp; b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator &(Expression left, Expression right)
            => Expression.And(left, right);

        /// <summary>
        /// Constructs binary logical OR expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a | b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator |(Expression left, Expression right)
            => Expression.Or(left, right);

        /// <summary>
        /// Constructs binary logical exclusive OR expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a ^ b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator ^(Expression left, Expression right)
            => Expression.ExclusiveOr(left, right);

        /// <summary>
        /// Constructs arithmetic remainder expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a % b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator %(Expression left, Expression right)
            => Expression.Modulo(left, right);

        /// <summary>
        /// Constructs binary arithmetic unchecked addition expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a + b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator +(Expression left, Expression right)
            => Expression.Add(left, right);

        /// <summary>
        /// Constructs binary arithmetic checked addition expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>checked(a + b)</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator checked +(Expression left, Expression right)
            => Expression.AddChecked(left, right);

        /// <summary>
        /// Constructs binary arithmetic unchecked subtraction expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a - b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator -(Expression left, Expression right)
            => Expression.Subtract(left, right);
        
        /// <summary>
        /// Constructs binary arithmetic checked subtraction expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>checked(a - b)</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator checked -(Expression left, Expression right)
            => Expression.SubtractChecked(left, right);
        
        /// <summary>
        /// Constructs binary arithmetic unchecked multiplication expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a * b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator *(Expression left, Expression right)
            => Expression.Multiply(left, right);

        /// <summary>
        /// Constructs binary arithmetic checked multiplication expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>checked(a * b)</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator checked *(Expression left, Expression right)
            => Expression.Multiply(left, right);

        /// <summary>
        /// Constructs binary arithmetic division expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a / b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator /(Expression left, Expression right)
            => Expression.Divide(left, right);

        /// <summary>
        /// Constructs "greater than" numeric comparison.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a &gt; b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator >(Expression left, Expression right)
            => Expression.GreaterThan(left, right);

        /// <summary>
        /// Constructs "less than" numeric comparison.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a &lt; b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator <(Expression left, Expression right)
            => Expression.LessThan(left, right);

        /// <summary>
        /// Constructs "greater than or equal" numeric comparison.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a &gt;= b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator >=(Expression left, Expression right)
            => Expression.GreaterThanOrEqual(left, right);

        /// <summary>
        /// Constructs "less than or equal" numeric comparison.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a &lt;= b</c>.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator <=(Expression left, Expression right)
            => Expression.LessThanOrEqual(left, right);

        /// <summary>
        /// Constructs bitwise left-shift expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a &lt;&lt; b</c> in Visual Basic.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator <<(Expression left, Expression right)
            => Expression.LeftShift(left, right);
        
        /// <summary>
        /// Constructs bitwise right-shift expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a &gt;&gt; b</c> in Visual Basic.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static BinaryExpression operator >>(Expression left, Expression right)
            => Expression.RightShift(left, right);

        /// <summary>
        /// Constructs bitwise unsigned right-shift expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a &gt;&gt;&gt; b</c> in Visual Basic.
        /// </remarks>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>Binary expression.</returns>
        public static UnsignedRightShiftExpression operator >>> (Expression left, Expression right)
            => new(left, right);
    }
}