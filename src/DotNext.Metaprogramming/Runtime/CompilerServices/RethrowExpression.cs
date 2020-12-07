using System;
using System.Diagnostics;
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
            var captureMethod = typeof(ExceptionDispatchInfo).GetMethod(nameof(ExceptionDispatchInfo.Capture), new[] { typeof(Exception) });
            Debug.Assert(captureMethod is not null);
            return Call(null, captureMethod, exceptionHolder).Call(nameof(ExceptionDispatchInfo.Throw));
        }

        internal override Expression Reduce(ParameterExpression stateMachine)
            => stateMachine.Call(nameof(AsyncStateMachine<ValueTuple>.Rethrow));
    }
}
