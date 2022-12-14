using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft;

[ExcludeFromCodeCoverage]
public abstract class RaftTest : Test
{
    [ExcludeFromCodeCoverage]
    private protected class LeaderChangedEvent
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

    internal static async Task<EndPoint> AssertLeadershipAsync(IEqualityComparer<EndPoint> comparer, params IRaftCluster[] nodes)
    {
        EndPoint ep = null;

        foreach (var n in nodes)
        {
            var leader = await n.WaitForLeaderAsync(DefaultTimeout);
            NotNull(leader);

            if (ep is null)
            {
                ep = leader.EndPoint;
            }
            else
            {
                Equal(ep, leader.EndPoint, comparer);
            }
        }

        NotNull(ep);
        return ep;
    }
}