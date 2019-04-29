using LabelTarget = System.Linq.Expressions.LabelTarget;

namespace DotNext.Linq.Expressions
{
    internal interface ILoopExpression
    {
         LabelTarget ContinueLabel { get; }
         LabelTarget BreakLabel { get; }
    }
}