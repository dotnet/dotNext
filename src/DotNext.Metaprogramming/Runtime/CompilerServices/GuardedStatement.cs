using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    internal abstract class GuardedStatement : Statement
    {
        internal LabelTarget FaultLabel;

        private protected GuardedStatement(Expression expression, LabelTarget faultLabel)
            : base(expression)
        {
            FaultLabel = faultLabel;
        }
    }
}
