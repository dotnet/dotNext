namespace DotNext.Runtime.CompilerServices
{
    internal abstract class TransitionExpression: StateMachineExpression
    {
        internal readonly uint StateId;

        private protected TransitionExpression(uint state) => StateId = state;
    }
}
