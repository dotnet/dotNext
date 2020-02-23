using System;
using System.Linq.Expressions;
using System.Reflection;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Linq.Expressions
{
    /// <summary>
    /// Expresses construction of <see cref="Index"/> value.
    /// </summary>
    public sealed class OffsetExpression : Expression
    {
        public OffsetExpression(Expression value, bool fromEnd = false)
        {
            IsFromEnd = fromEnd;
            Value = value;
        }

        /// <summary>
        /// Gets the offset value.
        /// </summary>
        /// <value>The offset value.</value>
        public Expression Value { get; }

        /// <summary>
        /// Gets a value that indicates whether the index is from the start or the end.
        /// </summary>
        /// <value><see langword="true"/> if the Index is from the end; otherwise, <see langword="false"/>.</value>
        public bool IsFromEnd { get; }

        /// <summary>
        /// Gets result type of asynchronous operation.
        /// </summary>
        public override Type Type => typeof(Index);

        /// <summary>
        /// Always return <see langword="true"/>.
        /// </summary>
        public override bool CanReduce => true;

        /// <summary>
        /// Gets expression node type.
        /// </summary>
        /// <see cref="ExpressionType.Extension"/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce()
        {
            ConstructorInfo? ctor = typeof(Index).GetConstructor(new []{ typeof(int), typeof(bool) });
            Debug.Assert(!(ctor is null));
            return New(ctor, Value, Constant(IsFromEnd));
        }

        /// <summary>
        /// Visit children expressions.
        /// </summary>
        /// <param name="visitor">Expression visitor.</param>
        /// <returns>Potentially modified expression if one of children expressions is modified during visit.</returns>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var expression = visitor.Visit(Value);
            return ReferenceEquals(expression, Value) ? this : new OffsetExpression(expression, IsFromEnd);
        }
    }
}