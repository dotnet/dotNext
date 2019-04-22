using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents generic loop builder.
    /// </summary>
    /// <remarks>
    /// This loop is equvalent to <c>while(true){ }</c>
    /// </remarks>
    internal sealed class LoopScope : LoopBuilderBase, IExpressionBuilder<LoopExpression>, ICompoundStatement<Action<LoopCookie>>
    {
        internal LoopScope(LexicalScope parent)
            : base(parent)
        {
        }

        public new LoopExpression Build() => base.Build().Loop(BreakLabel, ContinueLabel);

        void ICompoundStatement<Action<LoopCookie>>.ConstructBody(Action<LoopCookie> body)
        {
            using (var cookie = new LoopCookie(this))
                body(cookie);
        }
    }
}