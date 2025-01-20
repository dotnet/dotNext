using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices;

internal abstract class GuardedStatement(Expression expression, LabelTarget faultLabel) : Statement(expression)
{
    internal readonly LabelTarget FaultLabel = faultLabel;
}