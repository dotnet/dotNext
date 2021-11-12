namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Provides default algorithm of log compaction.
/// </summary>
internal static class LogCompaction
{
    internal static ValueTask ForceIncrementalCompactionAsync(this MemoryBasedStateMachine state, CancellationToken token)
        => state.CompactionCount > 0L ? state.ForceCompactionAsync(1L, token) : ValueTask.CompletedTask;
}
