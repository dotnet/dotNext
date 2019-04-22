using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class LocalScope: LexicalScope, IExpressionBuilder<Expression>
    {
        internal LocalScope(LexicalScope parent)
            : base(parent)
        {
        }

        public new Expression Build() => base.Build();
    }
}