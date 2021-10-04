using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

internal sealed class CatchStatement : Statement, ILexicalScope<TryBuilder, Action<ParameterExpression>>, ILexicalScope<TryBuilder, Action>
{
    private readonly TryBuilder builder;
    private readonly ParameterExpression exception;
    private readonly Expression? filter;

    internal CatchStatement(TryBuilder builder, Type? exceptionType = null, TryBuilder.Filter? filter = null)
    {
        this.builder = builder;
        exception = Expression.Variable(exceptionType ?? typeof(Exception), "e");
        this.filter = filter?.Invoke(exception);
    }

    public TryBuilder Build(Action<ParameterExpression> scope)
    {
        scope(exception);
        return builder.Catch(exception, filter, Build());
    }

    public TryBuilder Build(Action scope)
    {
        scope();
        return builder.Catch(exception, filter, Build());
    }
}