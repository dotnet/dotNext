using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal class AsyncLeaderChangedEvent
    {
        private TaskCompletionSource<IRaftClusterMember> source = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void OnLeaderChanged(ICluster sender, IRaftClusterMember leader)
        {
            if (leader is null)
                return;
            source.TrySetResult(leader);
        }

        internal Task<IRaftClusterMember> Result => source.Task;

        internal void Reset() => source = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}