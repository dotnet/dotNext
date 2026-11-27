using System.Linq.Expressions;
using System.Runtime.ExceptionServices;

namespace DotNext.Runtime.CompilerServices;

using static Linq.Expressions.ExpressionBuilder;

internal sealed class RethrowExpression : StateMachineExpression
{
    public override Type Type => typeof(void);

    public override UnaryExpression Reduce() => Rethrow();

    internal static MethodCallExpression Dispatch(ParameterExpression exceptionHolder)
        => Call(typeof(ExceptionDispatchInfo), nameof(ExceptionDispatchInfo.Throw), [], exceptionHolder);

    internal override MethodCallExpression Reduce(ParameterExpression stateMachine)
        => stateMachine.Call(nameof(AsyncStateMachine<>.Rethrow));
}