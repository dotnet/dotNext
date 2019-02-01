using System;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    internal sealed class StateIdExpression: StateMachineExpression
    {
        public override Expression Reduce() => 0U.AsConst();

        internal static Expression Get(ParameterExpression stateMachine)
            => stateMachine.Property(nameof(AsyncStateMachine<ValueTuple>.StateId));

        internal override Expression Reduce(ParameterExpression stateMachine)
            => Get(stateMachine);

        public override Type Type => typeof(uint);
    }
}
