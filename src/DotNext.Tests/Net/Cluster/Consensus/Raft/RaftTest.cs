using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Timestamp = Diagnostics.Timestamp;

[ExcludeFromCodeCoverage]
public abstract class RaftTest : Test
{
    [ExcludeFromCodeCoverage]
    private protected class LeaderChangedEvent : TaskCompletionSource<IClusterMember>
    {
        internal LeaderChangedEvent()
            : base(TaskCreationOptions.RunContinuationsAsynchronously)
        {
        }

        internal void OnLeaderChanged(ICluster sender, IClusterMember leader)
        {
            if (leader is not null)
                TrySetResult(leader);
        }
    }

    internal static async Task<EndPoint> AssertLeadershipAsync(IEqualityComparer<EndPoint> comparer, params IRaftCluster[] nodes)
    {
        EndPoint ep = null;
        var startTime = new Timestamp();

    restart:
        foreach (var n in nodes)
        {
            var leader = await n.WaitForLeaderAsync(DefaultTimeout);
            NotNull(leader);

            if (ep is null)
            {
                ep = leader.EndPoint;
            }
            else if (comparer.Equals(ep, leader.EndPoint))
            {
                continue;
            }
            else if (startTime.Elapsed < DefaultTimeout)
            {
                goto restart;
            }
            else
            {
                Fail("Leader was not elected in timely manner");
            }
        }

        NotNull(ep);
        return ep;
    }
}