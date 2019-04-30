using System;

namespace DotNext.Metaprogramming
{
    using SwitchBuilder = Linq.Expressions.SwitchBuilder;

    internal readonly struct DefaultStatement : IStatement<SwitchBuilder, Action>
    {
        private readonly SwitchBuilder builder;

        internal DefaultStatement(SwitchBuilder builder) => this.builder = builder;

        SwitchBuilder IStatement<SwitchBuilder, Action>.Build(Action scope, ILexicalScope body)
        {
            scope();
            return builder.Default(body.Build());
        }
    }
}
