using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents selection statement that chooses a single section to execute from a 
    /// list of candidates based on a pattern matching.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/switch">switch statement</seealso>
    public sealed class SwitchBuilder : ExpressionBuilder<SwitchExpression>
    {
        private readonly Expression switchValue;
        private readonly ICollection<SwitchCase> cases;
        private Expression defaultExpression;

        internal SwitchBuilder(Expression expression, LexicalScope parent, bool treatAsStatement)
            : base(parent, treatAsStatement)
        {
            cases = new LinkedList<SwitchCase>();
            defaultExpression = Expression.Empty();
            switchValue = expression;
        }

        /// <summary>
        /// Specifies a pattern to compare to the match expression
        /// and action to be executed if matching is successful.
        /// </summary>
        /// <param name="testValues">A list of test values.</param>
        /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public SwitchBuilder Case(IEnumerable<Expression> testValues, Action<ScopeBuilder> body)
        {
            using (var caseScope = NewScope())
                cases.Add(Expression.SwitchCase(caseScope.Build<Expression, ScopeBuilder>(body), testValues));
            return this;
        }

        /// <summary>
        /// Specifies a pattern to compare to the match expression
        /// and expression to be returned if matching is successful.
        /// </summary>
        /// <param name="testValues">A list of test values.</param>
        /// <param name="body">The expression to be returned from selection statement.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public SwitchBuilder Case(IEnumerable<UniversalExpression> testValues, UniversalExpression body)
        {
            cases.Add(Expression.SwitchCase(body, UniversalExpression.AsExpressions(testValues)));
            return this;
        }

        /// <summary>
        /// Specifies a pattern to compare to the match expression
        /// and action to be executed if matching is successful.
        /// </summary>
        /// <param name="testValues">A list of test values.</param>
        /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public SwitchBuilder Case(IEnumerable<UniversalExpression> testValues, Action<ScopeBuilder> body)
            => Case(UniversalExpression.AsExpressions(testValues), body);

        /// <summary>
        /// Specifies a pattern to compare to the match expression
        /// and action to be executed if matching is successful.
        /// </summary>
        /// <param name="test">Single test value.</param>
        /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public SwitchBuilder Case(UniversalExpression test, Action<ScopeBuilder> body)
            => Case(Sequence.Singleton((Expression)test), body);

        /// <summary>
        /// Specifies a pattern to compare to the match expression
        /// and expression to be returned if matching is successful.
        /// </summary>
        /// <param name="test">Single test value.</param>
        /// <param name="body">The expression to be returned from selection statement.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public SwitchBuilder Case(UniversalExpression test, UniversalExpression body)
            => Case(Sequence.Singleton(test), body);

        /// <summary>
        /// Specifies the switch section to execute if the match expression
        /// doesn't match any other cases.
        /// </summary>
        /// <param name="body">The expression to be returned from selection statement in default case.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public SwitchBuilder Default(UniversalExpression body)
        {
            defaultExpression = body;
            return this;
        }

        /// <summary>
        /// Specifies the switch section to execute if the match expression
        /// doesn't match any other cases.
        /// </summary>
        /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public SwitchBuilder Default(Action<ScopeBuilder> body)
        {
            using (var defaultScope = NewScope())
                return Default(defaultScope.Build<Expression, ScopeBuilder>(body));
        }

        private protected override SwitchExpression Build()
            => Expression.Switch(ExpressionType, switchValue, defaultExpression, null, cases);
    }
}
