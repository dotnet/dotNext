using System;

namespace DotNext.Metaprogramming
{
    using TryBuilder = Linq.Expressions.TryBuilder;

    internal sealed class TryStatement : IStatement<TryBuilder, Action>
    {
        internal static TryStatement Instance = new TryStatement();

        private TryStatement() { }

        TryBuilder IStatement<TryBuilder, Action>.Build(Action scope, ILexicalScope body)
        {
            scope();
            return new TryBuilder(body.Build());
        }
    }
}
