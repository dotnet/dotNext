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

        internal SwitchBuilder(Expression expression, ILexicalScope currentScope)
            : base(currentScope)
        {
            cases = new LinkedList<SwitchCase>();
            defaultExpression = Expression.Empty();
            switchValue = expression;
        }

        /// <summary>
        /// Specifies a pattern to compare to the match expression
        /// and expression to be returned if matching is successful.
        /// </summary>
        /// <param name="testValues">A list of test values.</param>
        /// <param name="body">The expression to be returned from selection statement.</param>
        /// <returns><c>this</c> builder.</returns>
        public SwitchBuilder Case(IEnumerable<Expression> testValues, Expression body)
        {
            VerifyCaller();
            cases.Add(Expression.SwitchCase(body, testValues));
            return this;
        }

        /// <summary>
        /// Specifies a pattern to compare to the match expression
        /// and expression to be returned if matching is successful.
        /// </summary>
        /// <param name="test">Single test value.</param>
        /// <param name="body">The expression to be returned from selection statement.</param>
        /// <returns><c>this</c> builder.</returns>
        public SwitchBuilder Case(Expression test, Expression body) => Case(Sequence.Singleton(test), body);

        /// <summary>
        /// Specifies the switch section to execute if the match expression
        /// doesn't match any other cases.
        /// </summary>
        /// <param name="body">The expression to be returned from selection statement in default case.</param>
        /// <returns><c>this</c> builder.</returns>
        public SwitchBuilder Default(Expression body)
        {
            VerifyCaller();
            defaultExpression = body;
            return this;
        }

        private protected override SwitchExpression Build() => Expression.Switch(Type, switchValue, defaultExpression, null, cases);
    }
}
