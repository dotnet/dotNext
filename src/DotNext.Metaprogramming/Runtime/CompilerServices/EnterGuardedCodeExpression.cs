using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices;

using static Linq.Expressions.ExpressionBuilder;

internal sealed class EnterGuardedCodeExpression(uint stateId) : TransitionExpression(stateId)
{
    public override Type Type => typeof(void);

    public override Expression Reduce() => Empty();

    internal override Expression Reduce(ParameterExpression stateMachine)
        => stateMachine.Call(nameof(AsyncStateMachine<ValueTuple>.EnterGuardedCode), StateId);
}