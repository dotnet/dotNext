using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

using ForExpression = Linq.Expressions.ForExpression;

internal sealed class ForStatement : LoopLexicalScope, ILexicalScope<ForExpression, Action<ParameterExpression>>, ILexicalScope<ForExpression, Action<ParameterExpression, LoopContext>>
{
    private readonly Action<ParameterExpression> iteration;
    private readonly ForExpression.LoopBuilder.Condition condition;
    private readonly Expression initialization;

    internal ForStatement(Expression initialization, ForExpression.LoopBuilder.Condition condition, Action<ParameterExpression> iteration)
    {
        this.iteration = iteration;
        this.condition = condition;
        this.initialization = initialization;
    }

    ForExpression ILexicalScope<ForExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope)
    {
        var result = new ForExpression(initialization, ContinueLabel, BreakLabel, condition);
        scope(result.LoopVar);
        AddStatement(Expression.Label(ContinueLabel));
        iteration(result.LoopVar);
        result.Body = Build();
        return result;
    }

    ForExpression ILexicalScope<ForExpression, Action<ParameterExpression, LoopContext>>.Build(Action<ParameterExpression, LoopContext> scope)
    {
        var result = new ForExpression(initialization, ContinueLabel, BreakLabel, condition);
        using (var context = new LoopContext(result))
            scope(result.LoopVar, context);
        AddStatement(Expression.Label(result.ContinueLabel));
        iteration(result.LoopVar);
        result.Body = Build();
        return result;
    }
}