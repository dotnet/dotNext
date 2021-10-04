using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices;

using static Linq.Expressions.ExpressionBuilder;

internal sealed class EnterGuardedCodeExpression : TransitionExpression
{
    internal EnterGuardedCodeExpression(uint stateId)
        : base(stateId)
    {
    }

    public override Type Type => typeof(void);

    public override Expression Reduce() => Empty();

    internal override Expression Reduce(ParameterExpression stateMachine)
        => stateMachine.Call(nameof(AsyncStateMachine<ValueTuple>.EnterGuardedCode), StateId);
}