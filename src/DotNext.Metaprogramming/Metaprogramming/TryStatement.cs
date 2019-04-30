using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class TryStatement : LexicalScope<TryBuilder>
    {
        private sealed class SingletonFactory : IFactory<TryStatement>
        {
            TryStatement IFactory<TryStatement>.Create(LexicalScope parent) => new TryStatement(parent);
        }

        internal static readonly IFactory<TryStatement> Factory = new SingletonFactory();

        private TryStatement(LexicalScope parent) : base(parent) { }

        private protected override TryBuilder CreateExpression(Expression body) => new TryBuilder(body, Parent);
    }
}
