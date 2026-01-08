using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices;

using Linq.Expressions;

internal abstract class StateMachineExpression : CustomExpression
{
    internal abstract Expression Reduce(ParameterExpression stateMachine);

    protected override Expression VisitChildren(ExpressionVisitor visitor) => this;
}