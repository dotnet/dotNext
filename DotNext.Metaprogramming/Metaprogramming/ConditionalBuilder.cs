using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Builder of conditional expression.
    /// </summary>
    public sealed class ConditionalBuilder: ExpressionOrStatementBuilder<ConditionalExpression>
    {
        private readonly Expression test;
        private Expression ifTrue;
        private Expression ifFalse;

        internal ConditionalBuilder(Expression test, ExpressionBuilder parent, bool treatAsStatement)
            : base(parent, treatAsStatement)
        {
            this.test = test;
            ifTrue = ifFalse = Expression.Empty();
        }

        /// <summary>
        /// Constructs positive branch of conditional expression.
        /// </summary>
        /// <param name="branch">Branch builder.</param>
        /// <returns>Conditional expression builder.</returns>
        public ConditionalBuilder Then(Action<ExpressionBuilder> branch)
        {
            ifTrue = NewScope().Build(branch);
            return this;
        }

        /// <summary>
        /// Constructs negative branch of conditional expression.
        /// </summary>
        /// <param name="branch">Branch builder.</param>
        /// <returns>Conditional expression builder.</returns>
        public ConditionalBuilder Else(Action<ExpressionBuilder> branch)
        {
            ifFalse = NewScope().Build(branch);
            return this;
        }

        private protected override ConditionalExpression Build() => Expression.Condition(test, ifTrue, ifFalse, ExpressionType);
    }
}