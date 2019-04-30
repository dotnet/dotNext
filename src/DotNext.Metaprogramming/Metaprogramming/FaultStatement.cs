using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class FaultStatement : LexicalScope<TryBuilder>
    {
        internal readonly struct Factory : IFactory<FaultStatement>
        {
            private readonly TryBuilder builder;

            internal Factory(TryBuilder builder) => this.builder = builder;

            public FaultStatement Create(LexicalScope parent) => new FaultStatement(builder, parent);
        }

        private readonly TryBuilder builder;

        private FaultStatement(TryBuilder builder, LexicalScope parent) : base(parent) => this.builder = builder;

        private protected override TryBuilder CreateExpression(Expression body) => builder.Fault(body);
    }
}
