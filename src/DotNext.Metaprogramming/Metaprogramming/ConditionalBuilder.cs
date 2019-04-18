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

        internal ConditionalBuilder(Expression test, CompoundStatementBuilder parent, bool treatAsStatement)
            : base(parent, treatAsStatement)
        {
            this.test = test;
            ifTrue = ifFalse = Expression.Empty();
        }

        /// <summary>
        /// Constructs positive branch of the conditional expression.
        /// </summary>
        /// <param name="branch">Branch builder.</param>
        /// <returns>Conditional expression builder.</returns>
        public ConditionalBuilder Then(Action<ScopeBuilder> branch)
        {
            using (var scope = NewScope())
                return Then(scope.Build<Expression, ScopeBuilder>(branch));
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
        public ConditionalBuilder Else(Action<ScopeBuilder> branch)
        {
            using (var scope = NewScope())
                return Else(scope.Build<Expression, ScopeBuilder>(branch));
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