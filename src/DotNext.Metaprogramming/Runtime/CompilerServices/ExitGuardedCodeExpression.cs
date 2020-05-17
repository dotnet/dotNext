using System;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Linq.Expressions.ExpressionBuilder;

    internal sealed class ExitGuardedCodeExpression : TransitionExpression
    {
        internal ExitGuardedCodeExpression(uint parentState)
            : base(parentState)
        {
        }

        internal ExitGuardedCodeExpression(StatePlaceholderExpression placeholder)
            : base(placeholder)
        {
        }

        public override Type Type => typeof(void);

        public override Expression Reduce() => Empty();

        internal override Expression Reduce(ParameterExpression stateMachine)
            => stateMachine.Call(nameof(AsyncStateMachine<ValueTuple>.ExitGuardedCode), StateId);
    }
}
