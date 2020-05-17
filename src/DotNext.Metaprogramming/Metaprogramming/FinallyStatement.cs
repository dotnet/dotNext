using System;

namespace DotNext.Metaprogramming
{
    internal sealed class FinallyStatement : Statement, ILexicalScope<TryBuilder, Action>
    {
        private readonly TryBuilder builder;

        internal FinallyStatement(TryBuilder builder) => this.builder = builder;

        public TryBuilder Build(Action scope)
        {
            scope();
            return builder.Finally(scope);
        }
    }
}
