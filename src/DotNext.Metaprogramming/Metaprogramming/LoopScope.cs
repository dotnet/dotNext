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
    internal sealed class LoopScope : LoopScopeBase, IExpressionBuilder<LoopExpression>, ICompoundStatement<Action<LoopContext>>
    {
        internal LoopScope(LexicalScope parent)
            : base(parent)
        {
        }

        public new LoopExpression Build() => base.Build().Loop(BreakLabel, ContinueLabel);

        void ICompoundStatement<Action<LoopContext>>.ConstructBody(Action<LoopContext> body)
        {
            using (var cookie = new LoopContext(this))
                body(cookie);
        }
    }
}