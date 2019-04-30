using System;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;

namespace DotNext.Runtime.CompilerServices
{
    using static Linq.Expressions.ExpressionBuilder;

    internal sealed class RethrowExpression : StateMachineExpression
    {
        public override Type Type => typeof(void);

        public override Expression Reduce() => Rethrow();

        internal static Expression Dispatch(ParameterExpression exceptionHolder)
        {
            var capture = Call(null,
                typeof(ExceptionDispatchInfo).GetMethod(nameof(ExceptionDispatchInfo.Capture), new[] { typeof(Exception) }),
                exceptionHolder);
            return capture.Call(nameof(ExceptionDispatchInfo.Throw));
        }

        internal override Expression Reduce(ParameterExpression stateMachine)
            => stateMachine.Call(nameof(AsyncStateMachine<ValueTuple>.Rethrow));
    }
}
