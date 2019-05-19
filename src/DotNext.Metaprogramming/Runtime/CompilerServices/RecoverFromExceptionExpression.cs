using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Linq.Expressions.ExpressionBuilder;

    internal sealed class RecoverFromExceptionExpression : StateMachineExpression
    {
        internal readonly ParameterExpression Receiver;

        internal RecoverFromExceptionExpression(ParameterExpression receiver)
        {
            Receiver = receiver;
        }

        public override Expression Reduce() => true.Const();
        public override Type Type => typeof(bool);

        internal override Expression Reduce(ParameterExpression stateMachine)
        {
            var tryRecover = stateMachine.Type.GetMethod(nameof(AsyncStateMachine<ValueTuple>.TryRecover));
            Debug.Assert(!(tryRecover is null));
            tryRecover = tryRecover.MakeGenericMethod(Receiver.Type);
            return stateMachine.Call(tryRecover, Receiver);
        }
    }
}
