using System;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    internal sealed class RecoverFromExceptionExpression : TransitionExpression
    {
        internal readonly ParameterExpression Receiver;

        internal RecoverFromExceptionExpression(uint recoveryState, ParameterExpression receiver)
            : base(recoveryState)
        {
            Receiver = receiver;
        }
        
        public override Expression Reduce() => true.AsConst();
        public override Type Type => typeof(bool);

        internal override Expression Reduce(ParameterExpression stateMachine)
        {
            var tryRecover = stateMachine.Type.GetMethod(nameof(AsyncStateMachine<ValueTuple>.TryRecover));
            tryRecover = tryRecover.MakeGenericMethod(Receiver.Type);
            return stateMachine.Call(tryRecover, StateId.AsConst(), Receiver);
        }
    }
}
