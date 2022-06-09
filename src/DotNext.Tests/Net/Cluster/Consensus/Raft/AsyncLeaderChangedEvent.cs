namespace DotNext.Net.Cluster.Consensus.Raft;

internal class LeaderChangedEvent
{
    private TaskCompletionSource<IClusterMember> source = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void OnLeaderChanged(ICluster sender, IClusterMember leader)
    {
        if (leader is null)
            return;
        source.TrySetResult(leader);
    }

    internal Task<IClusterMember> Result => source.Task;

    internal void Reset() => source = new(TaskCreationOptions.RunContinuationsAsynchronously);
}