namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

internal sealed class NoOpStateMachine : NoOpSnapshotManager, IStateMachine
{
    public readonly Dictionary<long, object> Context = new();
    
    ValueTask<long> IStateMachine.ApplyAsync(LogEntry entry, CancellationToken token)
    {
        if (entry.Context is { } context)
            Context.Add(entry.Index, context);

        return ValueTask.FromResult(entry.Index);
    }
}