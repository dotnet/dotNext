using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{

    internal sealed class DefaultStatement : Statement<SwitchBuilder>
    {
        internal readonly struct Factory : IFactory<DefaultStatement>
        {
            private readonly SwitchBuilder builder;

            internal Factory(SwitchBuilder builder) => this.builder = builder;

            public DefaultStatement Create(LexicalScope parent) => new DefaultStatement(builder, parent);
        }

        private readonly SwitchBuilder builder;

        private DefaultStatement(SwitchBuilder builder, LexicalScope parent) : base(parent) => this.builder = builder;

        private protected override SwitchBuilder CreateExpression(Expression body) => builder.Default(body);
    }
}
