using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

/// <summary>
/// Builder of conditional expression.
/// </summary>
public sealed class ConditionalBuilder : ExpressionBuilder<ConditionalExpression>
{
    private readonly Expression test;
    private Expression? ifTrue, ifFalse;

    internal ConditionalBuilder(Expression test, ILexicalScope currentScope)
        : base(currentScope) => this.test = test;

    /// <summary>
    /// Constructs positive branch of the conditional expression.
    /// </summary>
    /// <param name="branch">An expression representing positive branch.</param>
    /// <returns>Conditional expression builder.</returns>
    public ConditionalBuilder Then(Expression branch)
    {
        VerifyCaller();
        ifTrue = branch;
        return this;
    }

    /// <summary>
    /// Constructs positive branch of the conditional expression.
    /// </summary>
    /// <param name="body">Branch builder.</param>
    /// <returns>Conditional expression builder.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public ConditionalBuilder Then(Action body)
        => Branch(Then, body);

    /// <summary>
    /// Constructs negative branch of the conditional expression.
    /// </summary>
    /// <param name="branch">An expression representing negative branch.</param>
    /// <returns>Conditional expression builder.</returns>
    public ConditionalBuilder Else(Expression branch)
    {
        VerifyCaller();
        ifFalse = branch;
        return this;
    }

    /// <summary>
    /// Constructs negative branch of the conditional expression.
    /// </summary>
    /// <param name="body">Branch builder.</param>
    /// <returns>Conditional expression builder.</returns>
    /// <exception cref="InvalidOperationException">Attempts to call this method out of lexical scope.</exception>
    public ConditionalBuilder Else(Action body)
        => Branch(Else, body);

    private ConditionalBuilder Branch(Func<Expression, ConditionalBuilder> branch, Action body)
    {
        using var statement = new BranchStatement(branch);
        return statement.Build(body);
    }

    private protected override ConditionalExpression Build()
        => Expression.Condition(test, ifTrue ?? Expression.Empty(), ifFalse ?? Expression.Empty(), Type);

    private protected override void Cleanup()
    {
        ifTrue = ifFalse = null;
        base.Cleanup();
    }

    private sealed class BranchStatement : Statement, ILexicalScope<ConditionalBuilder, Action>
    {
        private readonly Func<Expression, ConditionalBuilder> branch;

        internal BranchStatement(Func<Expression, ConditionalBuilder> branch)
            => this.branch = branch;

        public ConditionalBuilder Build(Action body)
        {
            body();
            return branch(Build());
        }
    }
}