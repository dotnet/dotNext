using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices;

using static Linq.Expressions.ExpressionBuilder;

internal sealed class StateIdExpression : StateMachineExpression
{
    public override ConstantExpression Reduce() => 0U.Const();

    internal static MemberExpression Get(ParameterExpression stateMachine)
        => stateMachine.Property(nameof(AsyncStateMachine<ValueTuple>.StateId));

    internal override MemberExpression Reduce(ParameterExpression stateMachine)
        => Get(stateMachine);

    public override Type Type => typeof(uint);
}