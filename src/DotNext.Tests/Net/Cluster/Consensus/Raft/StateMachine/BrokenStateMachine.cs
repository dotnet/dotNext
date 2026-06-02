namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

internal sealed class BrokenStateMachine : NoOpSnapshotManager, IStateMachine
{
    public ValueTask<long> ApplyAsync(LogEntry entry, CancellationToken token)
        => ValueTask.FromException<long>(new ArithmeticException());
}