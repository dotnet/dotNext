using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class FinallyStatement : Statement<TryBuilder>
    {
        internal readonly struct Factory : IFactory<FinallyStatement>
        {
            private readonly TryBuilder builder;

            internal Factory(TryBuilder builder) => this.builder = builder;

            public FinallyStatement Create(LexicalScope parent) => new FinallyStatement(builder, parent);
        }

        private readonly TryBuilder builder;

        private FinallyStatement(TryBuilder builder, LexicalScope parent) : base(parent) => this.builder = builder;

        private protected override TryBuilder CreateExpression(Expression body) => builder.Finally(body);
    }
}
