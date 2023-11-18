namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;

internal partial class LeaderState<TMember>
{
    private readonly Func<TMember, Replicator> replicatorFactory, localReplicatorFactory;

    internal Func<TimeSpan, TMember, IFailureDetector>? FailureDetectorFactory
    {
        init
        {
            if (value is not null)
            {
                replicatorFactory = member => new(member, Logger) { FailureDetector = value.Invoke(maxLease, member) };
            }
        }
    }

    private Replicator CreateDefaultReplicator(TMember member) => new(member, Logger);
}