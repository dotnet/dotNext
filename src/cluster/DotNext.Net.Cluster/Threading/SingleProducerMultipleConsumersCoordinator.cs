namespace DotNext.Threading;

// The producer performs rounds. Every round is enclosed by SwitchValve() and Drain() calls.
// The consumer waits for the completion of the first round started after the stamp has been captured.
internal sealed class SingleProducerMultipleConsumersCoordinator : QueuedSynchronizer<long>
{
    // Incremented twice per round: odd value means that the round is in progress, even value means that
    // the round is completed. If the round is not completed (Drain() is not called), the stamp remains odd.
    private long stamp;

    public SingleProducerMultipleConsumersCoordinator()
    {
        stamp = long.MinValue; // must be even initially
        IsFair = false;
    }

    internal long Stamp => Volatile.Read(in stamp);

    // Starts a new round. The callers waiting for the stamp captured after this call are not resumed by the Drain()
    // of this round, they will be resumed in the next round.
    internal void SwitchValve() => Interlocked.Increment(ref stamp);

    // The stamp of the completion of the first round started after the stamp has been captured:
    // even (idle) stamp s - the next round starts at s + 1 and completes at s + 2;
    // odd (in-progress) stamp s - the current round completes at s + 1, the next round starts at s + 2 and completes at s + 3
    private static long GetCompletionStamp(long capturedStamp) => (capturedStamp + 3L) & ~1L;

    protected override bool CanAcquire(long capturedStamp) => Stamp >= GetCompletionStamp(capturedStamp);

    // Waits for the completion of the first round started after this call
    internal ValueTask WaitAsync(CancellationToken token = default)
        => AcquireAsync(Stamp, token);

    // Waits for the completion of the first round started after the stamp has been captured
    internal ValueTask WaitAsync(long capturedStamp, CancellationToken token = default)
        => AcquireAsync(capturedStamp, token);

    // Completes the current round and resumes the suspended callers waiting for it
    internal void Drain()
    {
        Interlocked.Increment(ref stamp);
        Release();
    }
}
