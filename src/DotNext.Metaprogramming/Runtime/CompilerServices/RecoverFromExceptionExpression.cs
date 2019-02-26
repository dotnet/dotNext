using System;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.ExpressionHelpers;

    internal sealed class RecoverFromExceptionExpression : StateMachineExpression
    {
        internal readonly ParameterExpression Receiver;

        internal RecoverFromExceptionExpression(ParameterExpression receiver)
        {
            Receiver = receiver;
        }
        
        public override Expression Reduce() => true.AsConst();
        public override Type Type => typeof(bool);

        internal override Expression Reduce(ParameterExpression stateMachine)
        {
            var tryRecover = stateMachine.Type.GetMethod(nameof(AsyncStateMachine<ValueTuple>.TryRecover));
            tryRecover = tryRecover.MakeGenericMethod(Receiver.Type);
            return stateMachine.Call(tryRecover, Receiver);
        }
    }
}
