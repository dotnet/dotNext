using System;

namespace DotNext.Metaprogramming
{
    using TryBuilder = Linq.Expressions.TryBuilder;

    internal readonly struct FaultStatement : IStatement<TryBuilder, Action>
    {
        private readonly TryBuilder builder;

        internal FaultStatement(TryBuilder builder) => this.builder = builder;

        TryBuilder IStatement<TryBuilder, Action>.Build(Action scope, ILexicalScope body)
        {
            scope();
            return builder.Fault(body.Build());
        }
    }
}
