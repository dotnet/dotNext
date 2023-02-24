namespace DotNext.Threading;

internal sealed class SingleProducerMultipleConsumersCoordinator : AsyncTrigger<SingleProducerMultipleConsumersCoordinator.State>
{
    internal sealed new class State
    {
        internal const uint ValveState0 = 0U;
        internal const uint ValveState1 = 1U;

        internal volatile uint Valve;

        internal void SwitchValve()
        {
            uint currentState, newState = Valve;
            do
            {
                currentState = newState;
                newState = currentState ^ ValveState1;
            }
            while ((newState = Interlocked.CompareExchange(ref Valve, newState, currentState)) != currentState);
        }
    }

    private sealed class Transition : ITransition
    {
        private static readonly Transition State0 = new(State.ValveState0), State1 = new(State.ValveState1);

        private readonly uint unexpectedState;

        private Transition(uint barrierStatus) => unexpectedState = barrierStatus;

        bool ITransition.Test(State state) => unexpectedState != state.Valve;

        void ITransition.Transit(State state)
        {
        }

        internal static Transition Get(uint state) => state is State.ValveState0 ? State0 : State1;
    }

    internal SingleProducerMultipleConsumersCoordinator()
        : base(new State())
    {
    }

    internal void SwitchValve() => base.State.SwitchValve();

    internal ValueTask WaitAsync(CancellationToken token = default)
        => WaitAsync(Transition.Get(base.State.Valve), token);
}