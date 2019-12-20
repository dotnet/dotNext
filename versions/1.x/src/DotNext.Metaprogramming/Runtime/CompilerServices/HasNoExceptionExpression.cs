using System;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Linq.Expressions.ExpressionBuilder;

    /// <summary>
    /// Represents exception check inside of state machine.
    /// </summary>
    internal sealed class HasNoExceptionExpression : StateMachineExpression
    {
        public override Type Type => typeof(bool);
        public override Expression Reduce() => Default(typeof(bool));
        internal override Expression Reduce(ParameterExpression stateMachine)
            => stateMachine.Property(nameof(AsyncStateMachine<ValueTuple>.HasNoException));
    }
}
