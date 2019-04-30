using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class CatchStatement : Statement, ILexicalScope<TryBuilder, Action<ParameterExpression>>, ILexicalScope<TryBuilder, Action>
    {
        internal readonly struct Factory : IFactory<CatchStatement>
        {
            private readonly TryBuilder builder;
            private readonly Type exceptionType;
            private readonly TryBuilder.Filter filter;

            internal Factory(TryBuilder builder, Type exceptionType, TryBuilder.Filter filter)
            {
                this.builder = builder;
                this.exceptionType = exceptionType;
                this.filter = filter;
            }

            internal Factory(TryBuilder builder)
                : this(builder, typeof(Exception), null)
            {

            }

            public CatchStatement Create(LexicalScope parent) => new CatchStatement(builder, exceptionType, filter, parent);
        }

        private readonly TryBuilder builder;
        private readonly ParameterExpression exception;
        private readonly Expression filter;

        private CatchStatement(TryBuilder builder, Type exceptionType, TryBuilder.Filter filter, LexicalScope parent)
            : base(parent)
        {
            this.builder = builder;
            exception = Expression.Variable(exceptionType, "e");
            this.filter = filter?.Invoke(exception);
        }

        TryBuilder ILexicalScope<TryBuilder, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope)
        {
            scope(exception);
            return builder.Catch(exception, filter, Build());
        }

        TryBuilder ILexicalScope<TryBuilder, Action>.Build(Action scope)
        {
            scope();
            return builder.Catch(exception, filter, Build());
        }
    }
}
