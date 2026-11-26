using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices;

using static Linq.Expressions.ExpressionBuilder;

internal sealed class EnterGuardedCodeExpression(uint stateId) : TransitionExpression(stateId)
{
    public override Type Type => typeof(void);

    public override DefaultExpression Reduce() => Empty();

    internal override MethodCallExpression Reduce(ParameterExpression stateMachine)
        => stateMachine.Call(nameof(AsyncStateMachine<>.EnterGuardedCode), StateId);
}