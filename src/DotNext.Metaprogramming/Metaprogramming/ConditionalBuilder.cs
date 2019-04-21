using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Builder of conditional expression.
    /// </summary>
    public sealed class ConditionalBuilder: ExpressionBuilder<ConditionalExpression>
    {
        private readonly Expression test;
        private Expression ifTrue;
        private Expression ifFalse;

        internal ConditionalBuilder(Expression test, bool treatAsStatement)
            : base(treatAsStatement)
        {
            this.test = test;
            ifTrue = ifFalse = Expression.Empty();
        }

        /// <summary>
        /// Constructs positive branch of the conditional expression.
        /// </summary>
        /// <param name="branch">Branch builder.</param>
        /// <returns>Conditional expression builder.</returns>
        public ConditionalBuilder Then(Action branch)
        {
            using (var scope = new ScopeBuilder())
            {
                branch();
                ifTrue = scope.Build();
            }
            return this;
        }

        /// <summary>
        /// Constructs positive branch of the conditional expression.
        /// </summary>
        /// <param name="branch">An expression representing positive branch.</param>
        /// <returns>Conditional expression builder.</returns>
        public ConditionalBuilder Then(UniversalExpression branch)
        {
            ifTrue = branch;
            return this;
        }

        /// <summary>
        /// Constructs negative branch of the conditional expression.
        /// </summary>
        /// <param name="branch">Branch builder.</param>
        /// <returns>Conditional expression builder.</returns>
        public ConditionalBuilder Else(Action branch)
        {
            using (var scope = new ScopeBuilder())
            {
                branch();
                ifFalse = scope.Build();
            }
            return this;
        }

        /// <summary>
        /// Constructs negative branch of the conditional expression.
        /// </summary>
        /// <param name="branch">An expression representing negative branch.</param>
        /// <returns>Conditional expression builder.</returns>
        public ConditionalBuilder Else(UniversalExpression branch)
        {
            ifFalse = branch;
            return this;
        }

        private protected override ConditionalExpression Build() => Expression.Condition(test, ifTrue, ifFalse, ExpressionType);
    }
}