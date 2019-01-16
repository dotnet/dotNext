using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Builder of conditional expression.
    /// </summary>
    public sealed class ConditionalBuilder
    {
        private readonly Expression test;
        private readonly ExpressionBuilder parentScope;
        private readonly bool treatAsStatement;
        private Expression ifTrue;
        private Expression ifFalse;

        internal ConditionalBuilder(Expression test, ExpressionBuilder parent, bool treatAsStatement)
        {
            this.treatAsStatement = treatAsStatement;
            parentScope = parent;
            this.test = test;
            ifTrue = ifFalse = Expression.Empty();
        }

        private Expression Branch(Action<ExpressionBuilder> branch)
        {
            var branchScope = new ExpressionBuilder(parentScope);
            branch(branchScope);
            return branchScope.Build();
        }

        /// <summary>
        /// Constructs positive branch of conditional expression.
        /// </summary>
        /// <param name="branch">Branch builder.</param>
        /// <returns>Conditional expression builder.</returns>
        public ConditionalBuilder Then(Action<ExpressionBuilder> branch)
        {
            ifTrue = Branch(branch);
            return this;
        }

        /// <summary>
        /// Constructs negative branch of conditional expression.
        /// </summary>
        /// <param name="branch">Branch builder.</param>
        /// <returns>Conditional expression builder.</returns>
        public ConditionalBuilder Else(Action<ExpressionBuilder> branch)
        {
            ifFalse = Branch(branch);
            return this;
        }

        private ConditionalExpression Build(Type type) => Expression.Condition(test, ifTrue, ifFalse, type ?? typeof(void));

        /// <summary>
        /// Builds conditional statement of the specified type.
        /// </summary>
        /// <param name="type">Type of conditional expression; or <see langword="null"/> to use <see cref="Void"/> type.</param>
        /// <returns>Constructed conditional expression.</returns>
        public ConditionalExpression EndIf(Type type = null)
        {
            var condition = Build(type);
            if(treatAsStatement)
                parentScope.AddStatement(condition);
            return condition;
        }
    }
}