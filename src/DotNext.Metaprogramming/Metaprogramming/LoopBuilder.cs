using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents generic loop builder.
    /// </summary>
    /// <remarks>
    /// This loop is equvalent to <code>while(true){ }</code>
    /// </remarks>
    public sealed class LoopBuilder : LoopBuilderBase, IExpressionBuilder<LoopExpression>
    {
        internal LoopBuilder(CompoundStatementBuilder parent)
            : base(parent)
        {
        }

        LoopExpression IExpressionBuilder<LoopExpression>.Build() => base.Build().Loop(breakLabel, continueLabel);

        internal override Expression Build() => ((IExpressionBuilder<LoopExpression>)this).Build();
    }
}