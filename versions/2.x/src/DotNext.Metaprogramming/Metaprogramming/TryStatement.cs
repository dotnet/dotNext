using System;

namespace DotNext.Metaprogramming
{
    internal sealed class TryStatement : Statement, ILexicalScope<TryBuilder, Action>
    {
        internal TryStatement()
        {
        }

        public TryBuilder Build(Action scope)
        {
            scope();
            return new TryBuilder(Build(), Parent ?? throw new InvalidOperationException());
        }
    }
}
