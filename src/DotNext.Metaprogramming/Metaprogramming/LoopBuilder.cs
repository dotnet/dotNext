using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents generic loop builder.
    /// </summary>
    /// <remarks>
    /// This loop is equvalent to <c>while(true){ }</c>
    /// </remarks>
    internal sealed class LoopBuilder : LoopBuilderBase, IExpressionBuilder<LoopExpression>
    {
        internal LoopBuilder(LexicalScope parent)
            : base(parent)
        {
        }

        public new LoopExpression Build() => base.Build().Loop(breakLabel, continueLabel);
    }
}