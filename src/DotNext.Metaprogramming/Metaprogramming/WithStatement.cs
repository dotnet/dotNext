using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

using WithExpression = Linq.Expressions.WithExpression;

internal sealed class WithStatement : Statement, ILexicalScope<WithExpression, Action<ParameterExpression>>
{
    private readonly Expression obj;

    internal WithStatement(Expression obj) => this.obj = obj;

    WithExpression ILexicalScope<WithExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope)
    {
        var result = new WithExpression(obj);
        scope(result.Variable);
        result.Body = Build();
        return result;
    }
}