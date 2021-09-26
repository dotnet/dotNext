using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

using ForEachExpression = Linq.Expressions.ForEachExpression;

internal sealed class ForEachStatement : LoopLexicalScope, ILexicalScope<ForEachExpression, Action<MemberExpression>>, ILexicalScope<ForEachExpression, Action<MemberExpression, LoopContext>>
{
    private readonly Expression collection;

    internal ForEachStatement(Expression collection) => this.collection = collection;

    ForEachExpression ILexicalScope<ForEachExpression, Action<MemberExpression>>.Build(Action<MemberExpression> scope)
    {
        var result = new ForEachExpression(collection, ContinueLabel, BreakLabel);
        scope(result.Element);
        result.Body = Build();
        return result;
    }

    ForEachExpression ILexicalScope<ForEachExpression, Action<MemberExpression, LoopContext>>.Build(Action<MemberExpression, LoopContext> scope)
    {
        var result = new ForEachExpression(collection, ContinueLabel, BreakLabel);
        using (var context = new LoopContext(result))
            scope(result.Element, context);
        result.Body = Build();
        return result;
    }
}