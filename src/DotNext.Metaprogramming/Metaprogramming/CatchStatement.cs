using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using DotNext.Linq.Expressions;
    using TryBuilder = Linq.Expressions.TryBuilder;

    internal readonly struct CatchStatement : IStatement<TryBuilder, Action<ParameterExpression>>, IStatement<TryBuilder, Action>
    {
        private readonly TryBuilder builder;
        private readonly ParameterExpression exception;
        private readonly Expression filter;

        internal CatchStatement(TryBuilder builder, Type exceptionType, TryBuilder.Filter filter = null)
        {
            this.builder = builder;
            exception = Expression.Variable(exceptionType, "e");
            this.filter = filter?.Invoke(exception);
        }

        TryBuilder IStatement<TryBuilder, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope, ILexicalScope body)
        {
            scope(exception);
            return builder.Catch(exception, filter, body.Build());
        }

        TryBuilder IStatement<TryBuilder, Action>.Build(Action scope, ILexicalScope body)
        {
            scope();
            return builder.Catch(exception, filter, body.Build());
        }
    }
}
