using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Threading;

    public sealed class LoopBuilder : LoopBuilderBase, IExpressionBuilder<LoopExpression>
    {
        internal LoopBuilder(ExpressionBuilder parent)
            : base(parent)
        {
        }

        LoopExpression IExpressionBuilder<LoopExpression>.Build() => base.Build().Loop(breakLabel, continueLabel);

        internal override Expression Build() => ((IExpressionBuilder<LoopExpression>)this).Build();
    }
}