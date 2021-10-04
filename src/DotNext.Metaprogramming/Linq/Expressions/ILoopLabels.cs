using LabelTarget = System.Linq.Expressions.LabelTarget;

namespace DotNext.Linq.Expressions;

internal interface ILoopLabels
{
    LabelTarget ContinueLabel { get; }

    LabelTarget BreakLabel { get; }
}