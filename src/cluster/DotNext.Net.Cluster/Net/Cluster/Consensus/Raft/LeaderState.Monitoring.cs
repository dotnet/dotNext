namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;
using Membership;

internal partial class LeaderState<TMember>
{
    private readonly Func<TMember, IClusterConfigurationStorage?, Replicator> replicatorFactory, localReplicatorFactory;

    internal Func<TimeSpan, TMember, IFailureDetector>? FailureDetectorFactory
    {
        init
        {
            if (value is not null)
            {
                replicatorFactory = (member, storage) => new(member, storage, Logger) { FailureDetector = value.Invoke(maxLease, member) };
            }
        }
    }

    private Replicator CreateDefaultReplicator(TMember member, IClusterConfigurationStorage? storage) => new(member, storage, Logger);
}