using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    internal abstract class TransitionExpression : StateMachineExpression
    {
        private protected readonly Expression StateId;

        private protected TransitionExpression(uint state) => StateId = Constant(state);

        private protected TransitionExpression(StatePlaceholderExpression placeholder) => StateId = placeholder;

        private protected TransitionExpression(StateIdExpression state) => StateId = state;
    }
}
