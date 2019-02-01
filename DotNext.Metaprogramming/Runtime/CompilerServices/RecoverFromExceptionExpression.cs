using System;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    internal sealed class RecoverFromExceptionExpression : TransitionExpression
    {
        private readonly Expression receiver;

        internal RecoverFromExceptionExpression(ExitGuardedCodeExpression exitCall, ParameterExpression receiver)
            : base(exitCall.StateId)
        {
            this.receiver = receiver;
        }

        private RecoverFromExceptionExpression(uint stateId, Expression receiver)
            : base(stateId)
        {
            this.receiver = receiver;
        }

        public override Expression Reduce() => true.AsConst();
        public override Type Type => typeof(bool);

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var receiver = visitor.Visit(this.receiver);
            return ReferenceEquals(receiver, this.receiver) ? this : new RecoverFromExceptionExpression(StateId, receiver);
        }

        internal override Expression Reduce(ParameterExpression stateMachine)
        {
            var tryRecover = stateMachine.Type.GetMethod(nameof(AsyncStateMachine<ValueTuple>.TryRecover));
            tryRecover = tryRecover.MakeGenericMethod(receiver.Type);
            return stateMachine.Call(tryRecover, StateId.AsConst(), receiver);
        }
    }
}
