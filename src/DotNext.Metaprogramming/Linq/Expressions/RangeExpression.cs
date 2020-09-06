using System;
using System.Linq.Expressions;
using System.Reflection;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Linq.Expressions
{
    /// <summary>
    /// Expresses construction of <see cref="Range"/>.
    /// </summary>
    public sealed class RangeExpression : CustomExpression
    {
        /// <summary>
        /// Initializes a new range with the specified starting and ending indexes.
        /// </summary>
        /// <param name="start">The inclusive start index of the range.</param>
        /// <param name="end">The exclusive end index of the range.</param>
        public RangeExpression(ItemIndexExpression? start = null, ItemIndexExpression? end = null)
        {
            Start = start ?? ItemIndexExpression.First;
            End = end ?? ItemIndexExpression.Last;
        }

        /// <summary>
        /// Gets the inclusive start index of the range.
        /// </summary>
        /// <value>The inclusive start index of the range.</value>
        public ItemIndexExpression Start { get; }

        /// <summary>
        /// Gets the exclusive end index of the range.
        /// </summary>
        /// <value>The end index of the range.</value>
        public ItemIndexExpression End { get; }

        /// <summary>
        /// Gets result type of asynchronous operation.
        /// </summary>
        public override Type Type => typeof(Range);

        private static Expression GetOffsetAndLength(Expression range, Expression length)
            => Call(range, nameof(Range.GetOffsetAndLength), null, length);

        internal static Expression GetOffsetAndLength(Expression range, Expression length, out ParameterExpression offsetAndLength, out MemberExpression offsetField, out MemberExpression lengthField)
        {
            var result = GetOffsetAndLength(range, length);
            offsetAndLength = Variable(result.Type);
            offsetField = Field(offsetAndLength, nameof(ValueTuple<int, int>.Item1));
            lengthField = Field(offsetAndLength, nameof(ValueTuple<int, int>.Item2));
            return result;
        }

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce()
        {
            ConstructorInfo? ctor = typeof(Range).GetConstructor(new[] { typeof(Index), typeof(Index) });
            Debug.Assert(!(ctor is null));
            return New(ctor, Start.Reduce(), End.Reduce());
        }

        /// <summary>
        /// Visit children expressions.
        /// </summary>
        /// <param name="visitor">Expression visitor.</param>
        /// <returns>Potentially modified expression if one of children expressions is modified during visit.</returns>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var start = Start.Visit(visitor);
            var end = End.Visit(visitor);
            return ReferenceEquals(start, Start) && ReferenceEquals(end, End) ? this : new RangeExpression(start, end);
        }
    }
}