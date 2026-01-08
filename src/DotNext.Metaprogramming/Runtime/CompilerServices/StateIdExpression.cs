using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices;

using Linq.Expressions;

internal sealed class StateIdExpression : StateMachineExpression
{
    public override ConstantExpression Reduce() => 0U.Quoted;

    private static MemberExpression Get(ParameterExpression stateMachine)
        => stateMachine.Property(nameof(AsyncStateMachine<>.StateId));

    internal override MemberExpression Reduce(ParameterExpression stateMachine)
        => Get(stateMachine);

    public override Type Type => typeof(uint);
}