using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{

    internal sealed class DefaultStatement : Statement, ILexicalScope<SwitchBuilder, Action>
    {
        private readonly SwitchBuilder builder;

        internal DefaultStatement(SwitchBuilder builder) => this.builder = builder;

        public SwitchBuilder Build(Action scope)
        {
            scope();
            return builder.Default(Build());
        }
    }
}
