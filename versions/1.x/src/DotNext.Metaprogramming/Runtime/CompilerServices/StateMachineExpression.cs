using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    internal abstract class StateMachineExpression : Expression
    {
        public sealed override bool CanReduce => true;
        public sealed override ExpressionType NodeType => ExpressionType.Extension;
        internal abstract Expression Reduce(ParameterExpression stateMachine);
        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;
    }
}
