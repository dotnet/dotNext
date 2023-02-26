namespace DotNext.Threading;

internal sealed class SingleProducerMultipleConsumersCoordinator : QueuedSynchronizer<uint>
{
    private volatile uint valve; // 0 or 1

    internal void SwitchValve()
    {
        uint currentState, newState = valve;
        do
        {
            currentState = newState;
            newState = currentState ^ 1U;
        }
        while ((newState = Interlocked.CompareExchange(ref valve, newState, currentState)) != currentState);
    }

    internal SingleProducerMultipleConsumersCoordinator()
        : base(concurrencyLevel: null)
    {
    }

    protected override bool CanAcquire(uint unexpectedState) => unexpectedState != valve;

    internal ValueTask WaitAsync(CancellationToken token = default)
        => AcquireAsync(valve, token);

    internal void Drain() => Release();
}