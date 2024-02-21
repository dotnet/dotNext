using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

using ForEachExpression = Linq.Expressions.ForEachExpression;

[RequiresUnreferencedCode("Dynamic access to GetAsyncEnumerator method and IAsyncEnumerable<T> interfaces.")]
internal sealed class AwaitForEachStatement : LoopLexicalScope, ILexicalScope<ForEachExpression, Action<MemberExpression>>, ILexicalScope<ForEachExpression, Action<MemberExpression, LoopContext>>
{
    private readonly Expression collection;
    private readonly Expression cancellationToken;
    private readonly bool configureAwait;

    internal AwaitForEachStatement(Expression collection, Expression? cancellationToken, bool configureAwait)
    {
        this.collection = collection;
        this.cancellationToken = cancellationToken ?? Expression.Default(typeof(CancellationToken));
        this.configureAwait = configureAwait;
    }

    ForEachExpression ILexicalScope<ForEachExpression, Action<MemberExpression>>.Build(Action<MemberExpression> scope)
    {
        var result = new ForEachExpression(collection, cancellationToken, configureAwait, ContinueLabel, BreakLabel);
        scope(result.Element);
        result.Body = Build();
        return result;
    }

    ForEachExpression ILexicalScope<ForEachExpression, Action<MemberExpression, LoopContext>>.Build(Action<MemberExpression, LoopContext> scope)
    {
        var result = new ForEachExpression(collection, cancellationToken, configureAwait, ContinueLabel, BreakLabel);
        using (var context = new LoopContext(result))
            scope(result.Element, context);
        result.Body = Build();
        return result;
    }
}
