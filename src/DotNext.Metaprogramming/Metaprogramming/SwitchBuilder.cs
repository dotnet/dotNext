using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

using Seq = Collections.Generic.Sequence;

/// <summary>
/// Represents selection statement that chooses a single section to execute from a
/// list of candidates based on a pattern matching.
/// </summary>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/switch">switch statement</seealso>
public sealed class SwitchBuilder : ExpressionBuilder<SwitchExpression>
{
    private readonly Expression switchValue;
    private readonly ICollection<SwitchCase> cases;
    private Expression? defaultExpression;

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
    /// and action to be executed if matching is successful.
    /// </summary>
    /// <param name="testValues">A list of test values.</param>
    /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
    /// <returns>Modified selection builder.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public SwitchBuilder Case(IEnumerable<Expression> testValues, Action body)
        => Case(body => Case(testValues, body), body);

    /// <summary>
    /// Specifies a pattern to compare to the match expression
    /// and expression to be returned if matching is successful.
    /// </summary>
    /// <param name="test">Single test value.</param>
    /// <param name="body">The expression to be returned from selection statement.</param>
    /// <returns><c>this</c> builder.</returns>
    public SwitchBuilder Case(Expression test, Expression body) => Case(Seq.Singleton(test), body);

    /// <summary>
    /// Specifies a pattern to compare to the match expression
    /// and action to be executed if matching is successful.
    /// </summary>
    /// <param name="test">Single test value.</param>
    /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
    /// <returns>Modified selection builder.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public SwitchBuilder Case(Expression test, Action body)
        => Case(body => Case(test, body), body);

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

    /// <summary>
    /// Specifies the switch section to execute if the match expression
    /// doesn't match any other cases.
    /// </summary>
    /// <param name="body">The block code to be executed if input value is equal to one of test values.</param>
    /// <returns>Modified selection builder.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public SwitchBuilder Default(Action body) => Case(Default, body);

    private SwitchBuilder Case(Func<Expression, SwitchBuilder> @case, Action body)
    {
        using var statement = new CaseStatement(Default);
        return statement.Build(body);
    }

    private protected override SwitchExpression Build() => Expression.Switch(Type, switchValue, defaultExpression, null, cases);

    private protected override void Cleanup()
    {
        cases.Clear();
        defaultExpression = null;
        base.Cleanup();
    }

    private sealed class CaseStatement : Statement, ILexicalScope<SwitchBuilder, Action>
    {
        private readonly Func<Expression, SwitchBuilder> builder;

        internal CaseStatement(Func<Expression, SwitchBuilder> builder)
            => this.builder = builder;

        public SwitchBuilder Build(Action body)
        {
            body();
            return builder(Build());
        }
    }
}